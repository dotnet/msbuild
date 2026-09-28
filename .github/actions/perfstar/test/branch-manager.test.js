const assert = require('node:assert/strict');
const test = require('node:test');
const { reconcilePerfStarBranch } = require('../src/branch-manager');

const pullRequestNumber = 42;
const initialPullRequest = {
  number: pullRequestNumber,
  state: 'open',
  head: { sha: 'pr-head-sha' },
  base: { ref: 'main' },
  labels: [{ name: 'PerfStar: Evaluate' }],
};

function createHarness({
  pullRequest = initialPullRequest,
  action = 'labeled',
  eventHeadSha = initialPullRequest.head.sha,
  existingRefs = [],
  merge = { sha: 'merged-sha' },
  mergeError,
} = {}) {
  const calls = [];
  const refs = new Set(existingRefs);
  const github = {
    rest: {
      pulls: {
        get: async () => {
          calls.push(['pulls.get']);
          return { data: pullRequest };
        },
      },
      git: {
        getRef: async ({ ref }) => {
          calls.push(['git.getRef', ref]);
          if (!refs.has(ref)) {
            const error = new Error('Not found');
            error.status = 404;
            throw error;
          }
          return {};
        },
        createRef: async ({ ref, sha }) => {
          calls.push(['git.createRef', ref, sha]);
          refs.add(ref.slice('refs/'.length));
        },
        updateRef: async ({ ref, sha }) => {
          calls.push(['git.updateRef', ref, sha]);
        },
        deleteRef: async ({ ref }) => {
          calls.push(['git.deleteRef', ref]);
          refs.delete(ref);
        },
        getCommit: async ({ commit_sha }) => {
          calls.push(['git.getCommit', commit_sha]);
          return { data: { tree: { sha: 'tree-sha' } } };
        },
        createCommit: async ({ parents }) => {
          calls.push(['git.createCommit', parents]);
          return { data: { sha: 'kickoff-sha' } };
        },
      },
      repos: {
        merge: async () => {
          calls.push(['repos.merge']);
          if (mergeError) {
            throw mergeError;
          }
          return { data: merge };
        },
      },
    },
  };
  const notices = [];
  const summaryCalls = [];
  const summary = {
    addHeading(value) {
      summaryCalls.push(['addHeading', value]);
      return this;
    },
    addLink(...args) {
      summaryCalls.push(['addLink', ...args]);
      return this;
    },
    addRaw(value) {
      summaryCalls.push(['addRaw', value]);
      return this;
    },
    write: async () => summaryCalls.push(['write']),
  };
  const core = {
    notice: message => notices.push(message),
    summary,
  };
  const context = {
    payload: {
      action,
      label: { name: action === 'labeled' ? 'PerfStar: Evaluate' : undefined },
      pull_request: {
        number: pullRequestNumber,
        head: { sha: eventHeadSha },
      },
    },
    repo: { owner: 'dotnet', repo: 'msbuild' },
    serverUrl: 'https://github.com',
  };

  return {
    calls,
    context,
    core,
    github,
    notices,
    refs,
    summaryCalls,
    run: () => reconcilePerfStarBranch({ github, context, core }),
  };
}

test('queues the branch for a newly labeled current commit', async () => {
  const harness = createHarness();

  await harness.run();

  assert.ok(harness.calls.some(([name]) => name === 'repos.merge'));
  assert.ok(harness.calls.some(([name, ref]) =>
    name === 'git.createRef' && ref === 'refs/heads/perf/pr-42'));
  assert.ok(harness.calls.some(([name, ref]) =>
    name === 'git.deleteRef' && ref === 'heads/perfstar-staging/pr-42'));
  assert.ok(harness.notices.some(message => message.includes('kickoff-sha')));
});

test('updates an existing branch when the label is reapplied', async () => {
  const harness = createHarness({ existingRefs: ['heads/perf/pr-42'] });

  await harness.run();

  assert.ok(harness.calls.some(([name, ref, sha]) =>
    name === 'git.updateRef' && ref === 'heads/perf/pr-42' && sha === 'kickoff-sha'));
});

test('deletes both branches when the label is absent', async () => {
  const harness = createHarness({
    action: 'unlabeled',
    pullRequest: { ...initialPullRequest, labels: [] },
  });

  await harness.run();

  assert.deepEqual(
    harness.calls.filter(([name]) => name === 'git.deleteRef').map(([, ref]) => ref),
    ['heads/perf/pr-42', 'heads/perfstar-staging/pr-42'],
  );
  assert.ok(!harness.calls.some(([name]) => name === 'repos.merge'));
});

test('deletes both branches when the pull request is closed', async () => {
  const harness = createHarness({
    action: 'closed',
    pullRequest: { ...initialPullRequest, state: 'closed' },
  });

  await harness.run();

  assert.deepEqual(
    harness.calls.filter(([name]) => name === 'git.deleteRef').map(([, ref]) => ref),
    ['heads/perf/pr-42', 'heads/perfstar-staging/pr-42'],
  );
});

test('does not queue a commit when a push follows label approval', async () => {
  const harness = createHarness({ action: 'synchronize' });

  await harness.run();

  assert.ok(!harness.calls.some(([name]) => name === 'repos.merge'));
  assert.deepEqual(harness.summaryCalls, []);
  assert.ok(harness.notices.some(message => message.includes('No new PerfStar approval')));
});

test('does not queue a stale label event for a newer current commit', async () => {
  const harness = createHarness({
    eventHeadSha: 'older-event-sha',
    pullRequest: {
      ...initialPullRequest,
      head: { sha: 'new-current-sha' },
    },
  });

  await harness.run();

  assert.ok(!harness.calls.some(([name]) => name === 'repos.merge'));
  assert.deepEqual(harness.summaryCalls, []);
});

test('deletes the staging branch if merging fails', async () => {
  const harness = createHarness({ mergeError: new Error('Merge failed') });

  await assert.rejects(harness.run(), /Merge failed/);

  assert.ok(harness.calls.some(([name, ref]) =>
    name === 'git.deleteRef' && ref === 'heads/perfstar-staging/pr-42'));
});
