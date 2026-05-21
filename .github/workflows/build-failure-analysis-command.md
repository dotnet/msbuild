---
name: "Build Failure Analysis (command)"
description: >-
  Rerun the build-failure analysis on a pull request when a maintainer
  comments `/analyze-build-failure`. Same body as `build-failure-analysis.md`
  — re-runs `./build.sh --binaryLog`, captures the binlog, and delegates to
  the `build-failure-analyst` agent. Useful when a previous run was
  cancelled, the analysis comment was dismissed, or the agent needs another
  pass after a force-push.

on:
  slash_command:
    name: analyze-build-failure
    events: [pull_request_comment]
    strategy: centralized
  roles: [admin, maintainer, write]
  reaction: "eyes"

permissions:
  contents: read
  pull-requests: read

concurrency:
  group: build-failure-analysis-${{ github.event.issue.number }}
  cancel-in-progress: true

env:
  BINLOG_MCP_VERSION: '1.0.0-preview.26268.3'

timeout-minutes: 30

network:
  allowed:
    - defaults
    - dotnet

imports:
  - shared/build-failure-analysis-shared.md

# Same deterministic setup as build-failure-analysis.md. The slash-command
# trigger fires on a `pull_request_comment` event; gh-aw handles the PR
# checkout when the comment originates on a PR.
steps:
  - name: Build with binary log
    id: build
    continue-on-error: true
    run: |
      set -o pipefail
      ./build.sh --binaryLog 2>&1 | tee /tmp/build-output.log

  - name: Put dotnet on the path
    if: always()
    run: echo "$PWD/.dotnet" >> $GITHUB_PATH

  - name: Locate binlog
    id: find-binlog
    run: |
      BINLOG=$(find artifacts/log -name '*.binlog' -type f -printf '%T@ %p\n' 2>/dev/null \
        | sort -rn | head -1 | cut -d' ' -f2-)
      if [ -n "$BINLOG" ] && [ -f "$BINLOG" ]; then
        echo "found=true"   >> "$GITHUB_OUTPUT"
        echo "path=$BINLOG" >> "$GITHUB_OUTPUT"
      else
        echo "found=false" >> "$GITHUB_OUTPUT"
      fi

  - name: Install binlog-mcp
    if: steps.build.outcome == 'failure' && steps.find-binlog.outputs.found == 'true'
    run: |
      mkdir -p /tmp/binlog-tool
      cat > /tmp/binlog-tool/nuget.config <<'EOF'
      <?xml version="1.0" encoding="utf-8"?>
      <configuration>
        <packageSources>
          <clear />
          <add key="dotnet-tools"
               value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-tools/nuget/v3/index.json" />
        </packageSources>
      </configuration>
      EOF
      dotnet tool install --global AITools.BinlogMcp \
        --configfile /tmp/binlog-tool/nuget.config \
        --version "$BINLOG_MCP_VERSION"
      echo "$HOME/.dotnet/tools" >> "$GITHUB_PATH"

  - name: Dump binlog as JSON
    if: steps.build.outcome == 'failure' && steps.find-binlog.outputs.found == 'true'
    continue-on-error: true
    env:
      BINLOG_PATH: ${{ steps.find-binlog.outputs.path }}
    run: |
      mkdir -p /tmp/binlog-data
      timeout 180 dotnet run --project .github/workflows/scripts/DumpBinlog -- \
        "$GITHUB_WORKSPACE/$BINLOG_PATH" \
        /tmp/binlog-data

  # `pull_request_comment` events use the `issues` event payload, so
  # `github.sha` is the default branch tip — NOT the PR head. Always resolve
  # the real PR head SHA via the API so permalinks and inline comment
  # placement match the PR.
  - name: Resolve PR head SHA
    id: resolve-pr-sha
    env:
      GH_TOKEN: ${{ github.token }}
      GH_AW_GITHUB_REPOSITORY: ${{ github.repository }}
      GH_AW_GITHUB_EVENT_ISSUE_NUMBER: ${{ github.event.issue.number }}
    run: |
      SHA=$(gh api "repos/${GH_AW_GITHUB_REPOSITORY}/pulls/${GH_AW_GITHUB_EVENT_ISSUE_NUMBER}" --jq .head.sha)
      echo "sha=$SHA" >> "$GITHUB_OUTPUT"

  - name: Export agent context
    env:
      GH_AW_STEPS_BUILD_OUTCOME: ${{ steps.build.outcome }}
      GH_AW_BINLOG_PATH_VALUE: ${{ steps.find-binlog.outputs.path }}
      GH_AW_GITHUB_EVENT_ISSUE_NUMBER: ${{ github.event.issue.number }}
      GH_AW_PR_HEAD_SHA_VALUE: ${{ steps.resolve-pr-sha.outputs.sha || github.sha }}
      GH_AW_GITHUB_WORKSPACE: ${{ github.workspace }}
    run: |
      {
        echo "GH_AW_BUILD_OUTCOME=${GH_AW_STEPS_BUILD_OUTCOME}"
        echo "GH_AW_BINLOG_PATH=${GH_AW_BINLOG_PATH_VALUE}"
        echo "GH_AW_PR_NUMBER=${GH_AW_GITHUB_EVENT_ISSUE_NUMBER}"
        echo "GH_AW_PR_HEAD_SHA=${GH_AW_PR_HEAD_SHA_VALUE}"
        echo "GH_AW_WORKSPACE=${GH_AW_GITHUB_WORKSPACE}"
      } >> "$GITHUB_ENV"

tools:
  github:
    toolsets: [pull_requests, repos]
  bash:
    - "cat"
    - "head"
    - "tail"
    - "grep"
    - "wc"
    - "sort"
    - "uniq"
    - "ls"
    - "find"

safe-outputs:
  add-comment:
    max: 1
    hide-older-comments: true
  create-pull-request-review-comment:
    max: 10
  noop:
    report-as-issue: false
---

<!--
  Body provided by shared/build-failure-analysis-shared.md.
-->
