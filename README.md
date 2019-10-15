# .NET Command Line Interface

[![Join the chat at https://gitter.im/dotnet/cli](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/dotnet/cli?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

This repo contains the source code for cross-platform [.NET Core](http://github.com/dotnet/core) command line toolchain. It contains the implementation of each command and the documentation.

# Looking for the .NET Core SDK tooling?

If you are looking for the latest nightly of the .NET Core SDK, see https://github.com/dotnet/core-sdk.

# Found an issue?

You can consult the [Documents Index](Documentation/README.md) to find out the current issues and to see the workarounds.

If you don't find your issue, file one! However, given that this is a very high-frequency repo, we've setup some [basic guidelines](Documentation/project-docs/issue-filing-guide.md) to help you. Consult those first.

This project has adopted the code of conduct defined by the [Contributor Covenant](http://contributor-covenant.org/) to clarify expected behavior in our community. For more information, see the [.NET Foundation Code of Conduct](http://www.dotnetfoundation.org/code-of-conduct).

# Build status

|Windows x64|
|:------:|
|[![Build Status](https://dev.azure.com/dnceng/internal/_apis/build/status/224)](https://dev.azure.com/dnceng/internal/_build?definitionId=224)|

# Basic usage

When you have the .NET Command Line Interface installed on your OS of choice, you can try it out using some of the samples on the [dotnet/core repo](https://github.com/dotnet/core/tree/master/samples). You can download the sample in a directory, and then you can kick the tires of the CLI.


First, you will need to restore the packages:

    dotnet restore

This will restore all of the packages that are specified in the project.json file of the given sample.

Then you can either run from source or compile the sample. Running from source is straightforward:

    dotnet run

Compiling to IL is done using:

    dotnet build

This will drop an IL assembly in `./bin/[configuration]/[framework]/[binary name]`
that you can run using `dotnet bin/[configuration]/[framework]/[binaryname.dll]`.

For more details, refer to the [documentation](https://aka.ms/dotnet-cli-docs).

# Building from source

If you are building from source, take note that the build depends on NuGet packages hosted on MyGet, so if it is down, the build may fail. If that happens, you can always see the [MyGet status page](http://status.myget.org/) for more info.

Read over the [contributing guidelines](CONTRIBUTING.md) and [developer documentation](Documentation) for prerequisites for building from source.

# Questions and comments

For all feedback, use the Issues on this repository.

# License

By downloading the .zip you are agreeing to the terms in the project [EULA](https://aka.ms/dotnet-core-eula).

# Repository SLA

This Service-Level Agreement (SLA) represents the commitment of the maintainers of the .NET Core CLI/SDK repositories to community members and contributors.

## Pull requests

While the maintainers of this repository have the right and obligation to move .NET Core in a direction which might conflict with pull requests from community contributors, pull requests from community contributors should receive prompt feedback and either be approved by assigning to an appropriate release milestone or closed with gratitude and a valid explanation.  Pull requests from community contributors should not be put in the backlog milestone because pull requests rarely stay valid over long periods of time; thanking the contributor for their effort and closing the pull request with an explanation is preferable to letting the pull request linger in backlog limbo.

### New pull requests

The maintainers of the repository will triage new pull requests from community contributors within **one week** of the creation of the pull request.

The community pull request will be triaged as follows:

* **Approved** - the pull request has been approved by the maintainers and should be assigned to an upcoming release milestone.  Additionally, the pull request should be assigned to a maintainer to assist the completion of the pull request.  The assigned maintainer will review the pull request, mentor the contributor regarding the standards and practices of the codebase, and inform the contributor when there might be an issue blocking the merging of the pull request, such as merge conflicts, changes in schedule necessitating branch retargeting, branch lockdowns, etc.  The assigned maintainer should provide initial feedback to the contributor within **three business days** of being assigned the pull request.
* **Pending discussion** - there are questions the maintainers have about the pull request that need to be resolved: the code needs additional work, unit tests are missing, whether or not the pull request aligns with the direction for the codebase, or the maintainers need clarification as to the purpose of the pull request.  Whenever possible, the discussion should take place solely in the GitHub pull request or in a linked GitHub issue.
* **Rejected** - The pull request is not in the direction the maintainers want to take the codebase, has too much risk, does not solve a problem of meaningful priority, or the pull request is stale and requires additional work to bring into a mergeable state.  Regardless of the reason for rejecting a pull request, maintainers should always thank the contributor for their effort and for being a valued member of the .NET Core community.

For any pull request, if the contributor has been asked for updates or clarifications and the maintainer has not heard back from the contributor for **one month**, the pull request will be assumed to be abandoned and will be closed, unless other time limits have been agreed upon between the contributor and the maintainer.

### Stale pull requests

As codebases change significantly over time and stale pull requests are unlikely to be merged without significant additional work, any pull request older than six months from a community contributor should be closed with an explanation as to why the pull request was not merged.   However, contributors may choose to update their feature branch and re-open the pull request (or re-submit a new pull request) if it is again ready to merge.  At such time, the maintainers should treat the reopening as if it were a new pull request and decide to accept the pull request and for which release milestone it belongs to.

## Issues

Reported issues from community members will be treated identically to any other issue, whether discovered by the maintainers, other Microsoft employees, etc.

The maintainers will triage issues **each business day**:

* High priority issues (crash, hang, data loss, regression, security, or other major malfunction) will get triaged to a Microsoft developer and put in their queue to address in the current milestone.

* Small malfunctions will be marked as such, assigned when developers are available, and moved to a future release milestone or the backlog milestone that represents no particular release.

* Minor issues (spelling errors, fit-n-finish, small issues with easy/obvious workaround) will be marked as `good first issue` for community contributors and will be moved to a backlog milestone mapping to no particular release.
  
* Even if an issue is not marked as `good first issue`, we welcome community contributions on any issue, but if the issue is currently assigned to someone, check with them first before working on a fix.

Issues may be closed by the maintainers of the repository for the following reasons:

* **Not repro** - the maintainer is unable to reproduce the issue following the steps outlined in the issue.  Contributors and community members may reopen the issue if they believe they have discovered another way of reproducing the issue.

* **Won't fix** - the maintainer has determined the issue has a valid reason to not be fixed, such as being too risky, code is scheduled for obsoletion in an upcoming release, fix is needed only for an out-of-service release, etc.  Contributors and community members should not reopen the issue unless there is a justifiable reason to do so, such as strong community demand, new information about the issue which might increase the severity, etc.

* **Fixed** - the maintainer has merged a pull request that addresses the issue.  The maintainer should follow-up on the issue to inform the community regarding which release to expect the fix in.

Because any code repository gradually amasses a non-trivial number of minor bugs that are unlikely to ever get fixed due to scheduling or other constraints, and which become increasingly irrelevant as the product changes, the maintainers should periodically (once or twice a year) close minor issues older than a year that no longer meet the bar as **won't fix**.  This action should include issues assigned to the backlog milestone so that the backlog isn't an issue graveyard.
