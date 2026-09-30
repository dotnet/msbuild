# PerfStar PR commands

Comment `/perfstar run` on an open pull request to queue its current head commit.
Only repository users with write, maintain, or admin permission can use the command.
The action records the approved head SHA in the kickoff commit on `perf/pr-<number>`.
A later push does not approve the new commit.
Use `/perfstar run` again to queue the new head commit.

Comment `/perfstar cancel` to delete the evaluation branch and staging branch.
The same repository permissions apply to this command.
Closing or merging a pull request also deletes both branches.
A push deletes the evaluation branch only if it records an older approved head.
Deletion does not stop a PerfStar run that has already started.

The `issue_comment` workflow runs from the repository's default branch.
The comment commands cannot run until the workflow and local action exist on that branch.
The workflow checks out the trusted default branch to load the action, including for PRs targeting older branches.
The action merges the PR's actual base branch into the evaluation branch and never runs the pull request head code.

Run `npm ci` and `npm run validate` from this directory to test the action and check its committed bundle.
