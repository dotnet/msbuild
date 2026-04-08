---
name: "Expert Code Review (command)"
description: "Runs the expert-reviewer agent on a pull request when a contributor comments /review."

on:
  slash_command:
    name: review
    events: [pull_request_comment]

permissions:
  contents: read
  pull-requests: read

imports:
  - shared/review-shared.md

timeout-minutes: 60
---

<!-- Body provided by shared/review-shared.md -->
