---
name: deep-reviewing-msbuild-code
description: "Performs thorough, expert-level code review of MSBuild changes by invoking the @expert-reviewer agent. Activates when the user requests deep review, thorough review, comprehensive review, expert review, detailed code analysis, or in-depth quality assessment of MSBuild pull requests, diffs, commits, or code changes. Covers backwards compatibility, ChangeWave discipline, performance, test coverage, error messages, API surface, concurrency, security, evaluation model integrity, and 15 additional dimensions."
---

# Deep MSBuild Code Review

This skill performs comprehensive code review using the `@expert-reviewer` agent.

## Usage

```
@expert-reviewer Review this PR for MSBuild compliance across all 24 dimensions.
```

## Pipeline

1. **Wave 1 — Find**: 5 parallel Opus 4.6 sub-agents evaluate dimensions. LGTM for clean dimensions. Issues require concrete failing scenarios (thread interleavings, null inputs, specific call sequences).
2. **Wave 2 — Validate**: Actively prove each finding by tracing code flow in the PR branch, writing proof-of-concept tests, simulating thread timelines. Multi-model vote (Opus+Codex+Gemini) for borderline cases. Only confirmed findings survive.
3. **Wave 3 — Post**: Inline comments at file:line via GitHub CLI with severity, scenario, and test snippet as proof. Design concerns as a single PR comment.
4. **Wave 4 — Summary**: 24-dimension checkbox table. All `[x]` → APPROVE. BLOCKING `[ ]` → REQUEST_CHANGES.

## When to Use

- Pull request reviews requiring MSBuild domain expertise
- Pre-merge quality gates for backwards compatibility and ChangeWave compliance
- Code quality assessment against MSBuild team conventions
