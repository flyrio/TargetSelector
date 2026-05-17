#!/usr/bin/env python3

import io
import json
import re
import subprocess
import sys
import urllib.error
import urllib.request
import zipfile
from pathlib import Path
from urllib.parse import quote


TARGET_DALAMUD_API_LEVEL = 15

SYNC_FIELDS = (
    "AssemblyVersion",
    "ApplicableVersion",
    "DalamudApiLevel",
    "TestingDalamudApiLevel",
    "DownloadLinkInstall",
    "DownloadLinkUpdate",
    "DownloadLinkTesting",
)


def fail(message: str) -> None:
    print(f"error: {message}", file=sys.stderr)
    raise SystemExit(1)


def load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def fetch_bytes(url: str) -> bytes:
    request = urllib.request.Request(
        url,
        headers={
            "User-Agent": "TargetSelector release sync workflow",
            "Accept": "application/octet-stream",
        },
    )
    with urllib.request.urlopen(request, timeout=120) as response:
        return response.read()


def list_remote_tags(git_url: str) -> list[str]:
    process = subprocess.run(
        ["git", "ls-remote", "--tags", "--refs", git_url],
        check=False,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    if process.returncode != 0:
        fail(
            "failed to list tags from "
            f"{git_url}: {process.stderr.strip() or process.stdout.strip()}"
        )

    tags = []
    for line in process.stdout.splitlines():
        parts = line.split()
        if len(parts) != 2:
            continue
        ref = parts[1]
        prefix = "refs/tags/"
        if ref.startswith(prefix):
            tags.append(ref[len(prefix) :])
    return tags


def version_sort_key(version: str) -> tuple[int, ...]:
    numbers = re.findall(r"\d+", version)
    if not numbers:
        fail(f"cannot derive numeric version key from {version!r}")
    return tuple(int(part) for part in numbers)


def version_parts(version: str) -> tuple[int, ...]:
    numbers = re.findall(r"\d+", version)
    if not numbers:
        return ()
    return tuple(int(part) for part in numbers)


def assembly_version_matches_tag(
    assembly_version: str,
    tag_version: str,
) -> bool:
    assembly_parts = version_parts(assembly_version)
    tag_parts = version_parts(tag_version)
    if not assembly_parts or not tag_parts:
        return False
    if assembly_parts[: len(tag_parts)] != tag_parts:
        return False
    return all(part == 0 for part in assembly_parts[len(tag_parts) :])


def select_latest_tag(source: dict) -> tuple[str, str]:
    git_url = source["git_url"]
    tag_pattern = re.compile(source["tag_regex"])
    candidates = []

    for tag in list_remote_tags(git_url):
        match = tag_pattern.fullmatch(tag)
        if not match:
            continue

        group_map = match.groupdict()
        if "version" in group_map:
            version = group_map["version"]
        elif match.groups():
            version = match.group(1)
        else:
            version = tag

        candidates.append((version_sort_key(version), tag, version))

    if not candidates:
        fail(f"{source['internal_name']}: no tag matches {source['tag_regex']!r}")

    _, tag, version = max(candidates, key=lambda item: item[0])
    return tag, version


def build_release_asset_url(source: dict, tag: str, version: str) -> str:
    asset_name = source["asset_name_template"].format(
        internal_name=source["internal_name"],
        tag=tag,
        version=version,
    )
    escaped_tag = quote(tag, safe="")
    escaped_asset_name = quote(asset_name, safe="")
    return (
        f"https://github.com/{source['repo']}/releases/download/"
        f"{escaped_tag}/{escaped_asset_name}"
    )


def read_plugin_manifest_from_zip(
    internal_name: str,
    package_bytes: bytes,
    manifest_name: str,
) -> dict:
    try:
        with zipfile.ZipFile(io.BytesIO(package_bytes)) as archive:
            names = set(archive.namelist())
            if manifest_name not in names:
                fail(f"{internal_name}: release package is missing {manifest_name}")
            return json.loads(archive.read(manifest_name).decode("utf-8-sig"))
    except zipfile.BadZipFile as exc:
        fail(f"{internal_name}: release package is not a valid zip file: {exc}")
    except json.JSONDecodeError as exc:
        fail(
            f"{internal_name}: {manifest_name} inside release package is not valid JSON "
            f"at line {exc.lineno}, column {exc.colno}: {exc.msg}"
        )


def build_desired_values(source: dict, tag: str, version: str) -> dict | None:
    internal_name = source["internal_name"]
    manifest_name = source.get("manifest_name", f"{internal_name}.json")
    download_link = build_release_asset_url(source, tag, version)
    try:
        package_bytes = fetch_bytes(download_link)
    except urllib.error.HTTPError as exc:
        if exc.code == 404:
            print(
                f"skip: {internal_name} {tag} release asset is missing: {download_link}",
                file=sys.stderr,
            )
            return None
        fail(f"{internal_name}: failed to download {download_link}: HTTP {exc.code}")
    except urllib.error.URLError as exc:
        fail(f"{internal_name}: failed to download {download_link}: {exc}")

    manifest = read_plugin_manifest_from_zip(
        internal_name,
        package_bytes,
        manifest_name,
    )

    if manifest.get("InternalName") != internal_name:
        fail(
            f"{internal_name}: release package manifest InternalName is "
            f"{manifest.get('InternalName')!r}"
        )

    api_level = manifest.get("DalamudApiLevel")
    if api_level != TARGET_DALAMUD_API_LEVEL:
        print(
            f"skip: {internal_name} {tag} has DalamudApiLevel {api_level!r}, "
            f"expected {TARGET_DALAMUD_API_LEVEL}",
            file=sys.stderr,
        )
        return None

    assembly_version = manifest.get("AssemblyVersion")
    if source.get("check_assembly_matches_tag", True):
        if not isinstance(assembly_version, str) or not assembly_version_matches_tag(
            assembly_version,
            version,
        ):
            print(
                f"skip: {internal_name} {tag} has AssemblyVersion "
                f"{assembly_version!r}, expected it to match tag version {version!r}",
                file=sys.stderr,
            )
            return None

    desired = {
        "DalamudApiLevel": api_level,
        "TestingDalamudApiLevel": manifest.get("TestingDalamudApiLevel", api_level),
        "DownloadLinkInstall": download_link,
        "DownloadLinkUpdate": download_link,
        "DownloadLinkTesting": download_link,
    }

    for field_name in ("AssemblyVersion", "ApplicableVersion"):
        if manifest.get(field_name) is not None:
            desired[field_name] = manifest[field_name]

    return desired


def update_field(target_item, field_name, new_value, source_name, change_log):
    if new_value is None:
        return
    old_value = target_item.get(field_name)
    if old_value == new_value:
        return
    target_item[field_name] = new_value
    change_log.append(
        f"{target_item['InternalName']}: {field_name} <- {source_name}"
    )


def replace_target_text(path: Path, desired_values):
    with path.open("r", encoding="utf-8-sig", newline="") as file:
        original_text = file.read()
    lines = original_text.splitlines(keepends=True)
    current_internal_name = None
    changed = False
    updated_lines = []

    internal_name_pattern = re.compile(r'^\s*"InternalName":\s*"([^"]+)",?\s*$')
    field_patterns = {
        field_name: re.compile(
            rf'^(\s*)"{re.escape(field_name)}":\s*(.+?)(,?\s*)$'
        )
        for field_name in SYNC_FIELDS
    }
    object_end_pattern = re.compile(r"^\s*}\s*,?\s*$")

    for line in lines:
        stripped_line = line.rstrip("\r\n")
        newline = line[len(stripped_line) :]

        match = internal_name_pattern.match(stripped_line)
        if match:
            current_internal_name = match.group(1)

        replacement_map = desired_values.get(current_internal_name)
        if replacement_map:
            for field_name, pattern in field_patterns.items():
                field_match = pattern.match(stripped_line)
                if not field_match or field_name not in replacement_map:
                    continue

                serialized_value = json.dumps(
                    replacement_map[field_name],
                    ensure_ascii=False,
                )
                new_line = (
                    f'{field_match.group(1)}"{field_name}": '
                    f"{serialized_value}{field_match.group(3)}{newline}"
                )
                if new_line != line:
                    line = new_line
                    stripped_line = line.rstrip("\r\n")
                    changed = True
                break

        updated_lines.append(line)

        if object_end_pattern.match(stripped_line):
            current_internal_name = None

    if changed:
        with path.open("w", encoding="utf-8", newline="") as file:
            file.write("".join(updated_lines))

    return changed


def main() -> int:
    repo_root = Path(__file__).resolve().parent.parent
    target_path = repo_root / "TargetSelector.json"
    config_path = repo_root / "scripts" / "release_sources.json"

    target_items = load_json(target_path)
    config = load_json(config_path)
    target_map = {
        item["InternalName"]: item
        for item in target_items
        if "InternalName" in item
    }

    changes = []
    desired_values = {}

    for source in config["sources"]:
        internal_name = source["internal_name"]
        target_item = target_map.get(internal_name)
        if target_item is None:
            print(
                f"skip: {internal_name} is not present in TargetSelector.json",
                file=sys.stderr,
            )
            continue

        tag, version = select_latest_tag(source)
        desired = build_desired_values(source, tag, version)
        if desired is None:
            continue

        for field_name in SYNC_FIELDS:
            update_field(
                target_item,
                field_name,
                desired.get(field_name),
                f"{source['repo']} {tag}",
                changes,
            )

    if changes:
        for target_item in target_items:
            internal_name = target_item.get("InternalName")
            if not internal_name:
                continue
            desired_values[internal_name] = {
                field_name: target_item[field_name]
                for field_name in SYNC_FIELDS
                if field_name in target_item
            }

        if not replace_target_text(target_path, desired_values):
            print("no textual changes")
            return 0

        print("updated TargetSelector.json")
        for change in changes:
            print(change)
        return 0

    print("no changes")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
