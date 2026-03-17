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

1. **5 parallel Opus 4.6 sub-agents** evaluate dimensions in batches. Each returns `$DimensionName — LGTM` for clean dimensions or verified findings with file:line.
2. **3-model verification** (Opus, Codex, Gemini) validates non-LGTM findings by tracing code flow. Findings kept only with ≥2/3 consensus.
3. **Inline PR comments** posted at exact file:line via GitHub CLI for each confirmed finding.
4. **Summary table** with 24-dimension checkbox list posted as review body. All `[x]` → APPROVE.

## When to Use

- Pull request reviews requiring MSBuild domain expertise
- Pre-merge quality gates for backwards compatibility and ChangeWave compliance
- Code quality assessment against MSBuild team conventions
