---
name: changewaves
description: 'Manage MSBuild Change Waves: create new waves, condition features behind opt-out flags, write tests for wave-gated features, document change waves in ChangeWaves.md, and retire expired waves. Use when adding changes that need an opt-out or rotating out old change waves. Changes that introduce a user-visible behavior change should consider whether to use a changewave.'
argument-hint: 'Add, query, or remove changewaves and changewave checks.'
---

# Managing MSBuild Change Waves

A Change Wave is an opt-out flag that groups risky features together. Users disable features by setting the environment variable `MSBUILDDISABLEFEATURESFROMVERSION` to the wave version. This skill covers the full lifecycle: creating a wave, conditioning code on it, testing, documenting, and retiring.


## Decide Whether a Change Wave Is Appropriate

Use a change wave when a change is valuable but has meaningful compatibility risk for existing builds.

Good candidates:
- User-visible behavior changes that may regress some build graphs.
- Changes in parsing/evaluation/execution semantics where real-world usage is broad or hard to predict.
- Changes that may require customer adaptation time and benefit from a temporary opt-out.

Usually avoid a change wave for:
- Pure bug fixes that restore intended existing behavior with low compatibility risk.
- Internal refactoring/perf work with no externally observable behavior change.
- New functionality that is already explicitly opt-in (for example, gated by a new property, switch, or API surface).

If uncertain, default to safety:
1. Assess blast radius and rollback difficulty.
2. Check whether the change could be experienced as a breaking change in production builds.
3. Use a wave if an opt-out is likely needed while customers adapt.

Document the decision in PR notes so reviewers can validate the call.

If a changewave is appropriate, consult [ChangeWaves-Dev.md](../../../documentation/wiki/ChangeWaves-Dev.md) (developer-facing) and [ChangeWaves.md](../../../documentation/wiki/ChangeWaves.md) (public-facing) for details on how to follow the below steps.

## Step 1: Determine the Correct Wave Version

Look up the current MSBuild version. If a wave already exists for that version in `src/Framework/ChangeWaves.cs`, use it. Otherwise, create a new one.

## Step 2: Condition Your Feature on the Wave

C# and MSBuild examples are in [ChangeWaves-Dev.md](../../../documentation/wiki/ChangeWaves-Dev.md). Prefer to put checks inline rather than abstracting out a method to host the check.

## Step 3: Write Tests

Write normal tests for the new behavior. Then add at least one test that verifies the old behavior is **preserved** when the wave is opted out.

## Step 4: Document the Feature

Add an entry to the **Current Rotation of Change Waves** section in [ChangeWaves.md](../../../documentation/wiki/ChangeWaves.md).

- If a heading for this wave already exists, add a bullet under it.
- If not, add a new `### {Major}.{Minor}` heading at the top of the current rotation list.

Each entry is a bullet with a link to the PR:

```markdown
### 18.6
- [Short description of the change.](https://github.com/dotnet/msbuild/pull/NNNNN)
```

## Only when explicitly requested: Retire an Expired Wave

Waves rotate out when a new major .NET version will be released. When retiring a wave, follow the detailed description in [ChangeWaves-Dev.md](../../../documentation/wiki/ChangeWaves-Dev.md).

## Checklist

- [ ] Decision made: change wave needed?
- [ ] Correct wave version chosen from `eng/Versions.props`
- [ ] Wave field and `AllWaves` entry added in `ChangeWaves.cs` (if new wave)
- [ ] Feature code wrapped with `AreFeaturesEnabled`
- [ ] Test verifying opt-out disables the feature
- [ ] Entry added to `documentation/wiki/ChangeWaves.md`
