# DARC configuration, flow rotation, and branch retirement

Load for the relevant Phase 1/2 task, not every release query. Read-only discovery
does not authorize configuration changes, branch deletion, or PR publication.

## Discover current topology

Use currently available Maestro tools after schema discovery, or version-checked
DARC `get-*` commands. Record repository/branch, channel, subscription ID,
source-enabled settings, and the observation date. A stale cached result or
historical channel ID is insufficient for a destructive decision.

For CLI writes, inspect each command's current help first. A missing or outdated
DARC installation is a blocker to report, not permission to run initialization
or change credentials.

## Batch authorized configuration changes

DARC configuration writes target
[maestro-configuration](https://dev.azure.com/dnceng/internal/_git/maestro-configuration).
With a CLI version supporting this workflow:

1. Choose an owned configuration branch for the requested phase.
2. Use `--configuration-branch` on the selected writes and `--no-pr` until the final write.
3. Omit `--no-pr` on that final authorized write to create one PR. Do not repeat an already applied mutation just to create a PR.
4. Read back the proposed diff and resulting configuration; review and merge are distinct authorized actions.

Some commands prompt when a target branch does not exist, and
`delete-subscriptions` has a confirmation prompt. Where supported, `-q` prevents
an authorized operation from hanging in a noninteractive session; it is not
authorization itself. Verify the verb and flags with the installed CLI.

`add-channel` is not idempotent: it rejects existing channels. Query first and
skip a change only if existing configuration matches the intended result. On
conflicts or partially applied batches, reconcile the branch and service state.
Do not force-push over shared configuration work.

## Phase 2: what moves and what stays

Retarget outgoing MSBuild forward-flow subscriptions only when their **target
branch is main**. Typical consumers include the VMR and F#, but discover the
actual current set rather than treating those examples as exhaustive.

Do not automatically retarget `dotnet/dotnet @ release/*` subscriptions,
including preview bands. They may be the downstream consumers that the new
MSBuild release branch must continue to feed.

Backflow is a separate direction: `dotnet/dotnet -> msbuild`, source-enabled.
If the new MSBuild release is paired with an SDK band that main was feeding:

1. Resolve main's current backflow subscription and the next/outgoing SDK band channels.
2. Retarget main's backflow to the next band only when that channel actually changed.
3. Add the outgoing-band backflow into the new release branch, preserving the intended source directory, merge policy, cadence, and excluded assets from verified configuration.

Skip SDK-band backflow rotation for a VS-only release. The checklist's
`source-directory msbuild`, `everyDay`, Standard merge, and excluded-assets `*`
are configuration starting points to compare with current policy, not permanent
truth for every subscription.

## Phase 1.3: supported branches and retirement

When a full retirement audit is requested, enumerate all live release branches,
default-channel associations, and inbound/outbound subscriptions. A branch named
three releases earlier is only a possible candidate, not a lifecycle decision.

Use both sources, recording their current dates and applicability:

| Lifecycle | Source |
|---|---|
| SDK feature band and corresponding VS version | [Supported .NET versions](https://learn.microsoft.com/dotnet/core/porting/versioning-sdk-msbuild-vs#supported-net-versions) |
| Visual Studio release/LTSC support | The servicing page for the applicable VS generation, such as [VS 2026](https://learn.microsoft.com/visualstudio/releases/2026/servicing-vs), and the product lifecycle entry |

A branch serving both lifecycles can retire only when neither still requires
support. SDK support may outlast the associated VS LTSC, or vice versa. Long-lived
historical mappings such as VS 2022 / `vs17.14`, VS 2019 / `vs16.11`, and VS 2017 /
`vs15.9` merit explicit inspection; do not freeze their EOL dates in this guide.

A Maestro channel can outlive its SDK band. Its presence is not proof of support.
Likewise, missing outbound subscriptions are a reason to investigate configuration
and other consumers, not proof that the branch can be deleted.

An association to a nonexistent branch may be an orphan, but can also be an
intentional pre-created next-release mapping. Check the tracking issue and
configuration history before removing it.

Record branch, paired band, SDK support evidence, VS support evidence, consumers,
and a keep/retire/unknown decision. Unknown evidence blocks retirement; it must
not default to deleting subscriptions.

## Historical lessons, not current lifecycle facts

The 18.9 rotation required correction after servicing/preview consumers were
moved with main. During the 18.11 cycle, an older SDK-coupled branch remained
needed while a newer band had expired, and deleted branches left associations.
Retain those failure patterns when auditing, but re-query current topology and
support sources instead of reusing the historical branch verdicts.
