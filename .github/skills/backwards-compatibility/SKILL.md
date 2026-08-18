---
name: assessing-breaking-changes
description: 'Guides assessment of backward compatibility for MSBuild changes. Consult when modifying behavior, adding warnings or errors, changing defaults, altering target ordering, removing or deprecating features, deciding whether a change needs a ChangeWave, reviewing blast radius of behavioral changes, or when a PR introduces user-visible output differences.'
argument-hint: 'Describe the change and its potential compatibility impact.'
---

# Backward Compatibility in MSBuild

Backward compatibility is the default — any change that could alter existing build behavior must be explicitly justified.

This skill covers **how to evaluate compatibility risk**. For the mechanics of ChangeWave implementation, see the [changewaves skill](../changewaves/SKILL.md).

## Core Philosophy

1. **Existing builds must not break.** If a project built successfully yesterday, it must build successfully today with identical semantics.
2. **New warnings are breaking changes.** Builds that use `-WarnAsError` or `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` will fail if you introduce a new warning. Always gate new warnings behind a ChangeWave or emit them as `Message` importance instead.
3. **Output changes are behavioral changes.** Even "improvements" to output formatting, file paths, or diagnostic text can break downstream consumers that parse MSBuild output.
4. **Removal is nearly impossible.** Never remove CLI switches, public APIs, or property names. Deprecate with warnings first, then gate removal behind a ChangeWave after multiple release cycles.

## Blast Radius Checklist

Before merging any behavioral change, evaluate:

| Question | If Yes |
|----------|--------|
| Does this change what gets built or how? | ChangeWave required |
| Does this add a new warning? | ChangeWave required (WarnAsError impact) |
| Does this change a property default? | ChangeWave required (existing .csproj files depend on defaults) |
| Does this alter target execution order? | Test with real-world solutions; likely ChangeWave |
| Does this change console or binlog output format? | Consider downstream tool impact |
| Does this affect only internal code paths with no user-visible effect? | No ChangeWave needed |
| Is this a pure bug fix restoring documented behavior? | Usually no ChangeWave; use judgment on blast radius |

## When ChangeWave Is NOT Needed

- Internal refactoring with no observable behavior change
- Performance improvements that don't change semantics
- New opt-in features gated by a new property or switch
- Bug fixes that restore clearly-intended behavior with limited blast radius

For detailed ChangeWave mechanics, see [ChangeWaves-Dev.md](../../../documentation/wiki/ChangeWaves-Dev.md) and [ChangeWaves.md](../../../documentation/wiki/ChangeWaves.md).

## The Warnings-as-Errors Rule

This is the most commonly missed compatibility concern:

```xml
<!-- Many enterprise builds set this globally -->
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
```

Any new `MSBxxxx` warning you add will **break these builds**. Options:
1. **Gate behind ChangeWave** — preferred for genuinely important warnings
2. **Use `MessageImportance.Low` or `Normal`** — for informational diagnostics
3. **Add as an error from the start** — if the condition is always wrong

## Deprecation Protocol

1. Add a deprecation warning (behind ChangeWave if broad impact)
2. Document the deprecation in release notes
3. Maintain the old behavior for at least two major .NET versions
4. Only remove after the ChangeWave has rotated out

## Compatibility Test Matrix

When testing backward compatibility, verify:

- **Multi-targeting projects** — `<TargetFrameworks>net472;net8.0</TargetFrameworks>`
- **Solution builds with mixed project types** — C#, F#, VB, C++/CLI
- **Incremental builds** — second build should be a no-op
- **Design-time builds** — Visual Studio calls different target contracts
- **Cross-platform** — path separator differences, case sensitivity on Linux
- **WarnAsError builds** — explicitly test with `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`

## Decision Framework

```
Is the change user-visible?
├── No → Ship it (no ChangeWave needed)
└── Yes
    ├── Is it a new opt-in feature? → Ship it (no ChangeWave needed)
    └── Does it change existing behavior?
        ├── Bug fix with low blast radius? → Ship it, add regression test
        └── Behavioral change or new warning?
            └── Gate behind ChangeWave, test opt-out path
```

Document the compatibility decision in your PR description so reviewers can validate it.
