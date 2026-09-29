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
  deleteRefError,
  actorPermission = 'write',
  permissionError,
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
          if (deleteRefError) {
            throw deleteRefError;
          }
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
        getCollaboratorPermissionLevel: async ({ username }) => {
          calls.push(['repos.getCollaboratorPermissionLevel', username]);
          if (permissionError) {
            throw permissionError;
          }
          return {
            data: {
              permission: actorPermission,
              role_name: actorPermission,
            },
          };
        },
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
    actor: 'trusted-maintainer',
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

test('allows write, maintain, and admin permission to queue evaluation', async t => {
  for (const permission of ['write', 'push', 'maintain', 'admin']) {
    await t.test(permission, async () => {
      const harness = createHarness({ actorPermission: permission });

      await harness.run();

      assert.ok(harness.calls.some(([name]) => name === 'repos.merge'));
    });
  }
});

test('does not queue evaluation when the labeler has triage or read permission', async t => {
  for (const permission of ['triage', 'read', 'pull']) {
    await t.test(permission, async () => {
      const harness = createHarness({ actorPermission: permission });

      await harness.run();

      assert.ok(!harness.calls.some(([name]) => name === 'repos.merge'));
      assert.ok(harness.notices.some(message => message.includes(`has ${permission} permission`)));
    });
  }
});

test('does not queue evaluation when the labeler is not a collaborator', async () => {
  const notCollaboratorError = new Error('Not found');
  notCollaboratorError.status = 404;
  const harness = createHarness({ permissionError: notCollaboratorError });

  await harness.run();

  assert.ok(!harness.calls.some(([name]) => name === 'repos.merge'));
  assert.ok(harness.notices.some(message => message.includes('not a repository collaborator')));
});

test('surfaces collaborator permission lookup failures', async () => {
  const lookupError = new Error('Permission lookup failed');
  lookupError.status = 403;
  const harness = createHarness({ permissionError: lookupError });

  await assert.rejects(harness.run(), /Permission lookup failed/);

  assert.ok(!harness.calls.some(([name]) => name === 'repos.merge'));
});

test('deletes both branches when the label is absent', async () => {
  const harness = createHarness({
    action: 'unlabeled',
    pullRequest: { ...initialPullRequest, labels: [] },
  });

  await harness.run();

  assert.ok(!harness.calls.some(([name]) => name === 'repos.getCollaboratorPermissionLevel'));
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

  assert.ok(!harness.calls.some(([name]) => name === 'repos.getCollaboratorPermissionLevel'));
  assert.deepEqual(
    harness.calls.filter(([name]) => name === 'git.deleteRef').map(([, ref]) => ref),
    ['heads/perf/pr-42', 'heads/perfstar-staging/pr-42'],
  );
});

test('treats missing-reference responses as successful cleanup', async () => {
  const missingReferenceError = new Error('Reference does not exist');
  missingReferenceError.status = 422;
  missingReferenceError.response = {
    data: { message: 'Reference does not exist' },
  };
  const harness = createHarness({
    action: 'closed',
    deleteRefError: missingReferenceError,
    pullRequest: { ...initialPullRequest, state: 'closed' },
  });

  await harness.run();

  assert.equal(
    harness.notices.filter(message => message.includes('was already deleted')).length,
    2,
  );
});

test('does not suppress other validation errors during cleanup', async () => {
  const validationError = new Error('Validation failed');
  validationError.status = 422;
  validationError.response = {
    data: { message: 'Validation failed' },
  };
  const harness = createHarness({
    action: 'closed',
    deleteRefError: validationError,
    pullRequest: { ...initialPullRequest, state: 'closed' },
  });

  await assert.rejects(harness.run(), /Validation failed/);
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
