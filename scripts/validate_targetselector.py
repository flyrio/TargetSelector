#!/usr/bin/env python3

import json
import re
import sys
import zipfile
from pathlib import Path
from urllib.parse import unquote, urlparse


REPO_ROOT = Path(__file__).resolve().parent.parent
TARGET_PATH = REPO_ROOT / "TargetSelector.json"
SYNC_CONFIG_PATH = REPO_ROOT / "scripts" / "sync_sources.json"
RELEASE_CONFIG_PATH = REPO_ROOT / "scripts" / "release_sources.json"

REQUIRED_FIELDS = (
    "Author",
    "Name",
    "InternalName",
    "AssemblyVersion",
    "Description",
    "ApplicableVersion",
    "DalamudApiLevel",
    "DownloadLinkInstall",
    "DownloadLinkUpdate",
    "DownloadLinkTesting",
)

URL_FIELDS = (
    "DownloadLinkInstall",
    "DownloadLinkUpdate",
    "DownloadLinkTesting",
    "RepoUrl",
    "IconUrl",
)


def fail(message: str) -> None:
    print(f"error: {message}", file=sys.stderr)
    raise SystemExit(1)


def assert_utf8_without_bom(path: Path) -> None:
    data = path.read_bytes()
    if data.startswith(b"\xef\xbb\xbf"):
        fail(f"{path.relative_to(REPO_ROOT)} must be UTF-8 without BOM")
    try:
        data.decode("utf-8")
    except UnicodeDecodeError as exc:
        fail(f"{path.relative_to(REPO_ROOT)} is not valid UTF-8: {exc}")


def load_json(path: Path):
    assert_utf8_without_bom(path)
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        fail(
            f"{path.relative_to(REPO_ROOT)} is not valid JSON "
            f"at line {exc.lineno}, column {exc.colno}: {exc.msg}"
        )


def validate_url(plugin_name: str, field_name: str, value: str) -> None:
    parsed = urlparse(value)
    if parsed.scheme not in {"http", "https"} or not parsed.netloc:
        fail(f"{plugin_name}: {field_name} is not an absolute HTTP(S) URL")


def resolve_local_raw_url(value: str) -> Path | None:
    parsed = urlparse(value)
    if parsed.scheme != "https" or parsed.netloc != "raw.githubusercontent.com":
        return None

    parts = [unquote(part) for part in parsed.path.strip("/").split("/")]
    if len(parts) < 4:
        return None
    owner, repo, branch = parts[:3]
    if owner != "flyrio" or repo != "TargetSelector" or branch != "main":
        return None

    relative_path = Path(*parts[3:])
    local_path = (REPO_ROOT / relative_path).resolve()
    repo_root = REPO_ROOT.resolve()
    if repo_root != local_path and repo_root not in local_path.parents:
        fail(f"local raw URL resolves outside the repository: {value}")
    return local_path


def validate_local_package(plugin_name: str, item: dict) -> None:
    install_link = item.get("DownloadLinkInstall")
    if not isinstance(install_link, str):
        return

    package_path = resolve_local_raw_url(install_link)
    if package_path is None:
        return
    if not package_path.exists():
        fail(f"{plugin_name}: local package does not exist: {package_path.relative_to(REPO_ROOT)}")
    if package_path.suffix.lower() != ".zip":
        fail(f"{plugin_name}: local package must be a zip file: {package_path.relative_to(REPO_ROOT)}")

    manifest_name = f"{plugin_name}.json"
    dll_name = f"{plugin_name}.dll"
    try:
        with zipfile.ZipFile(package_path) as archive:
            names = set(archive.namelist())
            if manifest_name not in names:
                fail(f"{plugin_name}: local package is missing {manifest_name}")
            if dll_name not in names:
                fail(f"{plugin_name}: local package is missing {dll_name}")

            package_manifest = json.loads(
                archive.read(manifest_name).decode("utf-8-sig")
            )
    except zipfile.BadZipFile as exc:
        fail(f"{plugin_name}: local package is not a valid zip file: {exc}")
    except json.JSONDecodeError as exc:
        fail(
            f"{plugin_name}: {manifest_name} inside local package is not valid JSON "
            f"at line {exc.lineno}, column {exc.colno}: {exc.msg}"
        )

    if package_manifest.get("InternalName") != plugin_name:
        fail(
            f"{plugin_name}: local package manifest InternalName is "
            f"{package_manifest.get('InternalName')!r}"
        )
    if package_manifest.get("AssemblyVersion") != item.get("AssemblyVersion"):
        fail(
            f"{plugin_name}: local package manifest AssemblyVersion "
            f"{package_manifest.get('AssemblyVersion')!r} does not match repo "
            f"manifest {item.get('AssemblyVersion')!r}"
        )
    if package_manifest.get("DalamudApiLevel") != item.get("DalamudApiLevel"):
        fail(
            f"{plugin_name}: local package manifest DalamudApiLevel "
            f"{package_manifest.get('DalamudApiLevel')!r} does not match repo "
            f"manifest {item.get('DalamudApiLevel')!r}"
        )


def validate_target_manifest() -> set[str]:
    items = load_json(TARGET_PATH)
    if not isinstance(items, list):
        fail("TargetSelector.json root must be a JSON array")
    if not items:
        fail("TargetSelector.json must contain at least one plugin")

    seen_internal_names = set()
    for index, item in enumerate(items):
        if not isinstance(item, dict):
            fail(f"plugin entry #{index + 1} must be a JSON object")

        internal_name = item.get("InternalName", f"entry #{index + 1}")

        missing = [field for field in REQUIRED_FIELDS if field not in item]
        if missing:
            fail(f"{internal_name}: missing required field(s): {', '.join(missing)}")

        if internal_name in seen_internal_names:
            fail(f"duplicate InternalName: {internal_name}")
        seen_internal_names.add(internal_name)

        api_level = item.get("DalamudApiLevel")
        if api_level != 15:
            fail(f"{internal_name}: DalamudApiLevel must be 15, got {api_level!r}")

        testing_api_level = item.get("TestingDalamudApiLevel")
        if testing_api_level is not None and testing_api_level != 15:
            fail(
                f"{internal_name}: TestingDalamudApiLevel must be 15 when present, "
                f"got {testing_api_level!r}"
            )

        for field_name in URL_FIELDS:
            value = item.get(field_name)
            if value is not None:
                if not isinstance(value, str):
                    fail(f"{internal_name}: {field_name} must be a string")
                validate_url(internal_name, field_name, value)

        validate_local_package(internal_name, item)

    return seen_internal_names


def validate_sync_config() -> None:
    config = load_json(SYNC_CONFIG_PATH)
    if not isinstance(config, dict):
        fail("scripts/sync_sources.json root must be a JSON object")

    sources = config.get("sources")
    if not isinstance(sources, list):
        fail("scripts/sync_sources.json must contain a sources array")

    for index, source in enumerate(sources):
        label = source.get("name", f"source #{index + 1}") if isinstance(source, dict) else f"source #{index + 1}"
        if not isinstance(source, dict):
            fail(f"{label}: source must be a JSON object")
        if not source.get("name"):
            fail(f"{label}: missing source name")
        if not source.get("url"):
            fail(f"{label}: missing source url")
        validate_url(label, "url", source["url"])
        plugins = source.get("plugins")
        if not isinstance(plugins, list) or not all(isinstance(name, str) for name in plugins):
            fail(f"{label}: plugins must be an array of strings")


def validate_release_config(target_internal_names: set[str]) -> None:
    config = load_json(RELEASE_CONFIG_PATH)
    if not isinstance(config, dict):
        fail("scripts/release_sources.json root must be a JSON object")

    sources = config.get("sources")
    if not isinstance(sources, list):
        fail("scripts/release_sources.json must contain a sources array")

    seen_internal_names = set()
    for index, source in enumerate(sources):
        label = source.get("internal_name", f"source #{index + 1}") if isinstance(source, dict) else f"source #{index + 1}"
        if not isinstance(source, dict):
            fail(f"{label}: release source must be a JSON object")

        required_string_fields = (
            "internal_name",
            "git_url",
            "repo",
            "asset_name_template",
        )
        for field_name in required_string_fields:
            if not isinstance(source.get(field_name), str) or not source[field_name]:
                fail(f"{label}: {field_name} must be a non-empty string")

        has_tag_regex = "tag_regex" in source
        has_fixed_tag = "fixed_tag" in source
        if has_tag_regex == has_fixed_tag:
            fail(f"{label}: exactly one of tag_regex or fixed_tag must be present")
        if has_tag_regex and (
            not isinstance(source.get("tag_regex"), str) or not source["tag_regex"]
        ):
            fail(f"{label}: tag_regex must be a non-empty string")
        if has_fixed_tag and (
            not isinstance(source.get("fixed_tag"), str) or not source["fixed_tag"]
        ):
            fail(f"{label}: fixed_tag must be a non-empty string")
        fixed_version = source.get("fixed_version")
        if fixed_version is not None and (
            not isinstance(fixed_version, str) or not fixed_version
        ):
            fail(f"{label}: fixed_version must be a non-empty string when present")

        internal_name = source["internal_name"]
        if internal_name in seen_internal_names:
            fail(f"duplicate release source InternalName: {internal_name}")
        seen_internal_names.add(internal_name)

        if internal_name not in target_internal_names:
            fail(f"{internal_name}: release source is not present in TargetSelector.json")

        validate_url(label, "git_url", source["git_url"])

        if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", source["repo"]):
            fail(f"{label}: repo must be in owner/name form")

        if has_tag_regex:
            try:
                re.compile(source["tag_regex"])
            except re.error as exc:
                fail(f"{label}: tag_regex is not a valid regular expression: {exc}")

        try:
            source["asset_name_template"].format(
                internal_name=internal_name,
                tag="v1.2.3",
                version="1.2.3",
            )
        except (KeyError, ValueError) as exc:
            fail(f"{label}: asset_name_template is invalid: {exc}")

        manifest_name = source.get("manifest_name")
        if manifest_name is not None:
            if not isinstance(manifest_name, str) or not manifest_name:
                fail(f"{label}: manifest_name must be a non-empty string when present")
            manifest_path = Path(manifest_name)
            if manifest_path.is_absolute() or ".." in manifest_path.parts:
                fail(f"{label}: manifest_name must be a relative zip member path")

        check_assembly_matches_tag = source.get("check_assembly_matches_tag")
        if check_assembly_matches_tag is not None and not isinstance(check_assembly_matches_tag, bool):
            fail(f"{label}: check_assembly_matches_tag must be a boolean when present")


def main() -> int:
    target_internal_names = validate_target_manifest()
    validate_sync_config()
    validate_release_config(target_internal_names)
    print("TargetSelector manifest validation passed")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
