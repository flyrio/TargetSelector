#!/usr/bin/env python3

import json
import sys
import zipfile
from pathlib import Path
from urllib.parse import unquote, urlparse


REPO_ROOT = Path(__file__).resolve().parent.parent
TARGET_PATH = REPO_ROOT / "TargetSelector.json"
SYNC_CONFIG_PATH = REPO_ROOT / "scripts" / "sync_sources.json"

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


def validate_target_manifest() -> None:
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


def main() -> int:
    validate_target_manifest()
    validate_sync_config()
    print("TargetSelector manifest validation passed")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
