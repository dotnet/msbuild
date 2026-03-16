---
name: deep-reviewing-msbuild-code
description: "Performs thorough, expert-level code review of MSBuild changes by invoking the @rainersigwald-reviewer agent persona. Activates when the user requests deep review, thorough review, comprehensive review, expert review, detailed code analysis, or in-depth quality assessment of MSBuild pull requests, diffs, commits, or code changes. Covers backwards compatibility, ChangeWave discipline, performance, test coverage, error messages, API surface, concurrency, security, evaluation model integrity, and 15 additional dimensions."
---

# Deep MSBuild Code Review

This skill performs comprehensive code review using the `@rainersigwald-reviewer` agent.

## Usage

Invoke the reviewer agent for thorough, multi-dimensional code review:

```
@rainersigwald-reviewer Review this PR for MSBuild compliance across all 24 dimensions.
```

The agent applies 24 review dimensions derived from 10,081 review comments by @rainersigwald (2016–2026), covering:

- **BLOCKING**: Backwards compatibility, ChangeWave discipline, concurrency, security, evaluation model integrity
- **MAJOR**: Performance, test coverage, error messages, API surface, correctness, cross-platform, SDK integration
- **MODERATE**: Logging, documentation, build infrastructure, scope discipline, dependency management
- **NIT**: Naming precision, idiomatic C#, code simplification

The agent prioritizes dimensions based on which files are changed (folder hotspot mapping) and categorizes all findings by severity.

## When to Use

- Pull request reviews requiring MSBuild domain expertise
- Architecture and design reviews for MSBuild changes
- Pre-merge quality gates for backwards compatibility and ChangeWave compliance
- Code quality assessment against MSBuild team conventions
