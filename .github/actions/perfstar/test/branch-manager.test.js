const assert = require('node:assert/strict');
const test = require('node:test');
const { reconcilePerfStarBranch } = require('../src/branch-manager');

const HEAD = 'a'.repeat(40);
const NEXT_HEAD = 'b'.repeat(40);
const BRANCH = 'heads/perf/pr-42';
const STAGING = 'heads/perfstar-staging/pr-42';
const pullRequest = {
  number: 42,
  state: 'open',
  head: { sha: HEAD },
  base: { ref: 'main' },
};

function apiError(status, message) {
  const error = new Error(message);
  error.status = status;
  error.response = { data: { message } };
  return error;
}

function createHarness({
  eventName = 'issue_comment',
  action = eventName === 'issue_comment' ? 'created' : 'synchronize',
  body = '/perfstar run',
  isPullRequest = true,
  permission = 'write',
  permissionError,
  pullRequests = [pullRequest],
  refs = {},
  merge = { sha: 'merged-sha' },
  mergeError,
  deleteRefError,
} = {}) {
  const calls = [];
  const references = new Map(Object.entries(refs));
  let pullReads = 0;
  const github = {
    rest: {
      pulls: {
        get: async ({ pull_number }) => {
          calls.push(['pulls.get', pull_number]);
          const current = pullRequests[Math.min(pullReads++, pullRequests.length - 1)];
          return { data: current };
        },
      },
      repos: {
        getCollaboratorPermissionLevel: async ({ username }) => {
          calls.push(['repos.getCollaboratorPermissionLevel', username]);
          if (permissionError) {
            throw permissionError;
          }
          return { data: { role_name: permission } };
        },
        merge: async ({ base, head }) => {
          calls.push(['repos.merge', base, head]);
          if (mergeError) {
            throw mergeError;
          }
          return { data: merge };
        },
      },
      git: {
        getRef: async ({ ref }) => {
          calls.push(['git.getRef', ref]);
          if (!references.has(ref)) {
            throw apiError(404, 'Not Found');
          }
          return { data: { ref: `refs/${ref}`, object: { sha: references.get(ref) } } };
        },
        createRef: async ({ ref, sha }) => {
          calls.push(['git.createRef', ref, sha]);
          references.set(ref.slice('refs/'.length), sha);
        },
        updateRef: async ({ ref, sha }) => {
          calls.push(['git.updateRef', ref, sha]);
          references.set(ref, sha);
        },
        deleteRef: async ({ ref }) => {
          calls.push(['git.deleteRef', ref]);
          if (deleteRefError) {
            throw deleteRefError;
          }
          if (!references.delete(ref)) {
            throw apiError(422, 'Reference does not exist');
          }
        },
        getCommit: async ({ commit_sha }) => {
          calls.push(['git.getCommit', commit_sha]);
          return {
            data: {
              tree: { sha: 'tree-sha' },
              message: commit_sha === 'existing-kickoff'
                ? `Queue PerfStar evaluation for #42\n\nPerfStar-Approved-Head: ${HEAD}`
                : 'Legacy evaluation commit',
            },
          };
        },
        createCommit: async ({ message, parents }) => {
          calls.push(['git.createCommit', message, parents]);
          return { data: { sha: 'new-kickoff' } };
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
    addLink(...values) {
      summaryCalls.push(['addLink', ...values]);
      return this;
    },
    addRaw(value) {
      summaryCalls.push(['addRaw', value]);
      return this;
    },
    write: async () => summaryCalls.push(['write']),
  };
  const core = { notice: message => notices.push(message), summary };
  const context = {
    eventName,
    actor: 'trusted-maintainer',
    repo: { owner: 'dotnet', repo: 'msbuild' },
    serverUrl: 'https://github.com',
    payload: eventName === 'issue_comment'
      ? {
          action,
          issue: { number: 42, ...(isPullRequest && { pull_request: {} }) },
          comment: { body },
        }
      : {
          action,
          pull_request: { number: 42, head: { sha: HEAD } },
        },
  };

  return {
    calls,
    context,
    notices,
    references,
    summaryCalls,
    run: () => reconcilePerfStarBranch({ github, context, core }),
  };
}

function callsTo(harness, operation) {
  return harness.calls.filter(([name]) => name === operation);
}

test('runs a command for the live PR head and records the approved SHA', async () => {
  const harness = createHarness();

  await harness.run();

  assert.deepEqual(callsTo(harness, 'pulls.get'), [
    ['pulls.get', 42],
    ['pulls.get', 42],
  ]);
  assert.equal(harness.references.get(BRANCH), 'new-kickoff');
  assert.equal(harness.references.has(STAGING), false);
  assert.match(callsTo(harness, 'git.createCommit')[0][1], new RegExp(`PerfStar-Approved-Head: ${HEAD}`));
  assert.equal(harness.summaryCalls.at(-1)[0], 'write');
});

test('re-running updates the evaluation branch', async () => {
  const harness = createHarness({ refs: { [BRANCH]: 'existing-kickoff' } });

  await harness.run();

  assert.equal(harness.references.get(BRANCH), 'new-kickoff');
  assert.deepEqual(callsTo(harness, 'git.updateRef').at(-1), ['git.updateRef', BRANCH, 'new-kickoff']);
});

test('does not publish a run if the PR changes before branch publication', async () => {
  const harness = createHarness({
    pullRequests: [pullRequest, { ...pullRequest, head: { sha: NEXT_HEAD } }],
  });

  await harness.run();

  assert.equal(harness.references.has(BRANCH), false);
  assert.equal(harness.references.has(STAGING), false);
  assert.ok(harness.notices.some(message => message.includes('changed after approval')));
});

test('does not publish a run if the PR closes during preparation', async () => {
  const harness = createHarness({
    pullRequests: [pullRequest, { ...pullRequest, state: 'closed' }],
  });

  await harness.run();

  assert.equal(harness.references.has(BRANCH), false);
});

test('denies a run command on a closed PR', async () => {
  const harness = createHarness({ pullRequests: [{ ...pullRequest, state: 'closed' }] });

  await harness.run();

  assert.equal(callsTo(harness, 'repos.merge').length, 0);
  assert.ok(harness.notices.some(message => message.includes('is not open')));
});

test('accepts only exact, newly created PR commands', async t => {
  for (const options of [
    { body: 'Please /perfstar run' },
    { body: '/perfstar run extra' },
    { body: '/perfstar RUN' },
    { action: 'edited' },
    { isPullRequest: false },
  ]) {
    await t.test(JSON.stringify(options), async () => {
      const harness = createHarness(options);

      await harness.run();

      assert.deepEqual(harness.calls, []);
    });
  }
});

test('checks the comment actor for both commands', async t => {
  for (const body of ['/perfstar run', '/perfstar cancel']) {
    for (const permission of ['write', 'push', 'maintain', 'admin']) {
      await t.test(`${body} as ${permission}`, async () => {
        const harness = createHarness({ body, permission });

        await harness.run();

        assert.equal(callsTo(harness, 'repos.getCollaboratorPermissionLevel').length, 1);
        assert.equal(callsTo(harness, body.endsWith('run') ? 'repos.merge' : 'git.deleteRef').length > 0, true);
      });
    }
    for (const permission of ['triage', 'read', 'pull']) {
      await t.test(`${body} as ${permission}`, async () => {
        const harness = createHarness({ body, permission });

        await harness.run();

        assert.equal(callsTo(harness, 'repos.merge').length, 0);
        assert.equal(callsTo(harness, 'git.deleteRef').length, 0);
      });
    }
  }
});

test('denies non-collaborators and surfaces permission lookup failures', async t => {
  await t.test('not a collaborator', async () => {
    const harness = createHarness({ permissionError: apiError(404, 'Not Found') });

    await harness.run();

    assert.equal(callsTo(harness, 'repos.merge').length, 0);
    assert.ok(harness.notices.some(message => message.includes('not a repository collaborator')));
  });

  await t.test('API failure', async () => {
    const harness = createHarness({ permissionError: apiError(403, 'Forbidden') });
    await assert.rejects(harness.run(), /Forbidden/);
  });
});

test('cancel removes both branches even when they are missing', async () => {
  const harness = createHarness({ body: '/perfstar cancel', refs: { [BRANCH]: 'existing-kickoff' } });

  await harness.run();

  assert.equal(harness.references.size, 0);
  assert.deepEqual(callsTo(harness, 'git.deleteRef').map(([, ref]) => ref), [BRANCH, STAGING]);
  assert.equal(callsTo(harness, 'repos.merge').length, 0);
});

test('closing or merging a PR deletes both branches without a permission check', async t => {
  for (const merged of [false, true]) {
    await t.test(merged ? 'merged' : 'closed without merge', async () => {
      const harness = createHarness({
        eventName: 'pull_request_target',
        action: 'closed',
        pullRequests: [{ ...pullRequest, state: 'closed', merged }],
        refs: { [BRANCH]: 'existing-kickoff', [STAGING]: HEAD },
      });

      await harness.run();

      assert.equal(harness.references.size, 0);
      assert.deepEqual(callsTo(harness, 'git.deleteRef').map(([, ref]) => ref), [BRANCH, STAGING]);
      assert.equal(callsTo(harness, 'repos.getCollaboratorPermissionLevel').length, 0);
    });
  }
});

test('synchronize deletes only when the approved SHA differs from the live head', async t => {
  await t.test('new head removes old approval', async () => {
    const harness = createHarness({
      eventName: 'pull_request_target',
      pullRequests: [{ ...pullRequest, head: { sha: NEXT_HEAD } }],
      refs: { [BRANCH]: 'existing-kickoff' },
    });

    await harness.run();

    assert.equal(harness.references.has(BRANCH), false);
  });

  await t.test('late synchronize preserves a newer approved run', async () => {
    const harness = createHarness({
      eventName: 'pull_request_target',
      refs: { [BRANCH]: 'existing-kickoff' },
    });
    harness.context.payload.pull_request.head.sha = NEXT_HEAD;

    await harness.run();

    assert.equal(harness.references.get(BRANCH), 'existing-kickoff');
    assert.equal(callsTo(harness, 'git.deleteRef').length, 0);
  });

  await t.test('legacy branch without approval metadata is removed', async () => {
    const harness = createHarness({
      eventName: 'pull_request_target',
      refs: { [BRANCH]: 'legacy-kickoff' },
    });

    await harness.run();

    assert.equal(harness.references.has(BRANCH), false);
  });

  await t.test('no branch is a no-op', async () => {
    const harness = createHarness({ eventName: 'pull_request_target' });

    await harness.run();

    assert.equal(callsTo(harness, 'git.deleteRef').length, 0);
  });
});

test('does not swallow other delete errors', async () => {
  const harness = createHarness({
    body: '/perfstar cancel',
    deleteRefError: apiError(422, 'Validation failed'),
  });

  await assert.rejects(harness.run(), /Validation failed/);
});

test('cleans staging if merge fails', async () => {
  const harness = createHarness({ mergeError: new Error('Merge failed') });

  await assert.rejects(harness.run(), /Merge failed/);

  assert.equal(harness.references.has(STAGING), false);
});
