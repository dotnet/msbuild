# SDK PR Guide

## Branching & Release Schedules
See the [versions](https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs#lifecycle) document for more details on how SDK versions map to MSBuild and Visual Studio. The Visual Studio schedule is available on the devdiv internal Azure DevOps wiki and the .NET schedule is communicated through the internal .NET Tactics alias.

### Major releases
The .NET SDK has yearly major releases aligning with the .NET Runtime in November.  These releases will be of the form `N.0.1xx`. The feature work for major releases will be done in `main`. 

Typically, previews for each major release start in February of each year.  For each preview, we will snap a new branch off of main approximately midway through the prior month.

During .NET 7, for example, the preview branches were created around the 21st of each month prior, with the later previews forking earlier to provide more time for stabilization and codeflow.
The `main` branch's major version will be set to match the next year's major release in August. (For example, the switch from `.NET 7` to `.NET 8` occurred in August of 2022, while .NET 8 was set for release in 2023.) Warning: This process can take a bit for those waiting to develop features for the next major release. In prior releases it has taken 1-3 months before the new branch could be fully used to target the new .NET Runtime version.

### Minor releases
The .NET SDK has quarterly minor releases that align with Visual Studio releases. For example, two different quarterly releases in the same schedule would be: `7.0.200` vs `7.0.300`. These SDK releases are open to all bug fixes and some features work as well. 
These quarterly releases should avoid breaking changes if at all possible. Breaking changes instead should be included in the major releases and be tied to customer `TargetFramework` updates when possible. We do this to limit the pain of breaking changes, as customers need to take action (upgrading the `TargetFramework`) before they are broken. 

Minor releases lock down after preview 3 for each release.  Preview 3 is typically 1 month prior to GA and locks down by the end of the month before the preview release.

#### Transition from minor release to servicing
After the minor release locks down, it transitions into servicing / "QB" (Quarterback) mode. This typically happens around the `preview 3` release to allow time for any late public changes we want feedback on. The next release after `preview 3` is typically GA and done as a servicing release. The following steps are done when transitioning from minor release previews to servicing:
- [ ] Follow the new branch check list for creating a new minor release (steps listed on internal OneNote)
- [ ] Set [`StabilizePackageVersion`](https://github.com/dotnet/sdk/blob/main/eng/Versions.props#L18) to true
- [ ] Enable internal runtime `darc` subscriptions and trigger them

### Servicing releases
The .NET SDK has monthly servicing releases (e.g.`7.0.100` VS `7.0.101`) aligning with the .NET Runtime servicing releases. These are for top fixes and security updates only to limit risk.
Any servicing release is open for check-ins from the day the [branding PRs](https://github.com/dotnet/sdk/pulls?q=is%3Apr+branding) are merged (~1st of each month) and when code complete is (typically two weeks later).
The servicing branches are locked from the time of code complete to the next branding in case we need to respin any monthly release. Final signoffs are typically in the last week of each month.

### Schedule
| Release Type | Frequency    | Lockdown Release  | Branch Open | Lockdown Date (estimate) |
| -------------|--------------|-------------------|-------------|--------------------------|
| Major        | Yearly (Nov) | RC2               | ~August     | Mid-September            |
| Minor        | Quarterly    | Preview 3         | ~Prior release Preview 3 date | End of the month prior to Preview 3 (~7 weeks prior to release) |
| Servicing    | Monthly      | N/A               | After branding, ~1st of the month | Third Tuesday of prior month (signoff is ~28th of each month) |

### Tactics approval
Even releases that are in lockdown can still take changes as long as they are approved and the final build isn't complete. To bring a change through tactics, mark it with the label servicing-consider and update the description to include 5 sections (Description, Customer Impact, Regression?, Risk, Testing). See previously approved bugs for examples by looking for the [servicing-approved](https://github.com/dotnet/sdk/pulls?q=is%3Apr+label%3AServicing-approved+is%3Aclosed) label.

## External contributions
External contributions are encouraged and welcome. There are so many teams working in this repo that it can be hard to track. Contributors looking to learn more about how to contribute should check out the [Developer Guide](https://github.com/dotnet/sdk/blob/main/documentation/project-docs/developer-guide.md).

- Ping `@dotnet/domestic-cat` when ready for a review
- Make sure all tests and checks are passing
- Make sure to add a new test for the scenario (when it makes sense)

Triage and PR review meetings are Wednesdays each week. If your PR is passing checks and has been reviewed by then, that's when we typically merge.

## Codeflow
Codeflow is handled by [darc](https://github.com/dotnet/arcade/blob/main/Documentation/Darc.md). Codeflow comes from three runtime branches and a dozen tools branches.
`@dotnet/domestic-cat` will monitor codeflow and approve passing PRs (as all PRs require 1 approver) and ping the owning team for any failures.

## Internal Builds

The .NET SDK [Installer](github.com/dotnet/installer) is what allows the .NET SDK to be installed on a machine for usage in a proper developer environment, rather than for developing the .NET SDK itself. The installer has nightly builds that align with the .NET SDK which you can find [here](https://github.com/dotnet/installer#installers-and-binaries).
The number of the build may be of particular interest. The build numbers match the internal build numbers with some caveats. YYYY MM DD are squashed to 5 digits. The first 2 digits represent the final 2 digits of the year. The next 3 digits represent the month and year: 50*The_Month_Number + the day of the month. The final suffix after the decimal value is the number of the build for that given day. 

Example:
`23080.2` is 2023, January 30th (1*50) + 30, and the second internal build of the SDK for that day. 

## Internal Branching
All monthly servicing releases are done of our internal branches in case there are security changes that have to go out. That means all flows from runtime branches into the SDK and from SDK to installer are done internally.
That is why we have removed all servicing builds from the installer main page as those builds do not include any changes from any repo other than the installer repo so are very limited.
Internal codeflow is merged into public GitHub repos on patch Tuesday each month to ensure we are updated.

