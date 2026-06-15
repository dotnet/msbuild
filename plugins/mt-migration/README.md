# mt-migration plugin

A GitHub Copilot CLI plugin that bundles the MSBuild **multithreaded task migration** playbook plus an MT-specific PR reviewer.

Use this on any repository that authors MSBuild tasks consuming the
`IMultiThreadableTask` / `TaskEnvironment` / `[MSBuildMultiThreadableTask]` API
surface from `Microsoft.Build.Framework` — currently `dotnet/msbuild`,
`dotnet/sdk`, and downstream task assemblies (NuGet, EF, ASP.NET Core, etc.).

## Contents

```
mt-migration/
├── plugin.json
├── skills/
│   └── multithreaded-task-migration/
│       └── SKILL.md         # The 8 deadly sins, ToolTask hazards, helper patterns, test patterns, sign-off checklist
└── agents/
    └── mt-migration-reviewer.agent.md   # MT-specific PR reviewer that follows call chains end-to-end
```

## What it does

| Component | When invoked | What it does |
|---|---|---|
| `multithreaded-task-migration` skill | Author migrating a task | Step-by-step migration recipe + compatibility red-team checklist. Distilled from ~50 merged migration PRs across `dotnet/msbuild` and `dotnet/sdk`. |
| `mt-migration-reviewer` agent | Reviewing an MT migration PR | Delegates the 24-dimension general review to the host repo's expert reviewer (if any), then layers MT-specific findings on top. Mandatorily traces every call chain from `Execute()` to leaves and reports hazards by chain, not by file. Verifies tests are not theater. |

The reviewer is deliberately complementary to a generic code reviewer: it does
not re-explain migration steps, does not redo style/perf/naming review, and
will not flag a clean attribute-only migration as needing a concurrency test if
the call-chain audit comes back clean.

## Installing

### As a local Copilot CLI plugin (from a clone of this repo)

```sh
# from a Copilot CLI session
/plugin install file://$(pwd)/plugins/mt-migration
```

### Across multiple repos (recommended)

Publish this plugin folder to a small standalone repo (e.g.
`your-org/mt-migration-plugin`) and install from there. The plugin contains no
hard-coded references to `dotnet/msbuild` paths — only to the public
`Microsoft.Build.Framework` API surface — so it works on any task-authoring
repo unchanged.

```sh
/plugin install github:your-org/mt-migration-plugin
```

## Relationship to the in-repo skill

This plugin is the **single, canonical** home of the MT migration skill. It is not duplicated under `.github/skills/` in `dotnet/msbuild` — install the plugin to use it locally. The skill-validator workflow in `dotnet/msbuild` only scans `.github/skills/` and `.github/agents/`, so the plugin lives outside that scope and is not subject to it.

## Updating the skill from PR experience

The skill's "deadly sins" and call-chain hazard table grow as new
defect classes are found in merged migrations. To contribute:

1. Find a defect that was caught in PR review and would have been missed by
   the existing skill.
2. Add a sin / hazard / pattern with a real-world citation (PR + line range).
3. Update `skills/multithreaded-task-migration/SKILL.md`.
4. Keep the agent file slim — it should keep delegating to the skill rather
   than absorbing the rules itself.
