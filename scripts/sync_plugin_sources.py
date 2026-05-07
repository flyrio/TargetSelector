#!/usr/bin/env python3

import json
import re
import sys
import urllib.request
from pathlib import Path


SYNC_FIELDS = (
    "AssemblyVersion",
    "ApplicableVersion",
    "DalamudApiLevel",
)


def load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def fetch_json(url: str):
    request = urllib.request.Request(
        url,
        headers={
            "User-Agent": "TargetSelector sync workflow",
            "Accept": "application/json",
        },
    )
    with urllib.request.urlopen(request, timeout=30) as response:
        return json.loads(response.read().decode("utf-8"))


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
        for field_name in {
            "AssemblyVersion",
            "ApplicableVersion",
            "DalamudApiLevel",
            "TestingDalamudApiLevel",
            "DownloadLinkInstall",
            "DownloadLinkUpdate",
            "DownloadLinkTesting",
        }
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


def main():
    repo_root = Path(__file__).resolve().parent.parent
    target_path = repo_root / "TargetSelector.json"
    config_path = repo_root / "scripts" / "sync_sources.json"

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
        source_items = fetch_json(source["url"])
        source_map = {
            item["InternalName"]: item
            for item in source_items
            if "InternalName" in item
        }

        for internal_name in source["plugins"]:
            target_item = target_map.get(internal_name)
            source_item = source_map.get(internal_name)

            if target_item is None:
                print(
                    f"skip: {internal_name} is not present in TargetSelector.json",
                    file=sys.stderr,
                )
                continue
            if source_item is None:
                print(
                    f"skip: {internal_name} is missing from {source['name']}",
                    file=sys.stderr,
                )
                continue

            for field_name in SYNC_FIELDS:
                update_field(
                    target_item,
                    field_name,
                    source_item.get(field_name),
                    source["name"],
                    changes,
                )

            testing_api = source_item.get(
                "TestingDalamudApiLevel",
                source_item.get("DalamudApiLevel"),
            )
            update_field(
                target_item,
                "TestingDalamudApiLevel",
                testing_api,
                source["name"],
                changes,
            )

            install_link = source_item.get("DownloadLinkInstall")
            update_link = source_item.get("DownloadLinkUpdate", install_link)
            testing_link = source_item.get(
                "DownloadLinkTesting",
                update_link or install_link,
            )

            update_field(
                target_item,
                "DownloadLinkInstall",
                install_link,
                source["name"],
                changes,
            )
            update_field(
                target_item,
                "DownloadLinkUpdate",
                update_link,
                source["name"],
                changes,
            )
            update_field(
                target_item,
                "DownloadLinkTesting",
                testing_link,
                source["name"],
                changes,
            )

    if changes:
        for target_item in target_items:
            internal_name = target_item.get("InternalName")
            if not internal_name:
                continue
            desired_values[internal_name] = {
                field_name: target_item[field_name]
                for field_name in (
                    "AssemblyVersion",
                    "ApplicableVersion",
                    "DalamudApiLevel",
                    "TestingDalamudApiLevel",
                    "DownloadLinkInstall",
                    "DownloadLinkUpdate",
                    "DownloadLinkTesting",
                )
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
