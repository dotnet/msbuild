---
name: "Expert Code Review (on open)"
description: "Automatically runs the expert-reviewer agent when a contributor opens a pull request."

on:
  pull_request:
    types: [opened]

permissions:
  contents: read
  pull-requests: read

imports:
  - shared/review-shared.md

timeout-minutes: 60
---

<!-- Body provided by shared/review-shared.md -->
