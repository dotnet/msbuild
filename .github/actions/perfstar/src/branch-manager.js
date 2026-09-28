const EVALUATION_LABEL = 'PerfStar: Evaluate';

async function deleteBranch(github, context, core, name) {
  try {
    await github.rest.git.deleteRef({
      owner: context.repo.owner,
      repo: context.repo.repo,
      ref: `heads/${name}`,
    });
    core.notice(`Deleted PerfStar branch ${name}`);
  } catch (error) {
    if (error.status !== 404) {
      throw error;
    }

    core.notice(`PerfStar branch ${name} was already deleted`);
  }
}

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

async function reconcilePerfStarBranch({ github, context, core }) {
  const { data: pullRequest } = await github.rest.pulls.get({
    owner: context.repo.owner,
    repo: context.repo.repo,
    pull_number: context.payload.pull_request.number,
  });
  const branch = `perf/pr-${pullRequest.number}`;
  const stagingBranch = `perfstar-staging/pr-${pullRequest.number}`;

  const isEvaluationRequested = pullRequest.state === 'open' &&
    pullRequest.labels.some(label => label.name === EVALUATION_LABEL);

  if (!isEvaluationRequested) {
    await deleteBranch(github, context, core, branch);
    await deleteBranch(github, context, core, stagingBranch);
    return;
  }

  if (context.payload.action !== 'labeled' ||
      context.payload.label.name !== EVALUATION_LABEL ||
      context.payload.pull_request.head.sha !== pullRequest.head.sha) {
    core.notice('No new PerfStar approval for the current pull request commit');
    return;
  }

  let kickoffSha;

  try {
    await upsertBranch(github, context, stagingBranch, pullRequest.head.sha);

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
      message: `Queue PerfStar evaluation for #${pullRequest.number}`,
      tree: mergedCommit.tree.sha,
      parents: [mergedSha],
    });

    kickoffSha = kickoffCommit.sha;
    await upsertBranch(github, context, branch, kickoffSha);
  } finally {
    await deleteBranch(github, context, core, stagingBranch);
  }

  const branchUrl = `${context.serverUrl}/${context.repo.owner}/${context.repo.repo}/tree/${branch}`;
  core.notice(`Queued PerfStar evaluation from ${branch} at ${kickoffSha}`);
  await core.summary
    .addHeading('PerfStar evaluation queued')
    .addLink(branch, branchUrl)
    .addRaw(` now contains pull request #${pullRequest.number} at ${pullRequest.head.sha} with ${pullRequest.base.ref} merged in.`)
    .write();
}

module.exports = { reconcilePerfStarBranch };
