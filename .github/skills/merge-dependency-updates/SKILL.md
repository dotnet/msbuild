---
name: merge-dependency-updates
description: 'Review and merge open bot PRs: dependency updates from dotnet-maestro, codeflow from dotnet/dotnet, and OneLoc localization PRs. Produces a clickable dashboard with CI status, review state, and suspicious file flags. Use when you want to triage all open bot PRs in one pass.'
argument-hint: 'Triage, update, and version-bump open bot PRs.'
---

# Managing Bot PRs (Dependencies, Codeflow, OneLoc)

This skill triages all open bot PRs in the MSBuild repo, produces a review dashboard, then optionally updates branches and bumps versions for merge readiness.

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

Run **two searches in parallel** to find all open bot PRs:

1. **Dependency updates + Codeflow:**
   ```
   Search: is:open author:app/dotnet-maestro repo:dotnet/msbuild
   ```

2. **OneLoc localization:**
   ```
   Search: is:open author:dotnet-bot repo:dotnet/msbuild ("OneLocBuild" OR "Localized file check-in")
   ```

### Step 2: Analyze Each PR (in parallel)

For each PR, query the following data using `gh api graphql`. Process PRs in parallel batches for speed.

```bash
gh api graphql -f query='
  query {
    repository(owner: "dotnet", name: "msbuild") {
      pullRequest(number: <PR_NUMBER>) {
        id
        title
        baseRefName
        headRefName
        mergeable
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
        reviews(last: 10) {
          nodes {
            state
            author { login }
          }
        }
        reviewThreads(first: 20) {
          nodes {
            isResolved
            comments(first: 1) {
              nodes { body author { login } }
            }
          }
        }
        files(first: 50) {
          nodes { path additions deletions }
          totalCount
        }
      }
    }
  }
'
```

From the response, extract:
- **CI status**: from `statusCheckRollup.state` — `SUCCESS`, `FAILURE`, `PENDING`, `ERROR`, `EXPECTED`, or `null`. Map `ERROR` to failure (❌) and `EXPECTED` to neutral (⚪) in the dashboard.
- **Review decision**: `APPROVED`, `REVIEW_REQUIRED`, `CHANGES_REQUESTED`
- **Approvals**: count and list of `APPROVED` reviews
- **Review comments**: any unresolved threads from the first 20 results (especially `CHANGES_REQUESTED`)
- **Files changed**: use `totalCount` for the file count; use the first 50 file paths from `files.nodes` for suspicious file detection
- **Suspicious flag**: check if any file paths are outside expected patterns (see below)

#### Suspicious File Detection

| PR Category | Expected Files | Flag if... |
|-------------|---------------|------------|
| Dependency update (arcade) | `eng/**`, `global.json` | Any file outside `eng/` or `global.json` |
| Dependency update (roslyn/nuget/runtime) | `eng/Version.Details.xml`, `eng/Version.Details.props` | Any file outside `eng/` |
| Codeflow (dotnet/dotnet) | `eng/Version.Details.xml` | Any `src/` files modified |
| OneLoc | `**/xlf/*.xlf`, `**/Resources/*.resx` | Any non-localization file, or reviewers flagging reverted translations |

### Step 3: Update Branches & Bump Versions (dependency PRs only)

For each **dependency update** PR:

#### 3a. Update the PR branch

Use the GitHub GraphQL `updatePullRequestBranch` mutation — the exact equivalent of the "Update branch" button:

```bash
gh api graphql -f query='
  mutation {
    updatePullRequestBranch(input: {pullRequestId: "<NODE_ID>"}) {
      pullRequest { number headRefOid }
    }
  }
'
```

#### 3b. Bump VersionPrefix (non-main branches only)

**Skip for `main`** — it uses `Major.Minor.0` with a `preview` label.

For `vs*` branches:

1. Read `<VersionPrefix>X.Y.Z</VersionPrefix>` from the **target branch's** `eng/Versions.props`
2. Increment patch: `X.Y.Z` → `X.Y.(Z+1)`
3. Checkout the PR branch, update the version, commit, push:
   ```bash
   git fetch origin <head-branch>
   git checkout <head-branch>
   # Edit eng/Versions.props: set <VersionPrefix> to X.Y.(Z+1)
   git add eng/Versions.props
   git commit -m "Bump VersionPrefix to X.Y.(Z+1)"
   git push origin <head-branch>
   ```

**Note:** The `<VersionPrefix>` line may be inline with `<DotNetFinalVersionKind>` — preserve that formatting.

### Step 4: Produce the Dashboard

Output a **single dashboard** with all PRs grouped by category. This is the most important output — it lets the user click through and approve/reject each PR.

Format:

```
## 🚦 Dependency Update PRs

| PR | Branch | CI | Review | Files | Suspicious? | Link |
|----|--------|-----|--------|-------|-------------|------|
| #13281 | main ← roslyn | ✅ SUCCESS | ✅ APPROVED (1) | 2 | ✅ Clean | https://github.com/dotnet/msbuild/pull/13281 |
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

After the tables, add a summary:
> **N PRs total. M ready to merge, K need reviews, J have CI failures, L blocked.**

## Version File Reference

The version is defined in `eng/Versions.props`:

```xml
<PropertyGroup>
  <VersionPrefix>18.3.3</VersionPrefix>
</PropertyGroup>
```

| Branch Pattern | VersionPrefix Format | Bump Needed? | Pre-release Label |
|---------------|---------------------|-------------|------------------|
| `main`        | `Major.Minor.0`     | No          | `preview`        |
| `vs*`         | `Major.Minor.Patch` | Yes (+1)    | `servicing` or `preview` |

## Troubleshooting

| Problem | Solution |
|---------|----------|
| `updatePullRequestBranch` fails | PR may have conflicts GitHub can't auto-resolve — fall back to local `git merge` |
| Push rejected after version bump | Branch was updated by maestro; re-fetch and retry |
| VersionPrefix has no patch component (e.g. `18.6.0` on main) | This is main — skip the bump |
| OneLoc PR flagged as "reverting translations" | Do NOT merge — wait for updated translations |
