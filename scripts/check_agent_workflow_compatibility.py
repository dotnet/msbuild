#!/usr/bin/env python3
# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

import argparse
import functools
import os
import re
import shlex
import subprocess
import sys
import urllib.error
import urllib.parse
import urllib.request


SETUP_REFERENCE = re.compile(
    r"""^\s*(?:-\s*)?uses:\s*['"]?github/gh-aw-actions/setup@([A-Za-z0-9._/-]+)""",
    re.MULTILINE,
)
FIREWALL_INSTALL = re.compile(
    r"""install_awf_binary\.sh['"]?\s+['"]?(v?\d+\.\d+\.\d+)['"]?(?=\s|$)(.*)"""
)


def git(*arguments):
    return subprocess.check_output(
        ["git", *arguments], text=True, encoding="utf-8"
    )


def updated_workflows(base, head):
    changes = git(
        "diff", "--name-status", "--no-renames", "--diff-filter=AM", base, head, "--",
        ".github/workflows/*.agent.lock.yml",
    )
    workflows = {}
    for change in changes.splitlines():
        status, path = change.split("\t", 1)
        current = git("show", f"{head}:{path}")
        previous = "" if status == "A" else git("show", f"{base}:{path}")
        references = SETUP_REFERENCE.findall(current)
        if references and references != SETUP_REFERENCE.findall(previous):
            workflows[path] = current
    return workflows


def download_script(reference, name):
    reference = urllib.parse.quote(reference, safe="")
    url = (
        f"https://raw.githubusercontent.com/github/gh-aw-actions/{reference}"
        f"/setup/sh/{name}"
    )
    with urllib.request.urlopen(url, timeout=30) as response:
        return response.read().decode("utf-8")


@functools.cache
def release_pins(reference):
    try:
        integrity = download_script(reference, "release_integrity.sh")
    except urllib.error.HTTPError as error:
        if error.code != 404:
            raise
        installer = download_script(reference, "install_awf_binary.sh")
        if "release_artifact_sha256" in installer:
            raise ValueError(f"setup@{reference} is missing required release integrity metadata")
        return None

    pins = re.search(r"<<'PINS'\r?\n(.*?)\r?\nPINS\b", integrity, re.DOTALL)
    if pins is None:
        raise ValueError(f"Unrecognized release integrity metadata in setup@{reference}")
    return pins.group(1)


def check_workflow(text):
    references = set(SETUP_REFERENCE.findall(text))
    if len(references) != 1:
        raise ValueError("Expected one consistent gh-aw setup reference in the workflow")
    reference = references.pop()
    text = re.sub(r"\\\r?\n\s*", " ", text)

    for line in text.splitlines():
        if "install_awf_binary.sh" not in line or line.lstrip().startswith("#"):
            continue
        install = FIREWALL_INSTALL.search(line)
        if install is None:
            raise ValueError("Expected a literal firewall release version")
        version = "v" + install.group(1).removeprefix("v")
        arguments = shlex.split(install.group(2))
        if "--checksums-sha256" in arguments:
            index = arguments.index("--checksums-sha256")
            if (
                arguments.count("--checksums-sha256") != 1
                or index + 1 == len(arguments)
                or re.fullmatch(r"[0-9a-f]{64}", arguments[index + 1]) is None
            ):
                raise ValueError(f"Invalid literal firewall checksum override for {version}")
            continue

        pins = release_pins(reference)
        if pins is None:
            continue
        matches = re.findall(
            rf"^github/gh-aw-firewall[ \t]+{re.escape(version)}[ \t]+"
            r"checksums\.txt[ \t]+([0-9a-f]{64})[ \t]*$",
            pins,
            re.MULTILINE,
        )
        if len(matches) != 1:
            raise ValueError(
                f"setup@{reference} has no unique reviewed checksum for firewall {version}. "
                "Regenerate the workflow with a compatible gh-aw compiler/runtime."
            )


def main(arguments=None):
    parser = argparse.ArgumentParser(
        description="Check firewall checksum compatibility when gh-aw setup references change."
    )
    parser.add_argument("--base", required=True)
    parser.add_argument("--head", required=True)
    parser.add_argument("--detect-only", action="store_true")
    args = parser.parse_args(arguments)

    try:
        workflows = updated_workflows(args.base, args.head)
        if args.detect_only:
            output = os.environ.get("GITHUB_OUTPUT")
            if output:
                with open(output, "a", encoding="utf-8") as file:
                    file.write(f"updated={str(bool(workflows)).lower()}\n")
            print(f"Found {len(workflows)} workflow(s) with gh-aw setup reference updates.")
            return 0
        if not workflows:
            print("No gh-aw setup reference updates; compatibility check skipped.")
            return 0

        failed = False
        for path, text in workflows.items():
            try:
                check_workflow(text)
                print(f"PASS: {path}")
            except ValueError as error:
                print(f"FAIL: {path}: {error}", file=sys.stderr)
                failed = True
        return int(failed)
    except (subprocess.CalledProcessError, urllib.error.URLError, ValueError) as error:
        print(f"Compatibility check failed: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
