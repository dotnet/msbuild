#!/usr/bin/env python3

from __future__ import annotations

import json
import re
import shlex
import sys
from pathlib import Path
from typing import Any


TARGET_OWNER = "dotnet"
TARGET_REPOSITORY = "msbuild"
TARGET_SLUG = f"{TARGET_OWNER}/{TARGET_REPOSITORY}"

MCP_COMMENT_TOOL = re.compile(
    r"(?:^|[-_])(?:"
    r"add_issue_comment|"
    r"add_reply_to_pull_request_comment|"
    r"add_comment_to_pending_review|"
    r"add_pull_request_review_comment|"
    r"create_pull_request_review|"
    r"submit_pending_pull_request_review|"
    r"pull_request_review_write|"
    r"discussion_comment_write"
    r")$"
)
GH_COMMAND_PREFIX = (
    r"(?<![\w.-])gh"
    r"(?:\s+(?:(?:-R|--repo|--hostname)(?:=|\s+)\S+))*"
)
GH_COMMENT_COMMAND = re.compile(
    rf"(?is){GH_COMMAND_PREFIX}\s+(?:pr|issue)\s+comment\b"
)
GH_REVIEW_COMMAND = re.compile(rf"(?is){GH_COMMAND_PREFIX}\s+pr\s+review\b")
GH_REVIEW_BODY = re.compile(
    r"(?is)(?:--comment|--request-changes|--approve|--body(?:-file)?\b|-b\b|-F\b)"
)
GH_API_COMMAND = re.compile(rf"(?is){GH_COMMAND_PREFIX}\s+api\b")
GH_COMMENT_ENDPOINT = re.compile(
    r"(?is)(?:"
    r"/issues/\d+/comments|"
    r"/issues/comments/\d+|"
    r"/pulls/\d+/(?:comments|reviews)|"
    r"/pulls/comments/\d+|"
    r"/comments/\d+/replies|"
    r"/reviews/\d+/comments"
    r")"
)
GH_API_WRITE = re.compile(
    r"(?is)(?:(?:-X|--method)\s*(?:POST|PUT|PATCH)\b|"
    r"(?:-f|--raw-field|-F|--field|--input)\b)"
)
GH_GRAPHQL_COMMENT = re.compile(
    rf"(?is){GH_COMMAND_PREFIX}\s+api\s+graphql\b.*"
    r"\b(?:addComment|addPullRequestReview|submitPullRequestReview|"
    r"addPullRequestReviewComment)\b"
)


def emit(value: dict[str, Any]) -> None:
    json.dump(value, sys.stdout, ensure_ascii=False, separators=(",", ":"))
    sys.stdout.write("\n")


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


def normalized_repository(owner: Any, repository: Any) -> str | None:
    if not isinstance(repository, str):
        return None

    repository = repository.strip().strip("/")
    if "/" in repository:
        if repository.casefold().endswith(".git"):
            repository = repository[:-4]
        return repository.casefold()
    if not isinstance(owner, str) or not owner.strip():
        return None
    return f"{owner.strip()}/{repository}".casefold()


def targets_msbuild(tool_args: dict[str, Any]) -> bool:
    repository = normalized_repository(tool_args.get("owner"), tool_args.get("repo"))
    if repository is None:
        repository = normalized_repository(
            tool_args.get("owner"), tool_args.get("repository")
        )
    return repository == TARGET_SLUG


def is_gh_comment_write(command: str) -> bool:
    if GH_COMMENT_COMMAND.search(command):
        return True
    if GH_REVIEW_COMMAND.search(command) and GH_REVIEW_BODY.search(command):
        return True
    return bool(
        GH_API_COMMAND.search(command)
        and GH_COMMENT_ENDPOINT.search(command)
        and GH_API_WRITE.search(command)
    )


def rewrite_mcp_args(tool_args: dict[str, Any]) -> bool:
    changed = False

    body = tool_args.get("body")
    if isinstance(body, str):
        prefixed_body = add_prefix(body)
        if prefixed_body != body:
            tool_args["body"] = prefixed_body
            changed = True

    comments = tool_args.get("comments")
    if isinstance(comments, list):
        for comment in comments:
            if not isinstance(comment, dict):
                continue
            comment_body = comment.get("body")
            if not isinstance(comment_body, str):
                continue
            prefixed_body = add_prefix(comment_body)
            if prefixed_body != comment_body:
                comment["body"] = prefixed_body
                changed = True

    return changed


def main() -> None:
    hook_input = json.load(sys.stdin)
    tool_name = str(hook_input.get("toolName", ""))
    tool_args = hook_input.get("toolArgs")

    if isinstance(tool_args, str):
        tool_args = json.loads(tool_args)
    if not isinstance(tool_args, dict):
        emit({})
        return

    if MCP_COMMENT_TOOL.search(tool_name):
        if not targets_msbuild(tool_args):
            emit({})
            return
        emit({"modifiedArgs": tool_args} if rewrite_mcp_args(tool_args) else {})
        return

    if tool_name != "bash":
        emit({})
        return

    command = tool_args.get("command")
    if not isinstance(command, str) or not is_gh_comment_write(command):
        emit({})
        return

    if GH_GRAPHQL_COMMENT.search(command):
        emit({})
        return

    wrapper_path = Path(__file__).with_name("gh-comment-wrapper.sh")
    if not wrapper_path.is_file():
        emit({})
        return

    tool_args["command"] = f". {shlex.quote(str(wrapper_path))}; {command}"
    emit({"modifiedArgs": tool_args})


if __name__ == "__main__":
    try:
        main()
    except Exception:
        emit({})
