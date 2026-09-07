---
name: project-management
description: Query MSBuild issues and project fields, prepare issue drafts, or perform explicitly authorized issue/project updates. Audits and drafts are read-only; not a code review, CI investigation, or release execution workflow.
---

# Issues and project planning

## Before acting

- Establish the repository/project, requested items, and action: query, draft, or mutation. A local fork does not automatically replace `dotnet/msbuild` as the team's target.
- Project 117 in `dotnet` is a discovery hint, not permanent authority. Resolve current project, field, option, iteration, and item identities before writes.
- Prefer `gh`; inspect installed help and discover current-session capabilities before using alternatives. Missing access is a blocker, not an empty result.
- Do not install tools, refresh authentication, post drafts, or edit the board as part of read-only discovery.

## Workflow

1. For one issue, query that issue first. For board-wide work, use [project fields](references/project-fields.md) and complete the relevant pagination.
2. Keep unset, inaccessible, redacted, and incomplete values distinct. The built-in Assignees field synchronizes with issue/PR assignees; discover custom ownership fields separately.
3. For drafting or authorized issue publication, use [issue publication](references/issue-publication.md). Keep drafts local until posting is authorized.
4. Before a board mutation, preview exact item identities and old/new values. Re-read stale state, apply only the authorized changes, and stop on unexpected errors.

## Evidence and stop condition

Report the target, relevant links, query coverage, and requested state or actual
changes. For partial bulk completion, identify completed and blocked items rather
than rerunning everything. Stop when the requested query, draft, or authorized
update is accounted for; no unsolicited comments or project cleanup.
