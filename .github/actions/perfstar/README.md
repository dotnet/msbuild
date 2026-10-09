# PerfStar PR commands

Comment `/perfstar run` on an open pull request to request approval for its current head commit.
The bot responds with the SHA and a `/perfstar run <sha>` command.
Review the SHA, then comment the command to approve that commit and queue its evaluation.
You can also comment `/perfstar run <sha>` directly if you already know the full 40-character head SHA.
Only repository users with write, maintain, or admin permission can request or approve a run.
The action queues a run only when the approved SHA matches the live PR head before and after preparation.
The action records the approved head SHA in the kickoff commit on `perf/pr-<number>`.
A later push does not approve the new commit.
Use `/perfstar run` again to request approval for the new head commit.
The bot reacts with eyes when it starts handling a recognized comment command.
It reacts with rocket after an approved run is queued, or with thumbs up after cancellation completes.
For a denied or stale command, it replies with an explanation instead of adding a success reaction.
These reactions do not report the result of a PerfStar evaluation.

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
