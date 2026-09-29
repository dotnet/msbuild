const EVALUATION_PERMISSIONS = new Set(['admin', 'maintain', 'write', 'push']);
const APPROVED_HEAD_PATTERN = /^PerfStar-Approved-Head: ([a-f0-9]{40})$/m;

/**
 * Authorize a PR comment command only when its actor currently has write-level repository access.
 * A missing collaborator is denied; other permission lookup failures stop the action.
 */
async function isAuthorizedEvaluator(github, context, core) {
  let collaborator;

  try {
    ({ data: collaborator } = await github.rest.repos.getCollaboratorPermissionLevel({
      owner: context.repo.owner,
      repo: context.repo.repo,
      username: context.actor,
    }));
  } catch (error) {
    if (error.status !== 404) {
      throw error;
    }

    core.notice(`PerfStar command denied: ${context.actor} is not a repository collaborator`);
    return false;
  }

  const permission = collaborator.role_name ?? collaborator.permission;
  if (!EVALUATION_PERMISSIONS.has(permission)) {
    core.notice(`PerfStar command denied: ${context.actor} has ${permission} permission`);
    return false;
  }

  return true;
}

/**
 * Delete a named repository branch, including a branch already removed by another run.
 * Only 404 and GitHub's specific missing-reference 422 are expected no-ops.
 */
async function deleteBranch(github, context, core, name) {
  try {
    await github.rest.git.deleteRef({
      owner: context.repo.owner,
      repo: context.repo.repo,
      ref: `heads/${name}`,
    });
    core.notice(`Deleted PerfStar branch ${name}`);
  } catch (error) {
    const isMissingReference = error.status === 404 ||
      (error.status === 422 && error.response?.data?.message === 'Reference does not exist');

    if (!isMissingReference) {
      throw error;
    }

    core.notice(`PerfStar branch ${name} was already deleted`);
  }
}

/**
 * Remove an evaluation branch only when its kickoff commit does not approve the live PR head.
 * A missing branch needs no cleanup; a late push event must preserve a newer approved run.
 */
async function deleteStaleBranch(github, context, core, branch, headSha) {
  let branchSha;

  try {
    const { data: reference } = await github.rest.git.getRef({
      owner: context.repo.owner,
      repo: context.repo.repo,
      ref: `heads/${branch}`,
    });
    branchSha = reference.object.sha;
  } catch (error) {
    if (error.status === 404) {
      return;
    }
    throw error;
  }

  const { data: kickoffCommit } = await github.rest.git.getCommit({
    owner: context.repo.owner,
    repo: context.repo.repo,
    commit_sha: branchSha,
  });
  const approvedHead = kickoffCommit.message.match(APPROVED_HEAD_PATTERN)?.[1];

  if (approvedHead !== headSha) {
    await deleteBranch(github, context, core, branch);
  }
}

/**
 * Point a repository branch to sha, creating it when absent.
 * The caller must supply a trusted PR head or kickoff commit SHA; updates intentionally replace old refs.
 */
async function upsertBranch(github, context, name, sha) {
  const ref = `heads/${name}`;
  const repository = {
    owner: context.repo.owner,
    repo: context.repo.repo,
  };

  try {
    await github.rest.git.getRef({
      ...repository,
      ref,
    });

    await github.rest.git.updateRef({
      ...repository,
      ref,
      sha,
      force: true,
    });
  } catch (error) {
    if (error.status !== 404) {
      throw error;
    }

    await github.rest.git.createRef({
      ...repository,
      ref: `refs/${ref}`,
      sha,
    });
  }
}

/**
 * Handle exact new PR comment commands and PR synchronize/close events.
 * Commands require write-level actor access; run publishes only if the live PR still has the approved head.
 * Close and stale-head cleanup do not require actor access. All events must share a serialized PR queue.
 */
async function reconcilePerfStarBranch({ github, context, core }) {
  let command;
  let pullNumber;

  if (context.eventName === 'issue_comment') {
    if (context.payload.action !== 'created' || !context.payload.issue.pull_request) {
      return;
    }

    command = context.payload.comment.body.trim();
    if (command !== '/perfstar run' && command !== '/perfstar cancel') {
      return;
    }

    pullNumber = context.payload.issue.number;
  } else if (context.eventName === 'pull_request_target') {
    if (context.payload.action !== 'synchronize' && context.payload.action !== 'closed') {
      return;
    }

    pullNumber = context.payload.pull_request.number;
  } else {
    throw new Error(`Unsupported PerfStar event: ${context.eventName}`);
  }

  const { data: pullRequest } = await github.rest.pulls.get({
    owner: context.repo.owner,
    repo: context.repo.repo,
    pull_number: pullNumber,
  });
  const branch = `perf/pr-${pullRequest.number}`;
  const stagingBranch = `perfstar-staging/pr-${pullRequest.number}`;

  if (context.eventName === 'pull_request_target' && pullRequest.state !== 'open') {
    await deleteBranch(github, context, core, branch);
    await deleteBranch(github, context, core, stagingBranch);
    return;
  }

  if (context.eventName === 'pull_request_target') {
    await deleteStaleBranch(github, context, core, branch, pullRequest.head.sha);
    return;
  }

  if (!await isAuthorizedEvaluator(github, context, core)) {
    return;
  }

  if (command === '/perfstar cancel') {
    await deleteBranch(github, context, core, branch);
    await deleteBranch(github, context, core, stagingBranch);
    return;
  }

  if (pullRequest.state !== 'open') {
    core.notice(`PerfStar run denied: pull request #${pullNumber} is not open`);
    return;
  }

  const approvedHead = pullRequest.head.sha;
  let kickoffSha;

  try {
    await upsertBranch(github, context, stagingBranch, approvedHead);

    const { data: merge } = await github.rest.repos.merge({
      owner: context.repo.owner,
      repo: context.repo.repo,
      base: stagingBranch,
      head: pullRequest.base.ref,
      commit_message: `Merge ${pullRequest.base.ref} into ${branch} for PerfStar testing`,
    });

    const mergedSha = merge?.sha ?? pullRequest.head.sha;
    const { data: mergedCommit } = await github.rest.git.getCommit({
      owner: context.repo.owner,
      repo: context.repo.repo,
      commit_sha: mergedSha,
    });
    const { data: kickoffCommit } = await github.rest.git.createCommit({
      owner: context.repo.owner,
      repo: context.repo.repo,
      message: `Queue PerfStar evaluation for #${pullRequest.number}\n\nPerfStar-Approved-Head: ${approvedHead}`,
      tree: mergedCommit.tree.sha,
      parents: [mergedSha],
    });

    kickoffSha = kickoffCommit.sha;
    const { data: currentPullRequest } = await github.rest.pulls.get({
      owner: context.repo.owner,
      repo: context.repo.repo,
      pull_number: pullNumber,
    });
    if (currentPullRequest.state !== 'open' || currentPullRequest.head.sha !== approvedHead) {
      core.notice(`PerfStar run not queued: pull request #${pullNumber} changed after approval`);
      return;
    }

    await upsertBranch(github, context, branch, kickoffSha);
  } finally {
    await deleteBranch(github, context, core, stagingBranch);
  }

  const branchUrl = `${context.serverUrl}/${context.repo.owner}/${context.repo.repo}/tree/${branch}`;
  core.notice(`Queued PerfStar evaluation from ${branch} at ${kickoffSha}`);
  await core.summary
    .addHeading('PerfStar evaluation queued')
    .addLink(branch, branchUrl)
    .addRaw(` now contains pull request #${pullRequest.number} at ${approvedHead} with ${pullRequest.base.ref} merged in.`)
    .write();
}

module.exports = { reconcilePerfStarBranch };
