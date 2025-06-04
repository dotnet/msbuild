# Issue-Labeler Workflows

This repository uses actions from [dotnet/issue-labeler](https://github.com/dotnet/issue-labeler) to predict area labels for issues and pull requests.

The following workflow templates were imported and updated from [dotnet/issue-labeler/wiki/Onboarding](https://github.com/dotnet/issue-labeler/wiki/Onboarding):

1. `labeler-cache-retention.yml`
2. `labeler-predict-issues.yml`
3. `labeler-predict-pulls.yml`
4. `labeler-promote.yml`
5. `labeler-train.yml`

## Repository Configuration

Across these workflows, the following changes were made to configure the issue labeler for this repository:

1. Set `LABEL_PREFIX` to `"Area: "`:
    - `labeler-predict-issues.yml`
    - `labeler-predict-pulls.yml`
    - `labeler-train.yml`
2. Remove the `DEFAULT_LABEL` setting since no default label is applied when prediction is not made:
    - `labeler-predict-issues.yml`
    - `labeler-predict-pulls.yml`
3. Remove the `EXCLUDED_AUTHORS` value as we do not bypass labeling for any authors' issues/pulls in this repository:
    - `labeler-predict-issues.yml`
    - `labeler-predict-pulls.yml`
4. Update the pull request labeling branches to include `main` and `vs*`:
    - `labeler-predict-pulls.yml`
5. Remove the `repository` input for training the models against another repository:
    - `labeler-train.yml`
6. Update the cache retention cron schedule to an arbitrary time of day:
    - `labeler-cache-retention.yml`
7. Disable pull request training, cache retention, and predition
    - `labeler-train.yml` - Change the default from "Both" to "Issues"
    - `labeler-cache-retention.yml` - Remove "pulls" from the job matrix (leaving a comment)
    - `labeler-predict-pulls.yml` - Workflow marked as Disabled via GitHub UI
