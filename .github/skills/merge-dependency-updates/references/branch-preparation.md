# Authorized branch preparation and merging

Load this procedure only when the user requested the specific external action.
An earlier triage dashboard is not authorization, and its checks become stale
when a branch changes.

## Preflight

Resolve the PR node ID, head repository, head branch, current head SHA, base
repository/branch, and requested action from fresh GitHub data. A bot may have
updated the branch since the last query. Preserve the existing worktree; prefer
an isolated worktree for any required local edit. Do not assume `origin` names
the PR's head repository or that an identically named local branch is its head.

## Update the PR branch

For a GitHub-managed update, inspect the current mutation schema or supported
`gh pr update-branch --help`. The GraphQL operation supports checking the head:

```powershell
$query = @'
mutation($id: ID!, $head: GitObjectID!) {
  updatePullRequestBranch(input: {pullRequestId: $id, expectedHeadOid: $head}) {
    pullRequest { number headRefOid }
  }
}
'@
gh api graphql -f "query=$query" -f "id=$pullRequestId" -f "head=$expectedHead"
if ($LASTEXITCODE -ne 0) { throw "Branch update failed; reconcile current PR state." }
```

A conflict, permission error, or unexpected head is a blocker to diagnose, not
permission to merge locally, force-push, or blindly retry the mutation.

## Version changes on historical servicing branches

Do not bump every `vs*` branch. Historically, branches older than `vs18.10`
incremented `VersionPrefix` for dependency updates; later release branding uses a
different publishing process. This is a historical boundary, not a substitute
for reading the target branch's current `eng\Versions.props` and release policy.
Read `PreReleaseVersionLabel` instead of assuming `preview` or `servicing`.

If the selected branch policy actually requires a bump:

1. Read the base branch's version and the PR branch's existing version change. Determine the intended result once so retries do not increment it again.
2. Apply the smallest authorized edit in the isolated PR worktree. Preserve surrounding XML, including inline properties.
3. Review the diff; stage only the intended file and use the repository's commit conventions.
4. Push to the verified head repository/branch without force. If the remote advanced, stop and reconcile rather than overwriting the bot's new work.

`18.6.0` has a patch component of zero; that shape alone does not identify `main`.
Use the actual target branch and policy.

## Merge only when requested

Refresh the head SHA, draft/mergeability state, current checks, required policies,
and review decision. Resolve incomplete evidence first. Do not dismiss reviews,
bypass branch rules, resolve threads, or enable auto-merge unless that distinct
action was authorized.

Use the current `gh pr merge --help` for supported options. Where available,
`--match-head-commit` prevents merging an unexpected newer head. Select the
authorized merge method explicitly; do not append branch deletion by default.
After a successful action, read back the PR/branch state and report what changed.
