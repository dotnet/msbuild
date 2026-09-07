# ChangeWave lifecycle

## Select and register

The authoritative inputs are [eng/Versions.props](../../../../eng/Versions.props), [ChangeWaves.cs](../../../../src/Framework/ChangeWaves.cs), and the feature's intended release.

Use the named static `Version` field for that release. If it is absent, add it and insert it into `AllWaves` in ascending order. `LowestWave`, `HighestWave`, clamping, and rounding depend on that ordering.

Do not gate a shipped feature against `HighestWave`: its meaning moves when another wave is added. Prefer a direct check at the behavior boundary; introduce an abstraction only when it genuinely owns a shared decision.

[ChangeWaves-Dev.md](../../../../documentation/wiki/ChangeWaves-Dev.md) describes the lifecycle, but its historical example versions and test snippets are not the current registry. Use current implementation/tests when choosing fields and assertions.

## Gate the behavior

C# callers use `ChangeWaves.AreFeaturesEnabled` with the selected named field. Tasks/targets can use the `[MSBuild]::AreFeaturesEnabled` property function with the selected version. Keep the previous behavior available when the wave is disabled.

For an existing inline use, see [MSBuildGlob](../../../../src/Build/Globbing/MSBuildGlob.cs). For property-function use and expected results, see [ChangeWaves_Tests](../../../../src/Build.UnitTests/ChangeWaves_Tests.cs).

The environment variable is `MSBUILDDISABLEFEATURESFROMVERSION`. It disables that wave **and later waves**, not just one feature.

| Input | Resolution |
|---|---|
| Unset or empty | All waves enabled |
| Current named wave | Disable that wave and later waves |
| Version between current waves | Round up to the next current wave |
| Version outside the current rotation | Clamp to the boundary; the caller can report the conversion warning |
| Invalid version format | Enable all waves; the caller can report the conversion warning |
| The registry's `EnableAllFeatures` sentinel | Enable all waves |

Use `DisabledWave` and `ConversionState` from the implementation rather than duplicating this parsing algorithm in a caller.

## Process lifetime and tests

`ApplyChangeWave` caches the resolved value. Changing the environment variable in an already-running process does not by itself establish a new test condition.

Given the selected `Version wave` in a test, the existing reset pattern is:

```csharp
using TestEnvironment env = TestEnvironment.Create();

ChangeWaves.ResetStateForTests();
env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", wave.ToString());
BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();
```

See the private `SetChangeWave` helper in [ChangeWaves_Tests](../../../../src/Build.UnitTests/ChangeWaves_Tests.cs). It is **not** a public method on `TestEnvironment`. [TestEnvironment cleanup](../../../../src/UnitTests.Shared/TestEnvironment.cs) restores test state and resets the wave/build-environment caches.

Exercise the actual feature in both states and assert opposite behavior where appropriate. The wave tests' `buildSimpleProjectAndValidateChangeWave` derives whether the target should run from the resolved wave; it does not always expect the message to appear.

For a feature that builds a project, use an appropriate existing fixture and logger, with the [test isolation instructions](../../../instructions/tests.instructions.md) and [test-runner workflow](../../running-unit-tests/SKILL.md). A cache reset without a feature assertion is not opt-out coverage.

Reused processes also matter outside tests. The [user-facing wave documentation](../../../../documentation/wiki/ChangeWaves.md) explains worker/server handshake behavior and the deliberately different task-host case. Do not assume changing the parent's environment reconfigures every already-running child.

## Document

Add a concise feature bullet under the selected version in **Current Rotation of Change Waves** in [ChangeWaves.md](../../../../documentation/wiki/ChangeWaves.md). Reuse its heading, or add the new release heading in the existing ordering. Link the actual change when a PR exists; do not publish a placeholder PR URL as a real reference.

Explain the compatibility reason and observable old/new behavior where needed. A wave is temporary risk mitigation, not a guarantee that a default-on behavior change cannot break a consumer.

## Retire only when requested

Consult current release policy and the eligibility/rotation description in [ChangeWaves.md](../../../../documentation/wiki/ChangeWaves.md). Do not infer authorization to retire from a date or from noticing an old field.

For an authorized retirement:

1. Remove the selected registry field and `AllWaves` entry.
2. Find C# and property-function gates for that wave.
3. Keep the intended permanent behavior and remove the obsolete branch.
4. Remove only opt-out-specific tests; retain regression coverage for permanent behavior.
5. Remove newly dead helpers/usings and update the rotation documentation.
6. Cover affected behavior and configuration boundaries rather than mechanically deleting every similarly named construct.

Wave retirement is not authorization to remove public APIs or invent a general two-major-version deprecation policy.
