---
name: deploy-msbuild-to-vs
description: Deploy or restore locally built MSBuild in a specifically selected Visual Studio installation, or configure VS-hosted debugging. Not needed for ordinary CLI testing; debugging alone does not authorize replacing installed binaries.
---

# Local Visual Studio deployment and debugging

## Before acting

- Select the requested mode: inspect/debug, deploy, or restore. Start read-only if unclear.
- Identify the Windows VS instance and desired scenario. Use [bootstrap](../use-bootstrap-msbuild/SKILL.md) instead for ordinary command-line testing.
- Read [Deploy-MSBuild.ps1](../../../scripts/Deploy-MSBuild.ps1) for current inputs, source paths, runtimes, and backup behavior. Do not infer compatibility from a remembered SDK version.
- Discover available tools and inspect version-specific help. Do not install tools, elevate, or terminate processes as a side effect of discovery.

## Workflow

1. For attachment or logging only, follow [debugging](references/debugging.md); do not rebuild or deploy unless needed for the requested scenario.
2. For deployment, follow [deployment preflight](references/deployment.md). Confirm the exact target, source revision/configuration, runtime compatibility, authorization, and independent rollback snapshot before writing.
3. Close only the selected instance and relevant build processes before copying. Any necessary termination must target identified, authorized PIDs.
4. For restoration, use the recorded snapshot and file inventory. The script's backup is not a full rollback of newly introduced files.

## Evidence and stop condition

Record the selected instance and loaded binary paths/version, then exercise the
requested scenario. Stop when that outcome is established, or report the precise
missing artifact, permission, compatibility fact, or recovery limitation. Do not
repair or patch another installation as an automatic fallback.
