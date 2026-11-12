# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

import tempfile
import unittest
import urllib.error
from pathlib import Path
from unittest import mock

import check_agent_workflow_compatibility as checker


PATH = ".github/workflows/review.agent.lock.yml"
SETUP = "        uses: github/gh-aw-actions/setup@v1.0.1\n"
INSTALL = '        run: bash "${RUNNER_TEMP}/gh-aw/actions/install_awf_binary.sh"'
DIGEST = "a" * 64
PINS = f"github/gh-aw-firewall v0.29.0 checksums.txt {DIGEST}"


class AgentWorkflowCompatibilityTests(unittest.TestCase):
    def test_unchanged_setup_reference_skips_unrelated_workflow_changes(self):
        previous = SETUP + "        run: echo before\n"
        current = SETUP + "        run: echo after\n"
        with mock.patch.object(checker, "git", side_effect=[f"M\t{PATH}\n", current, previous]):
            self.assertEqual(checker.updated_workflows("base", "head"), {})

    def test_setup_update_selects_workflow(self):
        previous = SETUP.replace("v1.0.1", "v0.92.3")
        with mock.patch.object(checker, "git", side_effect=[f"M\t{PATH}\n", SETUP, previous]):
            self.assertEqual(checker.updated_workflows("base", "head"), {PATH: SETUP})

    def test_no_updates_makes_no_upstream_requests(self):
        with (
            mock.patch.object(checker, "updated_workflows", return_value={}),
            mock.patch.object(checker, "download_script") as download,
        ):
            self.assertEqual(checker.main(["--base", "base", "--head", "head"]), 0)
            download.assert_not_called()

    def test_detect_only_sets_conditional_output_without_upstream_requests(self):
        for workflows, expected in [({}, "false"), ({PATH: SETUP}, "true")]:
            with self.subTest(expected=expected), tempfile.TemporaryDirectory() as directory:
                output = Path(directory) / "output"
                with (
                    mock.patch.object(checker, "updated_workflows", return_value=workflows),
                    mock.patch.dict(checker.os.environ, {"GITHUB_OUTPUT": str(output)}),
                    mock.patch.object(checker, "download_script") as download,
                ):
                    self.assertEqual(
                        checker.main(["--base", "base", "--head", "head", "--detect-only"]), 0
                    )
                    self.assertEqual(output.read_text(), f"updated={expected}\n")
                    download.assert_not_called()

    def test_supported_firewall_passes(self):
        with mock.patch.object(checker, "release_pins", return_value=PINS):
            checker.check_workflow(SETUP + INSTALL + " v0.29.0 --rootless\n")

    def test_unsupported_firewall_fails(self):
        with mock.patch.object(checker, "release_pins", return_value=PINS):
            with self.assertRaisesRegex(ValueError, "firewall v0.28.59"):
                checker.check_workflow(SETUP + INSTALL + " v0.28.59 --rootless\n")

    def test_duplicate_checksum_entries_fail(self):
        with mock.patch.object(checker, "release_pins", return_value=PINS + "\n" + PINS):
            with self.assertRaisesRegex(ValueError, "no unique reviewed checksum"):
                checker.check_workflow(SETUP + INSTALL + " v0.29.0 --rootless\n")

    def test_literal_checksum_override_allows_unlisted_firewall(self):
        with mock.patch.object(checker, "release_pins") as pins:
            checker.check_workflow(
                SETUP + INSTALL + f" v0.28.59 --checksums-sha256 {DIGEST} --rootless\n"
            )
            pins.assert_not_called()

    def test_invalid_or_dynamic_checksum_override_fails(self):
        for digest in ["bad-digest", '"${CHECKSUM}"', ""]:
            with self.subTest(digest=digest):
                with self.assertRaisesRegex(ValueError, "Invalid literal firewall checksum"):
                    checker.check_workflow(
                        SETUP + INSTALL + f" v0.28.59 --checksums-sha256 {digest}\n"
                    )

    def test_literal_firewall_version_required(self):
        with self.assertRaisesRegex(ValueError, "literal firewall release"):
            checker.check_workflow(SETUP + INSTALL + ' "${VERSION}" --rootless\n')

    def test_legacy_installer_without_release_pins(self):
        checker.release_pins.cache_clear()
        missing = urllib.error.HTTPError("url", 404, "Not Found", None, None)
        with mock.patch.object(checker, "download_script", side_effect=[missing, "legacy installer"]):
            self.assertIsNone(checker.release_pins("legacy"))
        checker.release_pins.cache_clear()


if __name__ == "__main__":
    unittest.main()
