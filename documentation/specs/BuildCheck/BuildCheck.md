
# BuildCheck - Design Spec

Previously known as "warning waves" and "MSBuild Checks"

The feature is meant to help customers to improve and understand quality of their MSBuild scripts via rules violations reporting. It will allow MSBuild to gradually roll out additional rules, as users will be capable to configure their opt-in and severity of reports – preventing unwanted build breakages. And to equip powerusers to roll out their own quality checks – whether for general community or internal enterprise usage.

# Terminology

* **Check** – unit of execution (single implementing class), can host multiple rules. 
* **Rule** – Single violation type, with single unique code (`“BC1234: Redefining built-in target”`). 
* **Report** – Output from check informing about violating particular rule.
* **CodeFix** – Violation remedy suggestion. Not yet applicable for MSBuild.
* **BuildCheck** - Feature name. The infrastructure within MSBuild allowing pluggability and execution of checks and their Rules


# North Star / Longer-term vision

MSBuild provides a rich object model (further just OM) exposing representation of the build scripts (unstructured and structured model of documents contributing to the build), build data (the definition and evaluated values of MSBuild primitives) and build execution (the eventing model of inputs, processing and outputs of the orchestrated execution) so that various quality checking rules can be authored. This includes static check rules (e.g. checking validity of condition expressions) as well as build execution rules (e.g. checking of referencing nonexistent files) and composition rules (e.g. unintended outputs overwrites checking). Identical OM is exposed from live build execution and via post-build log event sourcing – so that users can choose whether the build check will happen as part of the build or as a separate process.

Users are able to tune the behavior of the checks via `.editorconfig` which brings unified and standardized experience with other tooling (including built-in and third-party C# checks) leveraging `.editorconfig` files.

Powerusers are able to develop, test and publish their custom checks easily and contribute them back to community. The local development scenario doesn’t require roundtrip through packaging.

A solid set of in-the-box checks is provided by MSBuild and the .NET SDK, extended each release, with high quality reports (pointing exact locations of issue, offering clear and actionable explanations, not repetitive for builds with multi-execution or/and multi-importing of a same script in single build context). The existing in-the-box checks are gradually enabled by default and their severity increased - in waves (likely tied to sdk releases) - aiming to constantly increase the quality of our customers build scripts. To avoid breaking customers builds, there will still be an explicit user gesture required to opt into running the check. This will be done either by configuring the checks with `.editorconfig` or auto-enabling the check based on the TFM of the project. There will be no difference between building with `dotnet build` and with `MSBuild.exe`, they will follow the same enablement rules with the set of enabled built-in checks derived from `.editorconfig` and TFM/props. Building in Visual Studio will eventually reach parity with command-line build as well.

Projects that don't use the .NET SDK and those that are not SDK-style at all are TBD. There is a possibility of using a property like `MSBuildCheckLevel` to enable some base checks we believe will add value everywhere.

The check has small impact on build duration with ability to disable check altogether which will remove all the performance costs associated with the check. The perf impact on representative projects is continuously monitored and documented by the MSBuild team.


# Scope of initial iteration

Majority of following cases are included in appropriate context within the scenarios in [User Experience](#user-experience) section. Following is a quick overview.

## In scope
* Inbox (built-in) checks that run during the build execution.
* Inbox checks that run when replaying binlog.
* Custom authored checks, delivered via nuget.
* Checks reports (errors, warnings, messages) are in logger output, VS error window.
* Codes will be distinguishable from standard build warnings/error (to prevent easy mixups and attempts to configure standard msbuild warnings/errors via editorconfig), but otherwise the outputs are very similar.
* Default opt-ins and levels for inbox checks set by sdk version (via [`$SdkAnalysisLevel`]((https://github.com/dotnet/designs/blob/main/proposed/sdk-analysis-level.md))) or other agreed mechanism for controlling increasing strictness between .NET versions.
* Custom checks opted in via `PackageReference` of a particular nuget with the check.
* Explicit overrides of enablement and check levels via `.editorconfig` file (with up to a per-project scope).
* [Specification of `.editorconfig`](https://spec.editorconfig.org/) will be observed. 
* Simplified authoring experience via template and doc.
* Single check can produce reports for multiple rules. However those need to be declared upfront.
* Opt-in reporting of time spent via specific checks and infra overall.
* Collect touched `.editorconfig`s into binlog embedded files.
* Possibility to opt-out from check - the perf should not be impacted when done so.
* Team collects performance impact numbers on a set of benchmark builds with the inbox checks enabled.

## Non Goals, but subject for consideration
* Custom check in a local project (source codes) or a binary.
* Bulk configuration of multiple rules (based on prefixes).
* Specifying scope of MSBuild imports that will be considered for check (so that e.g. data from sdk won't even be passed to check, if not requested).
* Attempts to try to configure standard msbuild warnings/errors via `.editorconfig` should lead to fail fast errors.
* Configuring check levels when checking from binlog - beyond the collected editorconfigs
* Structured information in VS error window (similarly to the Roslyn check reports - reports have titles, details, locations, searchable codes and exposed links leading to detailed documentation).


## Out of scope
* Instrumentation for telemetry.
* Design time build check.
* Localization support (for reports message formats, identifiers, etc.).
* Custom checks have equal data access as the inbox checks. We'll aim to ship checks that use public BuildCheck API/OM surface. But for extra agility we might chose to implement and ship some checks using unexposed data.
* All inbox checks reports have precise location(s) of issues (however for each individual check not providing precise location we should have a very strong reason, why location cannot be provided and why it still brings value even without precise location).
* Opt-out of check on code-level (something like C# `#pragma warning disable`, but within msbuild xml files).
* Simplified authoring experience via dedicated reference assembly.
* Restore phase check.
* Turning check off/on based on target (e.g. multi-targeted builds, calling MSBuild task etc.).
* Controlling/Configuring lifetime of checks - checks will currently be held alive, as single instance per check, for the whole duration of the build. But future versions might prevent some of the checks to survive beyond a scope of a single project built (means for sharing data would be provided).
* Event Tracing for Windows (ETW) for checks.
* Attributing `.editorconfig` configurations to .sln files. E.g.:
```ini
# I expect this to apply to all projects within my solution, but not to projects which are not part of the solution
[ContosoFrontEnd.sln]
build_check.BC0101.Severity=warning
```
* Attributing `.editorconfig` configurations to lower granularity than whole projects. E.g.:
```ini
# I expect this to apply only to a scope of the imported file. Or possibly I expect this to apply to all projects importing this project.
[ContosoCommonImport.proj]
build_check.BC0101.Severity=warning
```
* Respecting `.editorconfig` file in msbuild import locations (unless they are in the parent folders hierarchy of particular project file).
* CodeFixes are not supported in V1
 

# User Experience

## Running / UX

### Inbox Checks

Suggested list of checks to be shipped with V1: https://github.com/dotnet/msbuild/issues/9630#issuecomment-2007440323

The proposed initial configuration for those is TBD (as well based on initial test runs of the checks of chosen public repositories).

### Live Build

BuildCheck will run as part of the build and execute [inbox checks](#inbox-checks) and [custom checks](#acquisition-of-custom-checks) based on the [configuration](#configuration). Users will have an option to completely opt-out from BuildCheck to run via an MSBuild property (could be set in a project file or passed on the commandline).

Findings - reports - of checks will be output as build messages/warnings/errors, and the message/warnings/error code should help distinguish BuildCheck produced reports from regular build errors/warnings.

BuildCheck reports will have power to fail the build (via errors or warnings), that would otherwise succeed without the BuildCheck. This is actually the main benefit of the feature - as it helps enforcing new rules, that are easily user configurable individually or as a whole feature - to prevent unwanted breakages of legacy builds not ready for improvements.

### Binlog Replay mode

Users will have option to explicitly opt-in to run BuildCheck during the binlog replay mode:

```bash
> dotnet build msbuild.binlog /check
```

Would there be any checks that are not possible to run during the replay mode (subject to internal design - this difference won't be exposed during [custom checks authoring](#custom-checks-authoring)), replay mode will inform user about those via warnings.

Replay mode will by default consider `.editorconfig` files stored within the binlog and will run checks based on those. This would possibly lead to unintended double-reports – as binlog will have the runtime check reports stored, plus the replay-time check reports will be augmented. At the same time we might want to run some additional checks in the replay mode, that have not been enabled (or not even available) during the build time.

For this reason we will consider following modes (all are non-goals):
* All binlog stored reports are skipped by default. We add option to request not skipping them (but they might need to be prefixed or otherwise distinguished from the 'fresh' reports).
* Ability to specify skipping of the stored .editorconfig files
* Ability to specify single replay-time .editorconfig file and it’s precedence (only the specified, specified as most significant, specified as least significant)

We might as well consider specifying custom checks on a command line (as a non-goal) - so that unreferenced custom checks can be run against the binlog.

## Configuration

There will be 3 mechanisms of configuring the checks and rules:
* The default configuration declared by the checks themselves ([more details on implementation](#rules-declaration))
* The TFM of the project and the [Sdk Analysis Level property](https://github.com/dotnet/designs/blob/main/proposed/sdk-analysis-level.md) – mostly for the inbox checks
* `.editorconfig` file

We will also consider respecting `SdkAnalysisLevel` to override the per-TFM defaults. Additionally, we may introduce a new "master switch" property, tentatively called `RunMSBuildChecks`, to make it possible to disable everything whole-sale. This would be used in scenarios like F5 in VS.
```
Skipping checks to speed up the build. You can execute 'Build' or 'Rebuild' command to run checks.
```

Here's the proposed release schedule:
- **.NET 9** - the feature is introduced and enabled in `dotnet build` and `MSBuild.exe` command-line builds. It is not enabled in VS just yet. No checks are enabled by default. It is not technically required to read the TFM or any other props during evaluation, though it would be nice to respect `RunMSBuildChecks` already in this release. `.editorconfig` can be the sole source of configuration.
- **.NET 10** - based on feedback and testing, we choose a set of checks to enable by default for projects targeting `net10.0`, and enable the feature as a whole in Visual Studio. Depending on how we feel about the perf characteristics of evaluation, especially the "double evaluation" mandated by discovering evaluation-tracking checks just-in-time, we may want to omit such checks from the default set.
- **.NET 11** and beyond - some more checks are enabled for projects targeting `net11.0`, the `net10.0` does not change. Everything is mature and performant enough that we are able to auto-enable any check. The gradual tightening of rules enabled by default gets us closer to the long envisioned "strict mode", which will ultimately allow us to evolve MSBuild to be simpler and more performant.


For the `.editorconfig` file configuration, following will apply:
* Only `.editorconfig` files collocated with the project file or up the folder hierarchy will be considered.
* `.editorconfig` files placed along with explicitly or implicitly imported msbuild files won’t be considered.
* `.editorconfig` files packaged within nuget packages within local nuget cache won’t be considered.

### Non-Goals (but might be considered):
* bulk configuration of multiple rules - based on checks/rules prefixes or/and categories.
* attempts to try to configure standard msbuild warnings/errors via `.editorconfig` should lead to fail fast errors.
* configuring check levels when checking from binlog - beyond the collected editorconfigs.
* Aliasing the checks/rules, allowing to create multiple instances with different custom configuration (e.g. single check checking configurable list of forbidden properties prefixes can have 2 instance, each initialized with different list to check, each of the instance configurable for individual projects separately).

### Out of scope for configuration:
* opt-out of check on code-level (analogy to C# pragmas, but within msbuild xml files).
* lower granularity of `.editorconfig` settings other than whole projects.
* attributing configuration to a .sln file and expecting it will apply to all contained projects.
* Support for multiple [custom configurations](#custom-configuration-declaration) within a single build for a single rule. (Not to be mixed with [standardized configuration](#standardized-configuration-declaration) - which can be configured freely per project) If a custom configuration will be used, it will need to be specified identically in each explicit configurations of the rule. This is chosen so that there are no implicit requirements on lifetime of the check or check instancing – each check will be instantiated only once per build (this is however something that will very likely change in future versions – so authors are advised not to take hard dependency on single instance policy).

### Sample configuration

```ini
[*.csproj]
build_check.BC0101.severity=warning

build_check.COND0543.severity=none
build_check.COND0543.scope=project
build_check.COND0543.custom_switch=QWERTY
```

### User Configurable Options

Initial version of BuildCheck plans a limited set of options configurable by user (via `.editorconfig`) by which users can override default configuration of individual check rules.

**NOTE:** The actual naming of the configuration options is yet to be determined.

#### Severity levels

Option `Severity` with following values will be available:

| Severity      | EditorConfig option      |
| ------------- | ------------- |
| Default | `default` |
| None | `none` |
| Suggestion | `suggestion` |
| Warning | `warning` |
| Error | `error` |

Severity levels are in line with [roslyn analyzers severity levels](https://learn.microsoft.com/en-us/visualstudio/code-quality/use-roslyn-analyzers). `Default` severity in `.editorconfig` will lead to using build-in severity from the check (so this can be used for clearing custom severity setting from higher level `.editorconfig` file). `Default` severity in the build-in code has same effect as if the code doesn't specify severity at all - an infrastruture default of `None` is considered.

Configuration will dictate transformation of the check report to particular build output type (message, warning or error).

Each rule has a severity, even if multiple rules are defined in a single check. The rule can have different severities for different projects within a single build session.

If all the rules from a single check have severity `None` - check won't be given any data for such configured part of the build (specific project or a whole build). If check have some rules enabled and some disabled - it will be still fed with data, but the reports will be post-filtered.

#### Configuring severity level

```ini
[*.csproj]
build_check.BC0101.severity=warning
```

#### Scope of Check

Option `EvaluationCheckScope` with following possible options will be available:

| EvaluationCheckScope (Solution Explorer)   | EditorConfig option      |  Behavior  | 
| ------------- | ------------- |   ------------- |
| ProjectOnly | `project` | Only the data from currently checked project will be sent to the check. Imports will be discarded. | 
| ProjectWithImportsFromCurrentWorkTree | `current_imports` |  Only the data from currently checked project and imports from files under the entry project or solution will be sent to the check. Other imports will be discarded. | 
| ProjectWithImportsWithoutSdks | `without_sdks` | Imports from SDKs will not be sent to the check. Other imports will be sent. | 
| ProjectWithAllImports | `all` | All data will be sent to the check. | 

All rules of a single check must have the `EvaluationCheckScope` configured to a same value. If any rule from the check have the value configured differently - a warning will be issued during the build and check will be deregistered.

Same rule can have `EvaluationCheckScope` configured to different values for different projects.

BuildCheck might not be able to guarantee to properly filter the data with this distinction for all [registration types](#RegisterActions) - in case an explicit value is attempted to be configured (either [from the check code](#BuildExecutionCheckConfiguration) or from `.editorconfig` file) for an check that has a subscription to unfilterable data - a warning will be issued during the build and check will be deregistered.

#### Configuring evalution scope

```ini
[*.csproj]
build_check.BC0101.scope=all
```

## Checks and Rules Identification

**TBD**

* Recommended and reserved prefixes
* Short vs descriptive names
* Rules categories
* Ability to use prefixes during configuration


## Custom Checks Authoring

### Implementation

To author custom check, user will need to implement given contract (delivered in Microsoft.Build package). The contract will provide access to the exposed BuildCheck OM focused on build check.

#### Check declaration

Simplified proposal:

```csharp
public abstract class BuildExecutionCheck : IDisposable
{
    /// <summary>
    /// Friendly name of the check.
    /// Should be unique - as it will be used in the tracing stats, infrastructure error messages, etc.
    /// </summary>
    public abstract string FriendlyName { get; }

    /// <summary>
    /// Single or multiple rules supported by the check.
    /// </summary>
    public abstract IReadOnlyList<BuildExecutionCheckRule> SupportedRules { get; }

    /// <summary>
    /// Optional initialization of the check.
    /// </summary>
    /// <param name="configurationContext">
    /// Custom data (not recognized by the infrastructure) passed from .editorconfig
    /// Currently the custom data has to be identical for all rules in the check and all projects.
    /// </param>
    public abstract void Initialize(ConfigurationContext configurationContext);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="context"></param>
    public abstract void RegisterActions(IBuildCheckRegistrationContext context);

    public virtual void Dispose()
    { }
}
```

<a name="RegisterActions"></a>The context in `RegisterActions` call will enable subscriptions for data pumping from the infrastructure. 

Sample of how registrations might look like:

```csharp
public interface IBuildCheckRegistrationContext
{
    void RegisterEvaluatedPropertiesAction(Action<BuildCheckDataContext<EvaluatedPropertiesCheckData>> evaluatedPropertiesAction);

    void RegisterParsedItemsAction(Action<BuildCheckDataContext<ParsedItemsCheckData>> parsedItemsAction);

    // ...
}
```

The data provided in callbacks for registered actions will allow the check to submit reports for its rules. A single callback can lead to multiple reports being generated.

Any check will be allowed to produce reports only for Rules that it declared in it’s `SupportedRules` definition.

#### Rules declaration

A single check can declare support of multiple rules – since it might produce reports for those on top of same input data – and for efficiency reasons a single processing of data might be needed.

Simplified proposal of definition of a single rule:

```csharp
public class BuildExecutionCheckRule
{
    // Identification/Description fields
    // (To be defined more precisely by https://github.com/dotnet/msbuild/issues/9823)

    /// <summary>
    /// The default configuration - overridable by the user via .editorconfig.
    /// If no user specified configuration is provided, this default will be used.
    /// </summary>
    public BuildExecutionCheckConfiguration DefaultConfiguration { get; }
}
```

<a name="BuildExecutionCheckConfiguration"></a>Each rule will supply its default configuration (mainly enablement and report severity) – those will apply if `.editorconfig` file will not set those settings explicitly. If the rule doesn't provide (some of) its defaults, a global hardcoded default is used (`severity: message, enabled: false`).

#### Standardized configuration declaration

Proposal of configuration specification:

```csharp
/// <summary>
/// Configuration for a build check.
/// Default values can be specified by the check in code.
/// Users can overwrite the defaults by explicit settings in the .editorconfig file.
/// Each rule can have its own configuration, which can differ per each project.
/// The <see cref="EvaluationCheckScope"/> setting must be same for all rules in the same check (but can differ between projects)
/// </summary>
public class BuildExecutionCheckConfiguration
{
    /// <summary>
    /// This applies only to specific events, that can distinguish whether they are directly inferred from
    ///  the current project, or from some import. If supported it can help tuning the level of detail or noise from check.
    ///
    /// If not supported by the data source - then the setting is ignored
    /// </summary>
    public EvaluationCheckScope? EvaluationCheckScope { get; internal init; }

    /// <summary>
    /// The default severity of the result for the rule. May be overridden by user configuration.
    ///
    /// If all rules within the check are `none`, the whole check will not be run.
    /// If some rules are `none` and some are not, the check will be run and reports will be post-filtered.
    /// </summary>
    public BuildExecutionCheckResultSeverity? Severity { get; internal init; }
}
```

Values for this recognized contract, that are explicitly specified via .editorconfig files are passed both to the BuildCheck infrastructure as well as individual checks.

#### Custom configuration declaration

However if user will specify additional – unrecognized - values in `.editorconfig` file as part of a particular check configuration – those values will be extracted as key-value pairs and passed to the check initialization call (`Initialize`) via `ConfigurationContext`:

```csharp
/// <summary>
/// Holder of an optional configuration from .editorconfig file (not recognized by the infrastructure)
/// </summary>
public class ConfigurationContext
{
    /// <summary>
    /// Custom configuration data - per each rule that has some specified.
    /// </summary>
    public IReadOnlyList<CustomConfigurationData> CustomConfigurationData { get; init; }

    /// <summary>
    /// Configuration data from standard declarations
    /// </summary>
    public IReadOnlyList<BuildExecutionCheckConfiguration> BuildExecutionCheckConfig { get; init; }
}
```

This can allow creation of extendable checks – e.g. a check that will validate that properties defined within project do not start with any forbidden prefix, while actual prefixes to check are configurable – so the user of the check can tune the behavior to their needs.

More details on configuration are in [Configuration](#configuration) section.


#### Compatibility

All the publicly exposed contracts will be available within `Microsoft.Build.Experimental.BuildCheck` namespace. The namespace is expressing that contracts are not guaranteed to be backward compatible (however breakage will be limited to necessary cases). The availability of particular set of BuildCheck API will be queryable via [Feature Query API](https://github.com/dotnet/msbuild/pull/9665):

```csharp
var availability = Features.CheckFeatureAvailability("BuildCheck.Beta");
```

This way the checks authors will be equipped to write highly-compatible checks even in a possibility of changing API.


### Testing and Debugging

**TBD**

We aim to provide ability to locally test checks from local projects or assemblies without a need to roundtrip through packaging them. The exact way is yet to be determined.

At the same time we aim to provide mocks providing the BuildCheck context data – this work is however a non-goal.

### Packaging

Several requirements are mandated for check packages to be properly recognized (Netstandard only, A call to designated property function will need to be part of the packaged build assets, dependencies will need to be packaged, binaries structure flattened). There might as well be couple of optional practices making the check package more resources savvy (E.g. defining the rule ids and enablement status within the mentioned property function - so that such information doesn't require loading and calling of the check type).

Also custom check package is a dependency is a purely development time harness - so it should be marked as [`DevelopmentDependency`](https://learn.microsoft.com/en-us/nuget/reference/nuspec#developmentdependency).

In order to simplify the packaging process (and meeting above mentioned requirements) a dotnet template will be provided producing proper package on pack action.

**TBD** - dotnet new sample on initiating the development.

## Acquisition of custom checks

Apart from [inbox checks](#inbox-checks) (shipped together with msbuild), users will be able to plug-in packaged checks shipped as nugets (this will serve for community contributions, but possibly as a venue for off-cycle distribution of official checks).

In order to use an check package users just need to reference them via `<PackageReference>` element as standard package reference. 

```xml
<PackageReference Include="Contoso.checks" Version="1.2.3" />
```

Only projects referencing the package will be able to run its checks. Enabling the rules from package on other projects won’t take any effect.
