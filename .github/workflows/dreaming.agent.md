---
name: "Dreaming (learning atoms curation)"
description: "Weekly (and manually triggerable) workflow inspired by Claude Managed Agents 'dreaming'. It reviews the past 7 days of pull requests, review feedback, conversations, and CI check outcomes to find recurring mistakes and misses that agents keep making, then curates the repository's 'learning atoms' — the durable agent-instruction files (AGENTS.md, .github/instructions/**, .github/skills/**, .github/agents/**) — with small, high-signal, non-duplicate changes, preferring to refine existing guidance over adding new, and opens up to three atomic, ready-for-review PRs (one per distinct recurring pattern), each naming (via @-mention in the PR body) the core MSBuild team members who were involved in the discussions behind it so a maintainer can assign them as reviewers."
on:
  # Weekly on Monday morning; the agent looks back over the previous 7 days of activity.
  # Explicit natural-language schedule so gh-aw pins a stable cron on compile.
  schedule: weekly on monday
  workflow_dispatch: # Allow manual triggering
  permissions: {}

if: ${{ github.event_name == 'workflow_dispatch' || !github.event.repository.fork }}

permissions:
  contents: read
  pull-requests: read
  issues: read
  # Read CI check-run / status conclusions on the week's PRs to categorize common failure sources.
  checks: read
  statuses: read
  actions: read

tools:
  edit:
  # The agent uses `gh` (PR/issue search, review threads, `gh pr checks`) and `git diff` to inspect
  # the week's activity and verify its own learning-atom edits before opening the PR.
  bash: [":*"]
  github:
    # gh-proxy mounts a PRE-AUTHENTICATED `gh` CLI inside the agent container so the bash `gh` calls
    # (PR search, review comments, check conclusions) authenticate with the workflow's read-only token.
    mode: gh-proxy
    toolsets: [repos, issues, pull_requests, actions]

safe-outputs:
  # Up to three atomic, ready-for-review PRs per run — one per distinct recurring pattern.
  create-pull-request:
    title-prefix: "[Dreaming] "
    labels: ["Area: Documentation"]
    draft: false
    base-branch: main
    # Up to THREE pull requests per run, so distinct, unrelated recurring patterns each land as their own
    # ATOMIC, independently-reviewable PR (each on its own branch) instead of one mixed bag. If everything
    # this week belongs to a single theme, one PR is still correct — don't split for the sake of it.
    max: 3
    # Exclusive allowlist: this workflow ONLY curates the "learning atoms" — the durable agent-instruction
    # files. It must never touch product code, tests, build manifests, or the workflows themselves.
    # AGENTS.md is the target of the `.github/copilot-instructions.md` symlink (the primary, repo-wide
    # learning atoms); the instructions/skills/agents directories hold the path-scoped and skill-scoped ones.
    allowed-files:
      - "AGENTS.md"
      - ".github/instructions/**"
      - ".github/skills/**"
      - ".github/agents/**"
    # Belt-and-braces: never let the agent modify the workflow definitions (also enforced in the prompt).
    excluded-files:
      - ".github/workflows/**"
    # Curating the learning atoms IS this workflow's whole job, and those files (AGENTS.md and everything
    # under .github/) are exactly what gh-aw's default file-protection policy guards. Left at the default
    # (request_review), the signed-commit push to a protected path is refused and the run silently falls
    # back to opening a review *issue* instead of a PR. `allowed` lets the branch push and PR open normally;
    # the tight `allowed-files` allowlist above is the real guardrail here, and the PR still needs maintainer
    # approval before it can merge, so human review is preserved at the PR gate.
    protected-files: allowed
  # When nothing clears the confidence/signal bar (quiet week, or every pattern is already captured),
  # the agent emits a `noop`. Keep it in the run summary for debuggability, but do NOT file an issue —
  # a weekly no-op should be silent.
  noop:
    report-as-issue: false

# One week-in-review scan (PR search + review-thread/check reads) plus in-place learning-atom edits. No builds
# or local reproduction, so a moderate budget is plenty.
timeout-minutes: 45

# ###############################################################
# Select a PAT from the pool and override COPILOT_GITHUB_TOKEN.
# Run agentic jobs in an isolated `copilot-pat-pool` environment.
#
# When org-level billing is available, this will be removed.
# See `shared/pat_pool.README.md` for more information.
# ###############################################################
imports:
  - uses: shared/pat_pool.md
    with:
      environment: copilot-pat-pool

environment: copilot-pat-pool

engine:
  id: copilot
  env:
     COPILOT_GITHUB_TOKEN: "${{ case( needs.pat_pool.outputs.pat_number == '0', secrets.COPILOT_PAT_0, needs.pat_pool.outputs.pat_number == '1', secrets.COPILOT_PAT_1, needs.pat_pool.outputs.pat_number == '2', secrets.COPILOT_PAT_2, needs.pat_pool.outputs.pat_number == '3', secrets.COPILOT_PAT_3, needs.pat_pool.outputs.pat_number == '4', secrets.COPILOT_PAT_4, needs.pat_pool.outputs.pat_number == '5', secrets.COPILOT_PAT_5, needs.pat_pool.outputs.pat_number == '6', secrets.COPILOT_PAT_6, needs.pat_pool.outputs.pat_number == '7', secrets.COPILOT_PAT_7, needs.pat_pool.outputs.pat_number == '8', secrets.COPILOT_PAT_8, needs.pat_pool.outputs.pat_number == '9', secrets.COPILOT_PAT_9, 'NO COPILOT PAT AVAILABLE') }}"
---

# Dreaming: curate the repository's "learning atoms" from the past week (scheduled weekly)

You are the **dreaming agent** for the **dotnet/msbuild** repository. "Dreaming" (a concept from
Claude Managed Agents) is a between-sessions process that reviews recent activity, extracts recurring
patterns a single session can't see, and curates the shared **learning atoms** so future agents and
contributors improve over time. Your job is to look back over the **past 7 days**, find the recurring
**misses** — mistakes reviewers keep pointing out, and common CI failures — and turn a *few* of them
into **small, durable learning atoms**, then open **up to three atomic pull requests** (one per distinct
recurring pattern) with those changes.

You are read-only against the repository's history and CI. The **only** thing you may modify is the
**learning atoms** (defined below), and only through the `create_pull_request` safe output.

## What "learning atoms" are in this repository

A **learning atom** is a single small, durable piece of agent-facing guidance — a bullet, a sentence,
a clarifying clause. (This is a local term for *this* workflow; it is **not** the same thing as GitHub
Copilot's separate "memory" feature — avoid conflating the two.) The learning atoms live in the set of
instruction files that Copilot and other agents load automatically:

- **`AGENTS.md`** — the primary, repo-wide agent instructions. `.github/copilot-instructions.md` is a
  symlink to it, so **edit `AGENTS.md`**, never the symlink. Use this for guidance that applies broadly
  across the whole repo.
- **`.github/instructions/*.instructions.md`** — path-scoped rules (each has an `applyTo:` glob in its
  front matter, e.g. `tests.instructions.md`, `tasks.instructions.md`, `evaluation.instructions.md`).
  Use these when a lesson only applies to a specific area of the codebase.
- **`.github/skills/*/SKILL.md`** — task-oriented skills (e.g. `running-unit-tests`,
  `reviewing-msbuild-code`, `changewaves`). Use these when a lesson refines *how to perform a specific
  task*.
- **`.github/agents/*.agent.md`** — agent personas (e.g. `expert-reviewer.agent.md`). Only touch these
  when a recurring review miss maps directly to that agent's checklist.

These are your only edit targets. Do **not** modify workflow files, product code, tests, or build
manifests.

## Guardrails (read before you start)

1. **Small bits only.** Every change must be a *few lines* — a bullet, a sentence, a clarifying clause.
   Never rewrite whole sections or add long new prose blocks. High signal, low volume.
2. **Prefer editing over adding.** Before adding anything, search the existing learning atoms for a
   related line and **refine it in place** (tighten, add a caveat, add an example). Only add a new bullet
   when no existing guidance is close enough to amend.
3. **No duplicates.** If the lesson is already captured anywhere in the learning atoms — even loosely, or
   in a different file — do **not** restate it. Deduplicate aggressively, including against other
   changes you make in the same run.
4. **Evidence-based.** Only encode a pattern you saw **recur** (roughly **2+ independent PRs/comments**
   in the window, or a clearly repeated CI failure category). One-off nits are not learning atoms. Cite
   the evidence in the PR body.
5. **Durable and general.** Encode lasting guidance, not transient facts (not "PR #123 broke X", not a
   specific person's preference on one PR, not a version number that will churn). No secrets, no
   contributor call-outs, no links to internal-only resources. This rule governs the **content of the
   learning atoms** (the edited files). It does **not** apply to the reviewer request in the PR *body* —
   naming the core-team reviewers there (Step 5b) is expected metadata, not atom content.
6. **Respect existing style.** Match the tone, formatting, and bullet style of the file you edit. Keep
   the `applyTo:` front matter and headings intact.
7. **Non-breaking.** These files steer agents, not builds, but still avoid guidance that would push
   contributors toward introducing new build warnings/errors or breaking changes.
8. **Cap the volume, keep each PR atomic.** At most **~5 distinct learning-atom changes total** across
   the run, even if you find more — pick the highest-signal, most-recurring ones. Split them into **up to
   3 PRs so each PR is atomic**: one PR per distinct, unrelated pattern. A single PR may bundle more than
   one change only when they belong to the **same theme** — even if that theme naturally spans two files
   (e.g. a `SKILL.md` refinement plus a matching `AGENTS.md` line). Unrelated patterns must go in separate
   PRs. It is completely fine to open only 1 PR with 1–2 changes.

## Step 1 — Gather the past week of activity

Use the pre-authenticated `gh` CLI (and the GitHub tools) to collect, for the **last 7 days**:

- **Pull requests** that were updated, merged, or closed in the window. For example:
  `gh pr list --state all --limit 100 --search "updated:>=<date-7d>" --json number,title,author,state,mergedAt,updatedAt,labels`
  (compute `<date-7d>` from today). Exclude bot-authored dependency PRs (`dotnet-maestro[bot]`,
  `dependabot[bot]`) — they carry no review lessons.
- **Review feedback and conversations** on those PRs: review threads, inline review comments, and
  issue-style PR comments. Focus on **human reviewer feedback**, especially:
  - Change requests and "please fix / nit / this should be…" comments.
  - Repeated corrections about the same thing across different PRs (naming, allocations/LINQ in hot
    paths, `is null` vs `== null`, Shouldly vs xUnit asserts, ChangeWave/opt-in gating for behavior
    changes, warnings-as-errors, cross-platform paths, missing tests, doc updates, etc.).
  - Points where an author (often an agent) clearly misunderstood a repo convention.
  For every reviewer/commenter you read, also record their **login**. You will use this in Step 5b to
  pick reviewers, where the authoritative filter for "core MSBuild team" is **membership in the
  `@dotnet/kitten` team** (see Step 5b) — not the coarse GitHub `authorAssociation`, which lumps in every
  dotnet-org member. So just collect the participant logins here; the team-membership check happens in
  Step 5b.
- **CI outcomes** on those PRs: use `gh pr checks <number>` (and check-run/status conclusions) to see
  which checks failed and cluster the **categories** of failure (e.g. formatting/editorconfig, a
  specific test project, build-warning-as-error, missing localization/resx, bootstrap issues). You are
  categorizing recurring *sources of misses*, not debugging individual runs — surface-level conclusions
  are enough; do not try to fetch deep external CI logs.

Aim for breadth over depth: sample enough PRs to see what **recurs**. If the GitHub search is rate-
limited or sparse, work with what you can retrieve rather than failing.

## Step 2 — Cluster into recurring misses

Group the raw feedback and CI failures into a small set of **themes**. For each theme, keep only those
that (a) recurred across 2+ PRs/comments or repeated CI failures, and (b) would plausibly have been
avoided if a learning atom had said something. Discard one-offs, subjective style debates, and anything
already well covered by existing learning atoms. For each surviving theme, also keep the list of
**participant logins** (from Step 1) who raised or discussed it — this feeds the per-PR reviewer
selection in Step 5b, which narrows them to the core `@dotnet/kitten` team.

## Step 3 — Decide the minimal learning-atom change per theme

For each surviving theme, in order of preference:

1. **Amend existing guidance.** Grep the existing learning atoms for the topic. If a related line
   exists, make a minimal edit to it (add a caveat/example/clarifying clause) in the most specific file
   that already owns the topic.
2. **Add a single small bullet** to the most relevant *existing* file (path-scoped instruction, skill,
   or `AGENTS.md` for repo-wide lessons) only if nothing is close enough to amend.
3. **Do not create new files** unless a recurring theme genuinely has no home anywhere; strongly prefer
   fitting into an existing instruction/skill. (If you truly must, a new `.github/instructions/*.instructions.md`
   with a correct `applyTo:` glob is the right shape — but treat this as a last resort.)

Re-check the **no-duplicates** and **small-bits** rules against everything you're about to write,
including your own other edits.

## Step 4 — Apply the edits and self-verify

Because each PR is created from its **own git branch**, keep unrelated patterns on separate branches so
they stay atomic. For **each** pattern you're turning into a PR:

1. Refresh `main` and branch from it, so the branch is built on the latest base regardless of workspace
   state (e.g. `git fetch origin main && git checkout -B dreaming/<short-topic> origin/main`).
2. Make just that pattern's edit(s) with the `edit` tool and commit them on that branch.
3. Then move to the next pattern's branch (again `git fetch origin main` + branch from `origin/main`) so
   its changes don't pile onto the previous one.

After staging each branch, run `git --no-pager diff main...HEAD` (or `git --no-pager diff` before
committing) and confirm:
- Only files under `AGENTS.md`, `.github/instructions/**`, `.github/skills/**`, or `.github/agents/**`
  changed. Nothing else.
- The diff is small (a handful of lines total), adds no duplicate guidance, and preserves each file's
  front matter/headings/style.
- Each branch carries **only its own** pattern's changes (no cross-contamination between PRs).
If any check fails, fix or drop the offending change before proceeding.

## Step 5 — Open the pull request(s) (or noop)

- If you have well-justified changes, emit a `create_pull_request` safe output **per distinct pattern**,
  up to **3** total, each with its `branch` set to that pattern's branch from Step 4. **Keep each PR
  atomic**: one theme per PR, on its own branch (a PR may carry more than one change only when they are
  the same theme, even if it spans two files — see rule 8). Unrelated patterns must be separate PRs. For
  **each** PR, write a body that:
  - For every learning-atom change it carries, states:
    - **What** changed and in which file (edit vs. small addition).
    - **Why** — the recurring evidence: cite the PR numbers / comment themes / CI failure category that
      prompted it (2+ occurrences). Keep it concise.
    - A one-line note confirming you checked it isn't already covered elsewhere.
  - **Ends with the `## Reviewers (core MSBuild team)` section** from Step 5b (always include it).
  Open each PR **ready for review** (not draft) so a maintainer can approve or adjust. A human merges it —
  you never self-merge.
- If **nothing** clears the bar this week (quiet week, or every recurring pattern is already captured),
  emit a **`noop`** explaining briefly what you looked at and why no learning-atom change was warranted.
  Do **not** open an empty or speculative PR.

## Step 5b — Name reviewers to notify (core MSBuild team, max 2 per PR)

For **each** PR you open, identify the people who were actually involved in the discussions behind that
PR's pattern (from the participant list you kept in Step 2) and @-mention them so a maintainer can assign
them:

1. **Fetch the core team roster once** for the run and keep the exact output:
   `gh api orgs/dotnet/teams/kitten/members --jq '.[].login'` (the `@dotnet/kitten` team is the MSBuild
   CODEOWNERS team — a small, curated set, currently ~8 people, *not* the whole org). A login qualifies
   as a reviewer **only if it satisfies BOTH conditions**: (a) it is in the participant list you actually
   collected in Steps 1–2 for *this* pattern, **and** (b) it appears verbatim in the roster output above.
   Drop everyone who fails either check, plus bots and the PR authors themselves. **Never** add a name
   from memory, from general knowledge of "who works on .NET/MSBuild", or because it seems plausible — if
   a login is not in both lists, it does not go in the PR. Do **not** use GitHub's `authorAssociation`
   for this — `MEMBER` means any dotnet-org member and is far too broad. If the token can't read team
   membership (the call errors/returns empty), skip individual selection and use the `@dotnet/kitten`
   fallback in step 2.
2. **Cap at 2.** If more than two qualify, pick the two most engaged in that pattern's threads (most
   comments / the ones who requested the changes). If exactly one qualifies, name one. If **none**
   qualify, fall back to the core-team handle `@dotnet/kitten` (verified team slug `kitten`).
3. The safe-outputs channel exposes no per-PR reviewer field, so you **cannot** create a formal GitHub
   review request. Instead, record the chosen people **in that PR's body** as its own final section. An
   `@`-mention here is a **notification/ping for manual assignment**, not an auto-assigned review — a
   maintainer still clicks "Reviewers" to formally request them:

   ```
   ## Reviewers (core MSBuild team)
   Suggested reviewers — core-team members who discussed this pattern (please assign): @login1 @login2
   ```

   Use real `@`-mentions (or `@dotnet/kitten` for the fallback). This is the one place contributor names
   belong — never put them in the learning-atom content itself (guardrail 5).

**Before emitting each PR, self-check the reviewers line:** every `@`-mention in it (other than the
`@dotnet/kitten` fallback) must be a login that appears in the `orgs/dotnet/teams/kitten/members` output
you fetched **and** in that pattern's discussion-participant list. If any name fails, remove it. When in
doubt, prefer the `@dotnet/kitten` fallback over guessing an individual.

## Reminders

- You may only change the four learning-atom targets; the `allowed-files` allowlist enforces this, but
  you should also self-check with `git diff`.
- Fewer, sharper learning atoms beat many shallow ones. When in doubt, leave it out.
- Never encode secrets, credentials, individual contributor names, or transient/one-off facts **into the
  learning-atom content**. (Naming the core-team reviewers in the PR body, per Step 5b, is the sole
  exception — that is review metadata, not atom content.)
- Keep each PR atomic and name at most **2** core-team reviewers to notify per PR (Step 5b).
