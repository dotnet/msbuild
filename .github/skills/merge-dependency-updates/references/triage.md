# Read-only bot PR triage

Default to `dotnet/msbuild` for this team's workflow, but resolve an explicitly
requested repository instead. Identify the repository before interpreting PR
numbers; a local fork does not automatically become the operational target.

## When to Use

- You want to see the status of all open bot PRs at a glance
- There are open dependency update / codeflow / OneLoc PRs that need merging
- You want to process all of them in one pass with a clickable summary

## PR Categories

| Category | Author | Title Pattern | Example |
|----------|--------|---------------|---------|
| Dependency update | `dotnet-maestro[bot]` | `[<branch>] Update dependencies from dotnet/<repo>` | `[vs18.3] Update dependencies from dotnet/arcade` |
| Codeflow | `dotnet-maestro[bot]` | `[<branch>] Source code updates from dotnet/dotnet` | `[vs18.3] Source code updates from dotnet/dotnet` |
| OneLoc | `dotnet-bot` | `Localized file check-in by OneLocBuild Task: ...` | `Localized file check-in by OneLocBuild Task: Build definition ID 9434: Build ID ...` |

## Step-by-Step Procedure

### Step 1: Gather All Bot PRs

Query Maestro and dotnet-bot PRs separately. Include `is:pr`; classify the results
by title and author rather than assuming every bot PR is a dependency update.
Independent queries can run in parallel when the host supports it.

1. **Dependency updates + Codeflow:**
   ```
   Search: is:pr is:open author:app/dotnet-maestro repo:dotnet/msbuild
   ```

2. **OneLoc localization:**
   ```
   Search: is:pr is:open author:dotnet-bot repo:dotnet/msbuild
   ```

For a complete inventory, use `gh api` search pagination and compare retrieved
items with `total_count`. GitHub search has a 1,000-result ceiling and can return
`incomplete_results`; partition the query or report incomplete coverage rather
than declaring every PR reviewed. Keep unfamiliar bot PRs visible as unclassified.

### Step 2: Establish each PR's current state

Use PowerShell variables from the selected repository and PR, not literal example
PR numbers. This query captures the head that the evidence belongs to:

```powershell
$query = @'
  query($owner: String!, $repo: String!, $number: Int!) {
    repository(owner: $owner, name: $repo) {
      pullRequest(number: $number) {
        id
        title
        url
        isDraft
        baseRefName
        headRefName
        headRefOid
        headRepository { nameWithOwner }
        mergeable
        mergeStateStatus
        reviewDecision
        commits(last: 1) {
          nodes {
            commit {
              statusCheckRollup {
                state
              }
            }
          }
        }
      }
    }
  }
'@
gh api graphql -f "query=$query" -f "owner=$owner" -f "repo=$repo" -F "number=$number"
if ($LASTEXITCODE -ne 0) { throw "PR state could not be retrieved." }
```

From the response, extract:
- **CI status**: from `statusCheckRollup.state` — `SUCCESS`, `FAILURE`, `PENDING`, `ERROR`, `EXPECTED`, or `null`. Map `ERROR` to failure (❌) and `EXPECTED` to neutral (⚪) in the dashboard.
- **Review decision**: `APPROVED`, `REVIEW_REQUIRED`, `CHANGES_REQUESTED`
- **Approvals**: use `reviewDecision`; do not count a sampled history of review submissions as current distinct approvals
- **Review comments**: paginate `reviewThreads(first: 100, after: $endCursor)` with `pageInfo { hasNextPage endCursor }`, selecting `id`, `isResolved`, and `isOutdated`; retrieve the comments of relevant threads when their content matters
- **Files changed**: paginate the REST `pulls/<number>/files?per_page=100` endpoint and compare the retrieved count to `changed_files` from the PR detail response
- **Suspicious flag**: check if any file paths are outside expected patterns (see below)

With supported `gh` versions, `--paginate --slurp` produces one outer array of
pages. Check `gh api --help` before relying on those flags. The REST PR-files API
has a 3,000-file ceiling: a mismatch is incomplete evidence, not a clean result.
Use a verified local diff or report the limitation for larger changes. Separate
cursors are needed for separate GraphQL connections; one paginated connection
does not make every nested connection complete.

#### Suspicious File Detection

| PR Category | Expected Files | Flag if... |
|-------------|---------------|------------|
| Dependency update (arcade) | `eng/**`, `global.json` | Any file outside `eng/` or `global.json` |
| Dependency update (roslyn/nuget/runtime) | `eng/Version.Details.xml`, `eng/Version.Details.props` | Any file outside `eng/` |
| Codeflow (dotnet/dotnet) | Source and dependency changes in the configured backflow mapping | Changes inconsistent with the actual source mapping or flowed commit range; `src/` changes are normal |
| OneLoc | `**/xlf/*.xlf`, `**/Resources/*.resx` | Any non-localization file, or reviewers flagging reverted translations |

These are review hints, not security boundaries or proof that an update is safe.
For source-flow questions, use the available flow-analysis workflow; for changed
code, use [MSBuild review](../../reviewing-msbuild-code/SKILL.md). For actual failing
checks, route to the available CI investigation workflow rather than duplicating
its investigation here.

### Step 3: Produce the dashboard and stop

For an inventory request, a dashboard grouped by category makes the next action
easy to find. For one selected PR, a short result may be enough.

Format:

```
## 🚦 Dependency Update PRs

| PR | Branch | CI | Review | Files | Suspicious? | Link |
|----|--------|-----|--------|-------|-------------|------|
| #13281 | main ← roslyn | ✅ SUCCESS | ✅ APPROVED | 2 | ✅ Expected files | https://github.com/dotnet/msbuild/pull/13281 |
| #13310 | vs17.10 ← arcade | ❌ FAILURE | 🔶 REVIEW_REQUIRED | 14 | ✅ Clean (eng/ only) | https://github.com/dotnet/msbuild/pull/13310 |

## 🔄 Codeflow PRs

| PR | Branch | CI | Review | Files | Suspicious? | Link |
|----|--------|-----|--------|-------|-------------|------|
| #13258 | vs18.3 ← dotnet/dotnet | ✅ SUCCESS | 🔶 REVIEW_REQUIRED | 1 | ✅ Clean | https://github.com/dotnet/msbuild/pull/13258 |

## 🌐 OneLoc PRs

| PR | Branch | CI | Review | Files | Suspicious? | Link |
|----|--------|-----|--------|-------|-------------|------|
| #13290 | main | ⚪ None | ⛔ CHANGES_REQUESTED | 65 | ⚠️ Reverts translations | https://github.com/dotnet/msbuild/pull/13290 |
```

Status icons:
- CI: ✅ SUCCESS, ❌ FAILURE/ERROR, ⏳ PENDING, ⚪ None/EXPECTED
- Review: ✅ APPROVED, 🔶 REVIEW_REQUIRED, ⛔ CHANGES_REQUESTED
- Suspicious: ✅ Clean, ⚠️ with description

Use only the categories and columns relevant to the request; the example is not
a mandatory three-table report. In actual output, qualify PR references with the
repository when it differs from the current repository. Report incomplete or
unavailable evidence explicitly and include the observed head SHA when readiness
matters. A successful status rollup alone does not prove branch-policy compliance
or merge eligibility.

End with the actionable blockers and next authorized action, not an automatic
update, merge, comment, or retry. A translation-reversion concern needs review of
the actual diff and discussion; a bot author is not grounds to override it.
