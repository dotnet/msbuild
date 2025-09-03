# BuildCheck Net10 polishing 

In net 9.0 we delivered initial preview of BuildCheck feature. 
 * Spec: https://github.com/dotnet/msbuild/blob/main/documentation/specs/BuildCheck/BuildCheck.md
 * Work track:  https://github.com/orgs/dotnet/projects/373

In addition to that we have an item tracking possible improvements and extensions of the feature: https://github.com/dotnet/msbuild/issues/10548

This doc focuses on a minimal investment path that would allow driving adoption of the BuildChecks feature and collecting feedback from real life usage.

## Goals and Motivation 

* Making sure the feature can be used in the real life scenarios.
* Driving adoption.

## Impact 

* MSBuild team has a venue to evangelise best practices for the build.
* Customers have a tool to formalize and distribute their view of best practices.
* MSBuild team can improving end-user build perf and security with high leverage by providing new Checks notifying about problems.


## Stakeholders 
- PM (@baronfel) - as a customer advocate
- Selected internal partner repo owners (details https://github.com/dotnet/msbuild/issues/10726)

### Successful handover
- Internal partner teams' ideas around BuildChecks collected and considered.
- Selected internal partner teams are using BuildChecks in their build pipelines.
- BuildChecks being evangelized externaly, adoption numbers grow up.

## Risks 
- Performance degradation is unacceptable on real scale project.
- There are multiple hard to investigate and/or hard to fix bugs identified during initial adoptions.
- Unsatisfactory specificity and/or sensitivity - leading to diminishing the perceived value of Checks and endangering the motivation to adopt them.
- Low perceived value of best practices enforced by the checks.
- Confusing diagnostics/documentation leading to lowering the educational value.


## Scope

### Goals
* MSBuild team runs buildchecks on selected partner repos (via private runs), identifying and fixing issues
* MSBuild team helps selected partner teams to enable buildchecks on their repos (in official runs), and providing initial support

### Non-Goals

* Measuring and defining the perf impact, detecting the sources of it
  This doc doesn't cover the perf measurement and analysis - see [the PerfStar one page for this topic](https://github.com/dotnet/msbuild/pull/11045/files#diff-dcbd46135c1492f7b8f0c1221118a6ec7c241b86e6493d5a93f2c2f83b50b7bfR21)
* Providing additional helpful low-hanging-fruit checks

### Out of scope

* OM/API enriching
* Configuration and other features improvements
* VS, VS-Code integrations

## Cost 

The below plan is expected with 0.25 Dev / Month investment (except for Month #2, that will realistically need ~0.5-2 Dev / Month)

## Suggested plan 
* Month #1 - Running build checks on selected partner repos and identifying issues
* Month #2 - Resolve identified adoption blockers
* Month #2 optional - run perf tests and define perf profile for build with Checks.
* Month #3 - Enabling buildchecks on partner repos, providing initial support
* Month #4 - Evangelization and driving the adoption in external community

 