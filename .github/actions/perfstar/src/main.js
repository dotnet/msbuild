const core = require('@actions/core');
const github = require('@actions/github');
const { reconcilePerfStarBranch } = require('./branch-manager');

async function run() {
  const token = core.getInput('github-token', { required: true });
  const octokit = github.getOctokit(token);

  await reconcilePerfStarBranch({
    github: octokit,
    context: github.context,
    core,
  });
}

run().catch(error => {
  core.setFailed(error.message);
});
