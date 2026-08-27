---
name: project-management
description: 'GitHub issue and project-board management for the dotnet/msbuild repo. Use when asked to file/triage/update issues, post comments, amend issue bodies, move sprints, bulk-update project board fields, or audit items by sprint/status/assignee.'
argument-hint: 'Manage issues and project board entries for dotnet/msbuild.'
---

# Project Management for dotnet/msbuild

This skill covers everything you need to manage issues and the project board for the MSBuild repository: querying, commenting, editing, creating issues, and bulk operations on the project board (sprints, status, assignees, etc.).

## Where things live

- **Repository:** `dotnet/msbuild` — all issues, PRs, and discussions.
- **Project board:** [`dotnet/projects/117`](https://github.com/orgs/dotnet/projects/117) — title "MSBuild". This is the canonical project board for the team. Most issues created in `dotnet/msbuild` are auto-added or manually added here for sprint planning.
- **Sprints** are tracked via the **Sprint** iteration field on project 117. Iteration titles use the format `2026 May I`, `2026 May II`, `2026 June I`, etc. (roughly two-week iterations).

> Note: there are/were other auxiliary project boards for specific initiatives. Use project 117 unless the user explicitly points at another one.

### Stable identifiers for project 117

| Identifier | Value |
|---|---|
| Project node ID | `PVT_kwDOAIt-yc4ABM5F` |
| Sprint field ID (iteration) | `PVTIF_lADOAIt-yc4ABM5FzgAtMGI` |

These IDs are stable but can be re-derived if the project is ever recreated or migrated:
```bash
# Project node ID
gh api graphql -f query='{ organization(login:"dotnet") { projectV2(number:117) { id } } }'

# All field IDs (Sprint, Status, Area, Priority, …)
gh project field-list 117 --owner dotnet --format json | jq '.fields[] | {id,name,type}'
```

## Tooling preference

For GitHub work, prefer `gh` CLI over MCP tools (per repo convention).

- Use `gh issue ...` for issue-level operations (create / view / comment / edit / close).
- Use `gh project ...` for project-board CRUD.
- Use `gh api graphql` for anything `gh` doesn't expose directly (sprint values per item, project-level field values, bulk reads).

The default `gh` token may not include the `project` scope. If a `gh project ...` write call fails with a scope error, ask the user to run:
```bash
gh auth refresh -s project
```

## Common single-issue actions

```bash
# View
gh issue view 13315 --repo dotnet/msbuild --json title,body,state,assignees,labels

# Comment (use --body-file to avoid shell-quoting traps with multi-line markdown)
gh issue comment 13315 --repo dotnet/msbuild --body-file ./comment.md

# Amend body (overwrite — there is no append; use view → edit → write)
gh issue edit 13597 --repo dotnet/msbuild --body-file ./new-body.md

# Create
gh issue create --repo dotnet/msbuild --title "Title" --body-file ./body.md

# Add labels / assignees
gh issue edit 13315 --repo dotnet/msbuild --add-label "Area: Server" --add-assignee someuser
```

When drafting comments / bodies that summarize internal discussions:
- Use neutral, team-voice language ("the team agreed", "open question").
- Scrub names, "X said Y", room/agent identifiers, and anything resembling internal politics.
- Always write to a file and use `--body-file`. Don't try to inline multi-line markdown.

## Querying issue → project field values (sprint, status, project-level assignees)

`gh issue view` does **not** return project-board field values. Use GraphQL via the `repository → issue → projectItems` path:

```bash
gh api graphql -f query='
query {
  r: repository(owner:"dotnet", name:"msbuild") {
    issue(number: 13315) {
      id
      projectItems(first: 5) {
        nodes {
          id
          project { number title }
          sprint:  fieldValueByName(name: "Sprint")  { ... on ProjectV2ItemFieldIterationValue   { title iterationId } }
          status:  fieldValueByName(name: "Status")  { ... on ProjectV2ItemFieldSingleSelectValue { name optionId } }
        }
      }
    }
  }
}'
```

> `fieldValueByName` returns `null` when the field has no value set on that item — handle accordingly.

## Bulk / board-wide queries

The `gh project item-list` CLI command has internal limits and is unreliable for large boards (project 117 is in the thousands of items). **Use GraphQL with pagination.**

```python
import subprocess, json

PROJECT_NUMBER = 117
ORG = "dotnet"

results = []
cursor = None
while True:
    after = f', after: "{cursor}"' if cursor else ''
    query = f'''query {{
      organization(login: "{ORG}") {{
        projectV2(number: {PROJECT_NUMBER}) {{
          items(first: 100{after}) {{
            pageInfo {{ hasNextPage endCursor }}
            nodes {{
              id
              fieldValues(first: 20) {{
                nodes {{
                  ... on ProjectV2ItemFieldIterationValue {{
                    field {{ ... on ProjectV2IterationField  {{ name }} }}
                    title
                  }}
                  ... on ProjectV2ItemFieldUserValue {{
                    field {{ ... on ProjectV2Field           {{ name }} }}
                    users(first: 10) {{ nodes {{ login }} }}
                  }}
                  ... on ProjectV2ItemFieldSingleSelectValue {{
                    field {{ ... on ProjectV2SingleSelectField {{ name }} }}
                    name
                  }}
                }}
              }}
              content {{
                ... on Issue        {{ number title }}
                ... on PullRequest  {{ number title }}
                ... on DraftIssue   {{ title }}
              }}
            }}
          }}
        }}
      }}
    }}'''
    try:
        r = subprocess.run(['gh','api','graphql','-f',f'query={query}'],
                           capture_output=True, text=True, check=True)
    except subprocess.CalledProcessError as e:
        raise RuntimeError(f"GraphQL query failed: {e.stderr}") from e
    page = json.loads(r.stdout)['data']['organization']['projectV2']['items']
    for node in page['nodes']:
        sprint = status = None
        assignees = []
        for fv in node.get('fieldValues', {}).get('nodes', []):
            if not fv:                              # null entries for unset fields
                continue
            fname = (fv.get('field') or {}).get('name', '')
            if fname == 'Sprint':
                sprint = fv.get('title')
            elif fname == 'Status':
                status = fv.get('name')
            elif fname == 'Assignees':
                assignees = [u['login'] for u in (fv.get('users') or {}).get('nodes', [])]
        results.append({'id': node['id'], 'sprint': sprint, 'status': status,
                        'assignees': assignees, 'content': node.get('content')})
    if not page['pageInfo']['hasNextPage']:
        break
    cursor = page['pageInfo']['endCursor']
```

## Updating project-board field values

### Set a sprint (iteration) — first look up iteration IDs

Iteration IDs are short hex strings (e.g., `303c2425`). They are **not** the same across projects; always look them up from project 117.

```bash
gh api graphql -f query='
query {
  node(id: "PVTIF_lADOAIt-yc4ABM5FzgAtMGI") {
    ... on ProjectV2IterationField {
      configuration {
        iterations           { id title startDate }
        completedIterations  { id title startDate }
      }
    }
  }
}' | jq '.data.node.configuration | (.iterations + .completedIterations)[] | {id,title}'
```

### Single-item field update — `gh project item-edit`

```bash
# Iteration / sprint
gh project item-edit \
  --project-id PVT_kwDOAIt-yc4ABM5F \
  --id <PROJECT_ITEM_ID> \
  --field-id PVTIF_lADOAIt-yc4ABM5FzgAtMGI \
  --iteration-id <ITERATION_ID>

# Single-select (Status, Area, Priority): use --single-select-option-id
# Text: --text  | Number: --number  | Date: --date YYYY-MM-DD
```

### Single-item field update — GraphQL mutation (works the same)

```bash
gh api graphql -f query='
mutation {
  updateProjectV2ItemFieldValue(input: {
    projectId: "PVT_kwDOAIt-yc4ABM5F",
    itemId:    "<PROJECT_ITEM_ID>",
    fieldId:   "PVTIF_lADOAIt-yc4ABM5FzgAtMGI",
    value:     { iterationId: "<ITERATION_ID>" }
  }) { projectV2Item { id } }
}'
```

### Adding a freshly created issue to project 117

A newly-created issue is not always auto-added. To add and set its sprint:

```bash
ISSUE_NODE_ID=$(gh api graphql -f query='
  query { repository(owner:"dotnet",name:"msbuild") { issue(number:13707) { id } } }' \
  | jq -r '.data.repository.issue.id')

ITEM_ID=$(gh api graphql -f query='
  mutation { addProjectV2ItemById(input:{
      projectId:"PVT_kwDOAIt-yc4ABM5F",
      contentId:"'"$ISSUE_NODE_ID"'"
  }) { item { id } } }' | jq -r '.data.addProjectV2ItemById.item.id')

gh api graphql -f query='
  mutation { updateProjectV2ItemFieldValue(input:{
      projectId:"PVT_kwDOAIt-yc4ABM5F",
      itemId:"'"$ITEM_ID"'",
      fieldId:"PVTIF_lADOAIt-yc4ABM5FzgAtMGI",
      value:{iterationId:"<ITERATION_ID>"}
  }) { projectV2Item { id } } }'
```

## Common errors and gotchas

### 1. `Field 'fieldValueByName' has an argument conflict`

`gh api graphql --paginate` (and several jq-style aliases) re-uses field names. If you call `fieldValueByName(name:"Sprint")` and `fieldValueByName(name:"Status")` in the same selection set without aliases, you'll get:

```
Field 'fieldValueByName' has an argument conflict: {name:"Sprint"} or {name:"Status"}?
```

Fix: alias each call.

```graphql
sprint: fieldValueByName(name: "Sprint")  { ... }
status: fieldValueByName(name: "Status")  { ... }
```

### 2. `gh api graphql --paginate` produces concatenated JSON, not one document

Each page is emitted as a separate JSON object on stdout; piping straight into `jq` fails with "Invalid numeric literal". Either:
- paginate manually in a loop (preferred for large reads), or
- use `jq --slurp` to read the stream as an array.

### 3. "Selections can't be made directly on unions"

`field` on a project field value is a union; you can't select `name` directly. Use a typed inline fragment per concrete field type (`ProjectV2Field`, `ProjectV2IterationField`, `ProjectV2SingleSelectField`, …).

### 4. Issue-level assignees ≠ project-board "Assignees" column

The board's "No Assignees" slice is driven by the **project-level Assignees field**, not by `Issue.assignees`. They can diverge. When filtering by board state, always read `ProjectV2ItemFieldUserValue` from `fieldValues`.

### 5. `null` entries in `fieldValues.nodes`

GraphQL returns `null` for items where a particular field is unset. Always guard `if not fv: continue` before dereferencing.

### 6. DraftIssue items lack `number` / `repository`

`content` may be a `DraftIssue` that only exposes `title`, `body`, `assignees`. Code that joins to issue numbers must handle `None`.

### 7. `gh project item-list` is unreliable for large boards

It silently truncates. Always use the GraphQL pagination loop above instead.

### 8. Missing `project` scope on `gh` token

Reads work; writes (`gh project item-edit`, the `addProjectV2ItemById` / `updateProjectV2ItemFieldValue` mutations) fail. Have the user run `gh auth refresh -s project`.

### 9. `gh issue edit --body` overwrites — there is no append

To "append" to a body, fetch with `gh issue view --json body`, append, write back with `--body-file`. For status updates, prefer a comment.

## Sanitization checklist for comments / body edits

When summarizing meetings or chat threads into GitHub:

- ✅ Use team voice ("the team agreed", "open question", "follow-up needed").
- ✅ Keep objective, decision-relevant content (problem statement, scope, exit criteria, repro steps, numbers).
- ❌ No personal names or "X said / Y replied".
- ❌ No full email addresses of external contributors (use GitHub handles instead).
- ❌ No internal-only identifiers (room names, recording IDs, agent fleet identifiers, partner-team contractor names).
- ❌ No speculation about other teams' motives or anything resembling internal politics.
- ❌ No confidential roadmap dates or unreleased product details unless already public.
- Always show the user the proposed comment / body before posting (unless they've explicitly preapproved).

## Common workflows

| Goal | Sketch |
|---|---|
| Add a status comment to N issues | `for n in 13315 13702 …; do gh issue comment $n --repo dotnet/msbuild --body-file ./c-$n.md; done` |
| Replace a one-line issue body with a full Context / Exit-criteria / Out-of-scope spec | `gh issue edit <n> --repo dotnet/msbuild --body-file ./body.md` |
| Move all unassigned items from sprint X to sprint Y | GraphQL pagination loop above + `gh project item-edit --iteration-id` per item |
| File a new issue and put it in the current sprint | `gh issue create` → `addProjectV2ItemById` → `updateProjectV2ItemFieldValue` |
| Audit "what's in the current sprint that has no owner" | GraphQL pagination loop, filter on `Sprint == "<current>"` and empty `Assignees` |
