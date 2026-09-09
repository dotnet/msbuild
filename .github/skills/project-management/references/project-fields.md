# Project field queries and authorized updates

Use the read sections for board questions. The mutation sections require explicit
authorization for the selected project, items, and field changes.

## Where things live

- **Repository default:** `dotnet/msbuild`; honor an explicitly requested alternative.
- **Project discovery hint:** [`dotnet/projects/117`](https://github.com/orgs/dotnet/projects/117), historically titled "MSBuild". Confirm its current owner, title, and fields before treating it as the requested board.
- **Sprints:** discover the iteration field and its current configuration. Titles are display labels, not durable IDs or a reliable way to calculate the current iteration.

Other initiative boards may exist. A user-supplied project URL takes precedence
over this default; do not write to a different board as a fallback.

### Discover project and field identities

```powershell
$query = @'
query($owner: String!, $number: Int!) {
  organization(login: $owner) {
    projectV2(number: $number) {
      id title
      fields(first: 100) {
        pageInfo { hasNextPage endCursor }
        nodes {
          ... on ProjectV2FieldCommon { id name dataType }
          ... on ProjectV2SingleSelectField { options { id name } }
          ... on ProjectV2IterationField {
            configuration {
              iterations { id title startDate duration }
              completedIterations { id title startDate duration }
            }
          }
        }
      }
    }
  }
}
'@
gh api graphql -f "query=$query" -f "owner=$owner" -F "number=$projectNumber"
if ($LASTEXITCODE -ne 0) { throw "Project discovery failed." }
```

Reject null/inaccessible projects and ambiguous field names. Confirm field types
before choosing a mutation. If `hasNextPage` is true, continue the fields
connection with its cursor before claiming a complete field inventory. For a
user-owned board, query `user(login: ...)` rather than `organization(login: ...)`.

## Tooling preference

For GitHub work, prefer `gh` CLI over MCP tools (per repo convention).

- Use `gh issue ...` for issue-level operations (create / view / comment / edit / close).
- Use `gh project ...` for project-board CRUD.
- Use `gh api graphql` for anything `gh` doesn't expose directly (sprint values per item, project-level field values, bulk reads).

Check `gh` help for the installed version. Classic-token project queries need
`read:project` (or `project`); mutations need `project`, and fine-grained/App tokens
need the appropriate equivalent permissions. Report missing access without
automatically logging in or refreshing credentials.

For issue creation, comments, and body edits, use
[issue publication](issue-publication.md) only for that requested action.

## Querying one issue's project membership

`gh issue view` exposes a version-dependent `projectItems` projection, not a
general reader for arbitrary iteration/custom fields. For fields such as Sprint,
use the `repository -> issue -> projectItems` GraphQL path:

```graphql
query($owner: String!, $repo: String!, $number: Int!, $endCursor: String) {
  repository(owner: $owner, name: $repo) {
    issue(number: $number) {
      id
      projectItems(first: 100, after: $endCursor) {
        pageInfo { hasNextPage endCursor }
        nodes {
          id
          project { id number title }
          sprint:  fieldValueByName(name: "Sprint")  { ... on ProjectV2ItemFieldIterationValue   { title iterationId } }
          status:  fieldValueByName(name: "Status")  { ... on ProjectV2ItemFieldSingleSelectValue { name optionId } }
        }
      }
    }
  }
}
```

Pass this query using `gh api graphql` variables as above. Continue the membership
connection if needed and select the verified project ID, not the first returned
item. A null field value may mean unset; establish that the named field exists
before interpreting it. Inaccessible content is unknown, not an unassigned issue.

## Bulk / board-wide queries

`gh project item-list` is useful for bounded reads; its documented default limit
is 30, configurable with `--limit`. A board-wide claim requires complete
pagination or an explicit limitation. Do not enumerate a whole board for a
question about one issue.

This portable Python example uses the already installed `gh` CLI. Set the owner
and project number to the verified target. It queries named fields directly,
preserves repository/content identity, and does not equate missing content with
no assignee:

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
              type
              isArchived
              sprint: fieldValueByName(name: "Sprint") {{
                ... on ProjectV2ItemFieldIterationValue {{ title iterationId }}
              }}
              status: fieldValueByName(name: "Status") {{
                ... on ProjectV2ItemFieldSingleSelectValue {{ name optionId }}
              }}
              content {{
                __typename
                ... on Issue {{
                  id number title url repository {{ nameWithOwner }}
                  assignees(first: 100) {{ nodes {{ login }} pageInfo {{ hasNextPage }} }}
                }}
                ... on PullRequest {{
                  id number title url repository {{ nameWithOwner }}
                  assignees(first: 100) {{ nodes {{ login }} pageInfo {{ hasNextPage }} }}
                }}
                ... on DraftIssue {{
                  id title
                  assignees(first: 100) {{ nodes {{ login }} pageInfo {{ hasNextPage }} }}
                }}
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
        raise RuntimeError(f"Project query failed (exit {e.returncode}); check access.") from e
    response = json.loads(r.stdout)
    if response.get('errors'):
        raise RuntimeError("GraphQL returned errors; board coverage is incomplete.")
    project = ((response.get('data') or {}).get('organization') or {}).get('projectV2')
    if project is None:
        raise RuntimeError("The requested project is unavailable.")
    page = project['items']
    for node in page['nodes']:
        if node is None:
            raise RuntimeError("A project item is unavailable; coverage is incomplete.")
        content = node.get('content')
        assigned = content.get('assignees') if content else None
        known = assigned is not None and not assigned['pageInfo']['hasNextPage']
        results.append({
            'id': node['id'], 'type': node['type'], 'archived': node['isArchived'],
            'sprint': (node.get('sprint') or {}).get('title'),
            'status': (node.get('status') or {}).get('name'),
            'assignees': [u['login'] for u in assigned['nodes']] if known else None,
            'assigneesKnown': known, 'content': content
        })
    if not page['pageInfo']['hasNextPage']:
        break
    next_cursor = page['pageInfo']['endCursor']
    if not next_cursor or next_cursor == cursor:
        raise RuntimeError("Project pagination did not advance.")
    cursor = next_cursor
```

Filter by the requested repository, sprint, archive state, and known assignment
before preparing changes. The built-in Assignees field is synchronized with the
underlying issue/PR, not independent ownership. Discover and query any custom
ownership field separately. Do not mutate draft/redacted items as if they were
ordinary `dotnet/msbuild` issues.

## Updating project-board field values

### Set a sprint (iteration) — first look up iteration IDs

Use the verified iteration field ID. Iteration IDs are not transferable across
projects; resolve the requested iteration from current configuration.

```graphql
query($fieldId: ID!) {
  node(id: $fieldId) {
    ... on ProjectV2IterationField {
      configuration {
        iterations           { id title startDate duration }
        completedIterations  { id title startDate duration }
      }
    }
  }
}
```

Use start dates and durations when selecting "current", with an explicit date/time
zone. Titles alone are not a calendar. Preview the exact old/new values and item
IDs before an authorized bulk update, and re-read them if execution was delayed.

### Single-item field update — `gh project item-edit`

```powershell
# Iteration / sprint
gh project item-edit --project-id $projectId --id $itemId --field-id $fieldId --iteration-id $iterationId
if ($LASTEXITCODE -ne 0) { throw "Item update failed; reconcile before continuing." }

# Single-select (Status, Area, Priority): use --single-select-option-id
# Text: --text  | Number: --number  | Date: --date YYYY-MM-DD
```

### Single-item field update — GraphQL mutation (works the same)

```graphql
mutation($project: ID!, $item: ID!, $field: ID!, $iteration: String!) {
  updateProjectV2ItemFieldValue(input: {
    projectId: $project,
    itemId:    $item,
    fieldId:   $field,
    value:     { iterationId: $iteration }
  }) { projectV2Item { id } }
}
```

### Adding a freshly created issue to the selected project

A newly-created issue is not always auto-added. To add and set its sprint:

```graphql
mutation($project: ID!, $content: ID!) {
  addProjectV2ItemById(input: { projectId: $project, contentId: $content }) {
    item { id }
  }
}
```

Check the mutation's exit status and returned item ID before issuing a separate
field update. Adding an already present item returns its existing item ID; do not
assume all other mutations are similarly idempotent. After updates, read back the
requested values. On a partial bulk failure, report exactly which items changed;
do not blindly repeat the entire batch.

## Common errors and gotchas

### 1. `Field 'fieldValueByName' has an argument conflict`

Calling `fieldValueByName` with different arguments in the same selection set
requires aliases. This is a GraphQL rule, independent of `gh --paginate`:

```
Field 'fieldValueByName' has an argument conflict: {name:"Sprint"} or {name:"Status"}?
```

Fix: alias each call.

```graphql
sprint: fieldValueByName(name: "Sprint")  { ... }
status: fieldValueByName(name: "Status")  { ... }
```

### 2. `gh api graphql --paginate` produces concatenated JSON, not one document

Each page is a separate JSON value. `jq` accepts such a stream; a parser expecting
one document may not. Use supported `gh api --paginate --slurp` to obtain one
outer array, or consume pages individually. Investigate malformed output or
diagnostics mixed into stdout rather than blaming valid JSON streaming.

### 3. "Selections can't be made directly on unions"

`field` on a project field value is a union; you can't select `name` directly. Use a typed inline fragment per concrete field type (`ProjectV2Field`, `ProjectV2IterationField`, `ProjectV2SingleSelectField`, …).

### 4. Built-in versus custom ownership

The built-in Assignees field and issue/PR assignees synchronize both ways. A
custom team ownership field can mean something different; discover its name and
type rather than assuming the built-in field is independent.

### 5. `null` entries in `fieldValues.nodes`

Connections may contain nullable entries, and unset fields need not appear as
one entry per field. Guard nullable entries and verify field existence; do not
interpret a truncated or inaccessible connection as a complete set of unset
values. Named field queries avoid relying on positional field enumeration.

### 6. DraftIssue items lack `number` / `repository`

`content` may be a `DraftIssue`, which has a node ID and draft fields but no issue
number/repository identity. Preserve its type instead of joining it to ordinary
issue numbers. Redacted content must remain unknown.

### 7. Bounded reads versus complete inventories

`gh project item-list --help` documents its default and configurable limits.
Use a bounded read when it answers the question; paginate and check completeness
before making board-wide claims or bulk selections.

### 8. Missing `project` scope on `gh` token

Read access can also be missing. Report the required capability without printing
tokens, automatically refreshing authentication, or treating inaccessible data
as an empty board.

### 9. `gh issue edit --body` overwrites — there is no append

To "append" to a body, fetch with `gh issue view --json body`, append, write back with `--body-file`. For status updates, prefer a comment.

## Common workflows

| Goal | Sketch |
|---|---|
| Add a status comment to selected issues | Preview each target and draft; follow [issue publication](issue-publication.md) only with posting authority |
| Replace an issue body | Preserve the current body, review the intended replacement, and re-read before overwriting |
| Move all unassigned items from sprint X to sprint Y | GraphQL pagination loop above + `gh project item-edit --iteration-id` per item |
| File a new issue and put it in the current sprint | `gh issue create` → `addProjectV2ItemById` → `updateProjectV2ItemFieldValue` |
| Audit "what's in the current sprint that has no owner" | GraphQL pagination loop, filter on `Sprint == "<current>"` and empty `Assignees` |

For the last workflow, require `assigneesKnown` as well as an empty assignee list.

## Authoritative references

- [GitHub Projects API](https://docs.github.com/en/issues/planning-and-tracking-with-projects/automating-your-project/using-the-api-to-manage-projects)
- [Projects synchronization](https://docs.github.com/en/issues/planning-and-tracking-with-projects/learning-about-projects/about-projects)
- [GitHub CLI API pagination](https://cli.github.com/manual/gh_api)
