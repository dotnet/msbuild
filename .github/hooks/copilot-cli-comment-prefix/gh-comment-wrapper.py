#!/usr/bin/env python3

from __future__ import annotations

import json
import os
import re
import subprocess
import sys
import tempfile
from pathlib import Path


TARGET_SLUG = "dotnet/msbuild"

COMMENT_ENDPOINT = re.compile(
    r"(?:"
    r"/issues/\d+/comments|"
    r"/issues/comments/\d+|"
    r"/pulls/\d+/(?:comments|reviews)|"
    r"/pulls/comments/\d+|"
    r"/comments/\d+/replies|"
    r"/reviews/\d+/comments"
    r")",
    re.IGNORECASE,
)
API_REPOSITORY = re.compile(
    r"(?:^|/)repos/([^/]+)/([^/?#]+)(?:/|$)", re.IGNORECASE
)
GITHUB_URL_PREFIX = re.compile(
    r"^(?:https?://github\.com/|git@github\.com:|github\.com/)", re.IGNORECASE
)
OPTIONS_WITH_VALUES = {
    "-F",
    "-H",
    "-R",
    "-X",
    "-b",
    "-f",
    "-q",
    "-t",
    "--body",
    "--body-file",
    "--cache",
    "--field",
    "--header",
    "--hostname",
    "--input",
    "--jq",
    "--method",
    "--raw-field",
    "--repo",
    "--template",
}


def read_prefix() -> str:
    prefix_path = Path(__file__).with_name("github-comment-prefix.txt")
    prefix = prefix_path.read_text(encoding="utf-8").replace("\r\n", "\n").rstrip(
        "\r\n"
    )
    if not prefix.strip():
        raise ValueError(f"GitHub comment prefix is empty: {prefix_path}")
    return prefix


def add_prefix(body: str) -> str:
    prefix = read_prefix()
    if body.replace("\r\n", "\n").startswith(prefix):
        return body
    return f"{prefix}\n\n{body}"


def normalize_repository(value: str) -> str | None:
    candidate = value.strip()
    prefixes = (
        "https://github.com/",
        "http://github.com/",
        "git@github.com:",
        "github.com/",
        "https://api.github.com/",
        "http://api.github.com/",
    )
    for prefix in prefixes:
        if candidate.casefold().startswith(prefix):
            candidate = candidate[len(prefix) :]
            break

    candidate = candidate.strip("/")
    if candidate.casefold().startswith("repos/"):
        candidate = candidate[len("repos/") :]

    parts = candidate.split("/")
    if len(parts) < 2 or not parts[0] or not parts[1]:
        return None

    repository = parts[1].split("?", 1)[0].split("#", 1)[0]
    if repository.casefold().endswith(".git"):
        repository = repository[:-4]
    return f"{parts[0]}/{repository}".casefold()


def explicit_repository(arguments: list[str]) -> tuple[bool, str | None]:
    index = 0
    while index < len(arguments):
        argument = arguments[index]
        if argument in {"-R", "--repo"}:
            if index + 1 >= len(arguments):
                return True, None
            return True, normalize_repository(arguments[index + 1])
        if argument.startswith("--repo="):
            return True, normalize_repository(argument[len("--repo=") :])
        if argument.startswith("-R") and len(argument) > 2:
            return True, normalize_repository(argument[2:])

        if argument in OPTIONS_WITH_VALUES and index + 1 < len(arguments):
            index += 2
        else:
            index += 1

    return False, None


def command_offset(arguments: list[str]) -> int | None:
    index = 0
    while index < len(arguments):
        argument = arguments[index]
        if argument in {"-R", "--repo", "--hostname"}:
            if index + 1 >= len(arguments):
                return None
            index += 2
            continue
        if argument.startswith(("--repo=", "--hostname=")):
            index += 1
            continue
        if argument.startswith("-R") and len(argument) > 2:
            index += 1
            continue
        return index
    return None


def comment_target_repository(
    arguments: list[str], offset: int
) -> str | None:
    target_index = offset + 2
    if target_index >= len(arguments):
        return None

    target = arguments[target_index]
    if target.startswith("-") or not GITHUB_URL_PREFIX.search(target):
        return None
    return normalize_repository(target)


def api_endpoint(arguments: list[str], offset: int) -> str | None:
    index = offset + 1
    while index < len(arguments):
        argument = arguments[index]
        if argument in OPTIONS_WITH_VALUES:
            index += 2
            continue
        if argument.startswith("-"):
            index += 1
            continue
        return argument
    return None


def api_target_repository(endpoint: str) -> str | None:
    match = API_REPOSITORY.search(endpoint)
    if match is None:
        return None

    owner = match.group(1)
    repository = match.group(2)
    if owner == "{owner}" and repository == "{repo}":
        return TARGET_SLUG
    return f"{owner}/{repository}".casefold()


def targets_msbuild(arguments: list[str]) -> bool:
    has_explicit_repository, repository = explicit_repository(arguments)
    if has_explicit_repository:
        return repository == TARGET_SLUG

    offset = command_offset(arguments)
    if offset is None:
        return False

    if (
        offset + 1 < len(arguments)
        and arguments[offset] in {"issue", "pr"}
        and arguments[offset + 1] == "comment"
    ) or (
        offset + 1 < len(arguments)
        and arguments[offset] == "pr"
        and arguments[offset + 1] == "review"
    ):
        repository = comment_target_repository(arguments, offset)
        return repository is None or repository == TARGET_SLUG

    if arguments[offset] == "api":
        endpoint = api_endpoint(arguments, offset)
        if endpoint is None:
            return False
        repository = api_target_repository(endpoint)
        return repository is None or repository == TARGET_SLUG

    return False


def new_temp_file(suffix: str, content: str, temporary_paths: list[Path]) -> Path:
    descriptor, path = tempfile.mkstemp(
        prefix="copilot-gh-comment-", suffix=suffix, dir=os.environ.get("TMPDIR")
    )
    temporary_path = Path(path)
    temporary_paths.append(temporary_path)
    with os.fdopen(descriptor, "w", encoding="utf-8", newline="") as stream:
        stream.write(content)
    return temporary_path


def prefixed_body_file(path: str, temporary_paths: list[Path]) -> str:
    if path == "-":
        return path

    source_path = Path(path).expanduser()
    if not source_path.is_file():
        return path

    try:
        body = source_path.read_text(encoding="utf-8")
        prefixed_body = add_prefix(body)
    except (OSError, UnicodeError):
        return path

    if prefixed_body == body:
        return path
    return str(new_temp_file(".md", prefixed_body, temporary_paths))


def prefixed_json_file(path: str, temporary_paths: list[Path]) -> str:
    if path == "-":
        return path

    source_path = Path(path).expanduser()
    if not source_path.is_file():
        return path

    try:
        payload = json.loads(source_path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError):
        return path

    if not isinstance(payload, dict) or not isinstance(payload.get("body"), str):
        return path

    prefixed_body = add_prefix(payload["body"])
    if prefixed_body == payload["body"]:
        return path

    payload["body"] = prefixed_body
    content = json.dumps(payload, ensure_ascii=False, separators=(",", ":"))
    return str(new_temp_file(".json", content, temporary_paths))


def transform_comment_args(
    arguments: list[str], temporary_paths: list[Path]
) -> list[str]:
    transformed = list(arguments)
    offset = command_offset(transformed)
    if offset is None or offset + 1 >= len(transformed):
        return transformed

    is_comment = (
        transformed[offset] in {"issue", "pr"}
        and transformed[offset + 1] == "comment"
    )
    is_review = (
        transformed[offset] == "pr" and transformed[offset + 1] == "review"
    )
    if not (is_comment or is_review):
        return transformed

    index = offset + 2
    while index < len(transformed):
        argument = transformed[index]

        if argument in {"--body", "-b"} and index + 1 < len(transformed):
            transformed[index + 1] = add_prefix(transformed[index + 1])
            index += 2
            continue

        if argument.startswith("--body="):
            transformed[index] = f"--body={add_prefix(argument[len('--body='):])}"
            index += 1
            continue

        if argument in {"--body-file", "-F"} and index + 1 < len(transformed):
            transformed[index + 1] = prefixed_body_file(
                transformed[index + 1], temporary_paths
            )
            index += 2
            continue

        if argument.startswith("--body-file="):
            path = argument[len("--body-file=") :]
            transformed[index] = (
                f"--body-file={prefixed_body_file(path, temporary_paths)}"
            )

        index += 1

    return transformed


def api_write_details(arguments: list[str]) -> tuple[int | None, bool]:
    offset = command_offset(arguments)
    if offset is None or arguments[offset] != "api":
        return None, False

    endpoint = api_endpoint(arguments, offset)
    if endpoint is None or not COMMENT_ENDPOINT.search(endpoint):
        return None, False

    explicit_method: str | None = None
    has_write_field = False
    index = offset + 1

    while index < len(arguments):
        argument = arguments[index]
        if argument in {"-X", "--method"} and index + 1 < len(arguments):
            explicit_method = arguments[index + 1].upper()
            index += 2
            continue
        if argument.startswith("--method="):
            explicit_method = argument[len("--method=") :].upper()
        elif argument.startswith("-X") and len(argument) > 2:
            explicit_method = argument[2:].upper()

        if argument in {"-f", "--raw-field", "-F", "--field", "--input"}:
            has_write_field = True
        elif argument.startswith(("--raw-field=", "--field=", "--input=")):
            has_write_field = True

        index += 1

    is_write = explicit_method in {"POST", "PUT", "PATCH"} or (
        explicit_method is None and has_write_field
    )
    return offset, is_write


def transform_api_args(
    arguments: list[str], temporary_paths: list[Path]
) -> list[str]:
    offset, is_write = api_write_details(arguments)
    if offset is None or not is_write:
        return list(arguments)

    transformed = list(arguments)
    index = offset + 1

    while index < len(transformed):
        argument = transformed[index]

        if (
            argument in {"-f", "--raw-field", "-F", "--field"}
            and index + 1 < len(transformed)
        ):
            field = transformed[index + 1]
            if field.startswith("body="):
                value = field[len("body=") :]
                if argument in {"-F", "--field"} and value.startswith("@"):
                    value = f"@{prefixed_body_file(value[1:], temporary_paths)}"
                else:
                    value = add_prefix(value)
                transformed[index + 1] = f"body={value}"
            index += 2
            continue

        field_prefixes = {
            "--raw-field=": False,
            "--field=": True,
        }
        matched_field = False
        for prefix, supports_file in field_prefixes.items():
            if not argument.startswith(prefix):
                continue
            field = argument[len(prefix) :]
            if not field.startswith("body="):
                break
            value = field[len("body=") :]
            if supports_file and value.startswith("@"):
                value = f"@{prefixed_body_file(value[1:], temporary_paths)}"
            else:
                value = add_prefix(value)
            transformed[index] = f"{prefix}body={value}"
            matched_field = True
            break
        if matched_field:
            index += 1
            continue

        if argument == "--input" and index + 1 < len(transformed):
            transformed[index + 1] = prefixed_json_file(
                transformed[index + 1], temporary_paths
            )
            index += 2
            continue

        if argument.startswith("--input="):
            path = argument[len("--input=") :]
            transformed[index] = (
                f"--input={prefixed_json_file(path, temporary_paths)}"
            )

        index += 1

    return transformed


def transform_arguments(
    arguments: list[str], temporary_paths: list[Path]
) -> list[str]:
    if not targets_msbuild(arguments):
        return list(arguments)

    transformed = transform_comment_args(arguments, temporary_paths)
    return transform_api_args(transformed, temporary_paths)


def run_gh(real_gh: str, arguments: list[str]) -> int:
    completed = subprocess.run([real_gh, *arguments], check=False)
    if completed.returncode < 0:
        return 128 - completed.returncode
    return completed.returncode


def main() -> int:
    if len(sys.argv) < 2:
        return 127

    real_gh = sys.argv[1]
    original_arguments = sys.argv[2:]
    temporary_paths: list[Path] = []

    try:
        try:
            arguments = transform_arguments(original_arguments, temporary_paths)
        except Exception:
            arguments = original_arguments
        return run_gh(real_gh, arguments)
    finally:
        for temporary_path in temporary_paths:
            try:
                temporary_path.unlink(missing_ok=True)
            except OSError:
                pass


if __name__ == "__main__":
    raise SystemExit(main())
