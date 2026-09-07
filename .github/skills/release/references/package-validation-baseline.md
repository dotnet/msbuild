# Package validation baseline provenance

Use for the Phase 3 main-version bump or an explicit baseline investigation.
This procedure is read-only until changing the baseline is authorized.

## Required evidence

The intended baseline is an MSBuild package from the release's API surface that:

1. Is published on the feed used by package validation.
2. Was produced from a commit reachable from the selected release branch, including its branch-point commit.

Read `VersionPrefix`, `PreReleaseVersionLabel`, and
`PackageValidationBaselineVersion` from the relevant
[Versions.props](../../../../eng/Versions.props), and inspect
[NuGet.config](../../../../NuGet.config) and the official build's publishing
configuration. The public [dotnet-tools feed](https://dev.azure.com/dnceng/public/_artifacts/feed/dotnet-tools)
is the current discovery starting point, not a substitute for verifying availability.

The common prerelease shape is
`<VersionPrefix>-<PreReleaseVersionLabel>.<shortDate>.<revision>`. A historical
example is `18.11.0-1.26426.2`; do not freeze that version or label in future steps.

## Why apparently obvious baselines can be wrong

| Candidate | Risk |
|---|---|
| A stable package version shipped to nuget.org or VS | Since dotnet/msbuild#14277, release CI and repacked publication versions differ. A stable publication version may not exist on the validation feed. Check the actual package/version instead of assuming availability. |
| The newest similarly branded package on the feed | Main can keep the outgoing version until the main-bump PR lands. A later main commit may contain next-version APIs but still have the outgoing branding. |
| An older published candidate while the intended branch-point build is still publishing | This can omit release APIs. Establish whether a newer intended candidate is pending publication before substituting an older one. |

## Helper and its limits

Inspect [Get-PackageValidationBaseline.ps1](../../../../scripts/Get-PackageValidationBaseline.ps1)
before running it. It reads Git refs, Azure DevOps builds, and feed metadata using
existing access; it must not trigger a build or change authentication.

```powershell
pwsh -NoProfile -File .\scripts\Get-PackageValidationBaseline.ps1 -ThisReleaseVersion $releaseVersion -MainRef $mainRef -ReleaseRef $releaseRef
if ($LASTEXITCODE -ne 0) { throw "Baseline discovery failed." }
```

Resolve refs against the intended upstream repository; do not assume `origin` is
upstream in a fork. Confirm the current official pipeline identity (historically
MSBuild, definition 9434 in DevDiv).

The current helper has important limitations:

- It queries only a recent window of 50 builds per branch, so it can miss an old branch-point build.
- Feed-version enumeration does not establish complete pagination.
- Candidate sorting uses strings, not full NuGet version ordering; numeric prerelease revisions such as `.9` and `.10` require care.
- It can select an older published candidate when a newer candidate is not yet on the feed.

Do not describe this output as a deterministic proof of the latest eligible
baseline. Inspect candidate SHAs, build numbers, publication state, and coverage.
If the required evidence is absent, widen the read-only search or report a blocker.

## Read-only manual reconciliation

1. Establish the release branch and branch point from verified refs. Check reachability of each candidate commit, not just its version string.
2. Query successful builds for that release branch and the branch-point commit. Follow the service's continuation tokens/time windows; constrain repository and definition.
3. Inspect package artifacts or manifests for the actual package version. The Arcade short-date convention (`year-within-century * 1000 + month * 50 + day`) helps interpret an OfficialBuildId, but an artifact is stronger evidence than reconstructing a version from a guessed label.
4. Check the exact package/version on the configured feed, paging through version results when necessary. Record the build URL, source SHA, version, and feed evidence.
5. Compare eligible versions with NuGet version semantics and account for pending publication. Resolve discrepancies before editing `PackageValidationBaselineVersion`.

Package validation failures are not permission to accept a newer main baseline
or generate suppressions indiscriminately. See the current
[public API release procedure](../../../../documentation/release.md#public-api).
