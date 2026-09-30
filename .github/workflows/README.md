# GitHub workflows

This directory contains GitHub Actions workflows and GitHub Agentic Workflow sources.
Files ending in `.agent.md` define agentic workflows.
Their generated `.agent.lock.yml` files are the runnable workflows.
Edit the source and regenerate its lock file when changing an agentic workflow.

| Workflow | Trigger | Purpose and commands |
|---|---|---|
| [Backport PR to branch](backport.yml) | New issue comment on a PR. | Calls the Arcade backport workflow. Comment `/backport to <branch>` or `/backport <branch>` on the PR. |
| [Copilot Setup Steps](copilot-setup-steps.yml) | Manual `workflow_dispatch`; Copilot coding agent setup. | Restores MSBuild and sets up the .NET environment for the coding agent. |
| [Labeler: Cache Retention](labeler-cache-retention.yml) | Daily schedule or manual dispatch. | Restores the issue-label model to prevent cache eviction. |
| [Labeler: Predict (Issues)](labeler-predict-issues.yml) | New issue or manual dispatch with issue numbers. | Predicts `Area: ` labels for issues. |
| [Labeler: Predict (Pulls)](labeler-predict-pulls.yml) | New PR targeting `main` or `vs*`, five-minute schedule, or manual dispatch. | Predicts `Area: ` labels for PRs. |
| [Labeler: Promotion](labeler-promote.yml) | Manual dispatch. | Promotes staged issue or PR label models to `ACTIVE`. |
| [Labeler: Training](labeler-train.yml) | Manual dispatch. | Downloads data, trains, and tests issue or PR label models. |
| [Validate PerfStar action](perfstar-action-validation.yml) | PR or `main` push changing the action or its workflows. | Tests the action and checks its generated bundle. |
| [Queue PerfStar evaluation](perfstar-branch.yml) | New PR comment; PR head update, closure, or merge. | Comment `/perfstar run` to queue the current head or `/perfstar cancel` to delete its branches. Closure and merge also delete them. See [action details](../actions/perfstar/README.md). |
| [Skill Validation](skill-validation.yml) | PR or `main` push changing skills, agents, or validation workflows; manual dispatch. | Validates skill and agent definitions and publishes results as an artifact. |
| [Skill Validation — PR Comment](skill-validation-comment.yml) | Completion of Skill Validation for a PR. | Posts the validation result on the PR. |
| [Sync Microsoft.Build version](SyncAnalyzerTemplateMSBuildVersion.yml) | `main` push changing `eng/Versions.props`. | Opens a PR when the analyzer template needs the new MSBuild version. |
| [Validate PAT Pool](validate-pat-pool.yml) | Daily schedule or manual dispatch. | Checks Copilot PAT pool credentials. |

## Agentic workflows

Each source below has a generated `.agent.lock.yml` file with the same base name.
The lock file implements the trigger declared in the source.

| Source | Trigger | Purpose and commands |
|---|---|---|
| [Close Stale Pull Requests](close-stale-prs.agent.md) ([lock](close-stale-prs.agent.lock.yml)) | Weekly Monday schedule or manual dispatch. | Warns about inactive PRs and closes eligible stale PRs. |
| [Dreaming](dreaming.agent.md) ([lock](dreaming.agent.lock.yml)) | Weekly Monday schedule or manual dispatch. | Proposes PRs that improve agent instructions based on recent review and CI feedback. |
| [Flaky Test Triage](flaky-test-detector.agent.md) ([lock](flaky-test-detector.agent.lock.yml)) | Daily schedule or manual dispatch. | Tracks flaky tests and proposes test quarantine changes. |
| [Flaky Test Auto-Fixer](flaky-test-fixer.agent.md) ([lock](flaky-test-fixer.agent.lock.yml)) | Daily schedule or manual dispatch. | Proposes test-only fixes for quarantined tests that still fail. |
| [Expert Code Review (on open)](review-on-open.agent.md) ([lock](review-on-open.agent.lock.yml)) | Non-draft PR opened or draft PR marked ready. | Reviews PRs from repository users with write-level access. |
| [Expert Code Review (command)](review.agent.md) ([lock](review.agent.lock.yml)) | New PR comment from a user with write-level access. | Comment `/review` on a PR to request an expert review. |

PR comment commands run only when their workflow exists on the default branch.
The PerfStar workflow checks out the trusted default branch for its action, even for PRs targeting older branches.
The action merges the PR's actual base branch into the evaluation branch and never checks out PR head code.
