# SDK PR Guide

## Branching
See the [versions](https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs#lifecycle) document for more details on how SDK versions map to MSBuild and Visual Studio.

### Major releases
The .NET SDK has yearly major releases aligning with the .NET Runtime in November.  These releases will be of the form N.0.1xx. The feature work for major releases will be done in `main`. 
Typically, previews for each major release start in February of each year.  For each preview, we will snap a new branch off of main approximately midway through the prior month.
For .NET 7 for example, the preview branches were created typically around the 21st of each month prior with the later previews forking earlier to provide more time for stabilization and codeflow.
Main branch has moved to be the next year's release branch in August.

### Minor releases
The .NET SDK has quarterly minor releases that align with Visual Studio releases. These SDK releases are open to all bug fixes and some features work as well. 
These quarterly releases should avoid breaking changes if at all possible. Breaking changes instead should be included in the major releases and be tied to customer TargetFramework updates when possible.

Minor releases lock down after preview 3 for each release.  Preview 3 is typically 1 month prior to GA and locks down by the end of the month before the preview release.

### Servicing releases
The .NET SDK has monthly servicing releases aligning with the .NET Runtime servicing releases. These are for top fixes and security updates only to limit risk.
Any servicing release is open for checkins from the day the branding PRs are merged (~1st of each month) and when code complete is (typically two weeks later).
The servicing branches are locked from the time of code complete to the next branding in case we need to respin any monthly release. Final signoffs are typically in the last week of each month.

### Schedule
| Release Type | Frequency    | Lockdown Release  | Branch Open | Lockdown Date (estimate) |
| -------------|--------------|-------------------|-------------|--------------------------|
| Major        | Yearly (Nov) | RC2               | ~August     | Mid-September            |
| Minor        | Quarterly    | Preview 3         | ~Prior release Preview 3 date | End of the month prior to Preview 3 (~7 weeks prior to release) |
| Servicing    | Monthly      | N/A               | After branding, ~1st of the month | Third Tuesday of prior month (signoff is ~28th of each month) |

### Tactics approval
Even releases that are in lockdown can still take changes as long as they are approved and the final build isn't complete. To bring a change through tactics, mark it with the label servicing-consider and update the description to include 5 sections (Description, Customer Impact, Regression?, Risk, Testing). See previously approved bugs for examples by looking for the [servicing-approved](https://github.com/dotnet/sdk/pulls?q=is%3Apr+label%3AServicing-approved+is%3Aclosed) label

## External contributions
External contributions are encouraged and welcome. There are so many teams working in this repo that it can be hard to track.

- Ping `@dotnet/domestic-cat` when ready for a review
- Make sure all tests and checks are passing
- Make sure to add a new test for the scenario (when it makes sense)

Triage and PR review meetings are Wednesdays each week. If your PR is passing checks and has been reviewed by then, that's when we typically merge.

## Codeflow
Codeflow is handled by [darc](https://github.com/dotnet/arcade/blob/main/Documentation/Darc.md). Codeflow comes from three runtime branches and a dozen tools branches.
`@dotnet/domestic-cat` will monitor codeflow and approve passing PRs (as all PRs require 1 approver) and ping the owning team for any failures.

## Internal Branching
All monthly servicing releases are done of our internal branches in case there are security changes that have to go out. That means all flows from runtime branches into the SDK and from SDK to installer are done internally.
That is why we have removed all servicing builds from the installer main page as those builds do not include any changes from any repo other than the installer repo so are very limited.
Internal codeflow is merged into public GitHub repos on patch Tuesday each month to ensure we are updated.

