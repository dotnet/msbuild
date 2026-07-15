#!/usr/bin/env bash

set -euo pipefail

hook_dir="$(
    cd "$(dirname "${BASH_SOURCE[0]}")/.." >/dev/null 2>&1
    pwd -P
)"
temporary_root="$(mktemp -d "${TMPDIR:-/tmp}/copilot-msbuild-comment-prefix-tests.XXXXXX")"
trap 'rm -rf "$temporary_root"' EXIT

python3 - "$hook_dir" "$temporary_root" <<'PY'
import json
import os
import subprocess
import sys
from pathlib import Path


hook_dir = Path(sys.argv[1])
temporary_root = Path(sys.argv[2])
handler = hook_dir / "prefix-github-comments.py"
shell_wrapper = hook_dir / "gh-comment-wrapper.sh"
python_wrapper = hook_dir / "gh-comment-wrapper.py"
prefix = (
    (hook_dir / "github-comment-prefix.txt")
    .read_text(encoding="utf-8")
    .replace("\r\n", "\n")
    .rstrip("\r\n")
)


def assert_true(condition, message):
    if not condition:
        raise AssertionError(message)


def invoke_handler(payload):
    completed = subprocess.run(
        [sys.executable, str(handler)],
        input=json.dumps(payload),
        text=True,
        capture_output=True,
        check=True,
    )
    return json.loads(completed.stdout)


target_mcp = invoke_handler(
    {
        "toolName": "github-mcp-server-add_issue_comment",
        "toolArgs": json.dumps(
            {"owner": "dotnet", "repo": "msbuild", "issue_number": 1, "body": "hello"}
        ),
    }
)
assert_true(
    target_mcp["modifiedArgs"]["body"].startswith(prefix),
    "Target MCP body was not prefixed",
)

other_mcp = invoke_handler(
    {
        "toolName": "github-mcp-server-add_issue_comment",
        "toolArgs": json.dumps(
            {
                "owner": "JanProvaznik",
                "repo": "msbuild",
                "issue_number": 1,
                "body": "hello",
            }
        ),
    }
)
assert_true(
    "modifiedArgs" not in other_mcp,
    "Another repository MCP body was modified",
)

idempotent = invoke_handler(
    {
        "toolName": "github-mcp-server-add_issue_comment",
        "toolArgs": json.dumps(
            {
                "owner": "dotnet",
                "repo": "msbuild",
                "issue_number": 1,
                "body": target_mcp["modifiedArgs"]["body"],
            }
        ),
    }
)
assert_true("modifiedArgs" not in idempotent, "MCP prefix was duplicated")

shell = invoke_handler(
    {
        "toolName": "bash",
        "toolArgs": json.dumps(
            {
                "command": 'gh issue comment 1 --repo dotnet/msbuild --body "hello"'
            }
        ),
    }
)
assert_true(
    str(shell_wrapper) in shell["modifiedArgs"]["command"],
    "Bash gh command was not wrapped",
)

leading_repo = invoke_handler(
    {
        "toolName": "bash",
        "toolArgs": json.dumps(
            {
                "command": 'gh --repo dotnet/msbuild issue comment 1 --body "hello"'
            }
        ),
    }
)
assert_true(
    str(shell_wrapper) in leading_repo["modifiedArgs"]["command"],
    "Bash gh command with leading --repo was not wrapped",
)

read_only = invoke_handler(
    {
        "toolName": "bash",
        "toolArgs": json.dumps(
            {"command": "gh issue view 1 --repo dotnet/msbuild"}
        ),
    }
)
assert_true("modifiedArgs" not in read_only, "Read-only gh command was modified")

invalid = subprocess.run(
    [sys.executable, str(handler)],
    input="{not-json",
    text=True,
    capture_output=True,
    check=True,
)
assert_true(json.loads(invalid.stdout) == {}, "Malformed hook input did not fail open")

capture_path = temporary_root / "capture.json"
fake_gh = temporary_root / "gh"
fake_gh.write_text(
    """#!/usr/bin/env python3
import json
import os
import sys
from pathlib import Path

args = sys.argv[1:]
capture = {"args": args}
for index, argument in enumerate(args):
    if argument == "--body-file" and index + 1 < len(args):
        path = Path(args[index + 1])
        if path.is_file():
            capture["body_file_path"] = str(path)
            capture["body_file"] = path.read_text(encoding="utf-8")

Path(os.environ["CAPTURE_PATH"]).write_text(
    json.dumps(capture, ensure_ascii=False), encoding="utf-8"
)
""",
    encoding="utf-8",
)
fake_gh.chmod(0o755)


def run_shell_gh(arguments, source_twice=False):
    environment = dict(os.environ)
    environment["PATH"] = f"{temporary_root}{os.pathsep}{environment['PATH']}"
    environment["CAPTURE_PATH"] = str(capture_path)
    sources = f'. "{shell_wrapper}"; '
    if source_twice:
        sources += f'. "{shell_wrapper}"; '
    subprocess.run(
        ["bash", "-c", f'{sources}gh "$@"', "copilot-gh-test", *arguments],
        env=environment,
        check=True,
    )
    return json.loads(capture_path.read_text(encoding="utf-8"))


def captured_body(capture):
    body_index = capture["args"].index("--body")
    return capture["args"][body_index + 1]


target = run_shell_gh(
    ["pr", "comment", "1", "--repo", "dotnet/msbuild", "--body", "hello"],
    source_twice=True,
)
target_body = captured_body(target)
assert_true(target_body.startswith(prefix), "Target gh body was not prefixed")
assert_true(target_body.count(prefix) == 1, "Sourcing the hook twice duplicated the prefix")

other = run_shell_gh(
    ["pr", "comment", "1", "--repo", "JanProvaznik/msbuild", "--body", "hello"]
)
assert_true(captured_body(other) == "hello", "Another repository gh body was modified")

target_url = run_shell_gh(
    [
        "pr",
        "comment",
        "https://github.com/dotnet/msbuild/pull/1",
        "--body",
        "hello",
    ]
)
assert_true(
    captured_body(target_url).startswith(prefix),
    "Target URL gh body was not prefixed",
)

leading_repo_capture = run_shell_gh(
    ["--repo", "dotnet/msbuild", "issue", "comment", "1", "--body", "hello"]
)
assert_true(
    captured_body(leading_repo_capture).startswith(prefix),
    "Leading --repo gh body was not prefixed",
)

target_api = run_shell_gh(
    [
        "api",
        "--method",
        "POST",
        "repos/dotnet/msbuild/issues/1/comments",
        "-f",
        "body=hello",
    ]
)
field_index = target_api["args"].index("-f")
assert_true(
    target_api["args"][field_index + 1].startswith(f"body={prefix}"),
    "Target gh API body was not prefixed",
)

other_api = run_shell_gh(
    [
        "api",
        "--method",
        "POST",
        "repos/JanProvaznik/msbuild/issues/1/comments",
        "-f",
        "body=hello",
    ]
)
field_index = other_api["args"].index("-f")
assert_true(
    other_api["args"][field_index + 1] == "body=hello",
    "Another repository gh API body was modified",
)

body_file = temporary_root / "body.md"
body_file.write_text("file body", encoding="utf-8")
body_capture = run_shell_gh(["issue", "comment", "1", "--body-file", str(body_file)])
assert_true(
    body_capture["body_file"].startswith(prefix),
    "Target gh body file was not prefixed",
)
assert_true(
    body_file.read_text(encoding="utf-8") == "file body",
    "Original body file was modified",
)
assert_true(
    not Path(body_capture["body_file_path"]).exists(),
    "Temporary body file was not removed",
)

missing_prefix_dir = temporary_root / "missing-prefix"
missing_prefix_dir.mkdir()
copied_wrapper = missing_prefix_dir / python_wrapper.name
copied_wrapper.write_bytes(python_wrapper.read_bytes())
environment = dict(os.environ)
environment["CAPTURE_PATH"] = str(capture_path)
subprocess.run(
    [
        sys.executable,
        str(copied_wrapper),
        str(fake_gh),
        "pr",
        "comment",
        "1",
        "--body",
        "hello",
    ],
    env=environment,
    check=True,
)
fallback = json.loads(capture_path.read_text(encoding="utf-8"))
assert_true(
    fallback["args"] == ["pr", "comment", "1", "--body", "hello"],
    "Wrapper conversion failure did not pass through original arguments",
)
PY

echo "All Unix hook tests passed."
