
# BuildCheck - Design Spec

AKA MSBuild Analyzers

The feature is meant to help customers to improve and understand quality of their MSBuild scripts via rules violations reporting. It will allow MSBuild to gradually roll out additional rules, as users will be capable to configure their opt-in and severity of reports – preventing unwanted build breakages. And to equip powerusers to roll out their own quality checks – whether for general community or internal enterprise usage.

# Table of Contents

- [Terminology](#terminology)
- [North Star / Longer-term vision](#north-star-longer-term-vision)
- [Scope of initial iteration](#scope-of-initial-iteration)
- [User Experience](#user-experience)
   * [Running / UX](#running-ux)
      + [Inbox Analyzers](#inbox-analyzers)
      + [Live Build](#live-build)
      + [Binlog Replay mode](#binlog-replay-mode)
   * [Configuration](#configuration)
      + [User Configurable Options](#user-configurable-options)
         - [Enablement](#enablement)
         - [Severity](#severity)
         - [Scope of Analysis](#scope-of-analysis)
      + [Sample configuration](#sample-configuration)
   * [Analyzers and Rules Identification](#analyzers-and-rules-identification)
   * [Custom Analyzers Authoring](#custom-analyzers-authoring)
      + [Implementation](#implementation)
         - [Analyzer declaration](#analyzer-declaration)
         - [Rules declaration](#rules-declaration)
         - [Standardized configuration declaration](#standardized-configuration-declaration)
         - [Custom configuration declaration](#custom-configuration-declaration)
         - [Compatibility](#compatibility)
      + [Testing and Debugging](#testing-and-debugging)
      + [Packaging](#packaging)
   * [Acquisition of custom analyzers](#acquisition-of-custom-analyzers)



# Terminology

* **Analyzer** – unit of execution (single implementing class), can host multiple rules. 
* **Rule** – Single violation type, with single unique code (`“MSB123: Redefining built-in target”`). 
* **Report** – Output from Analyzer informing about violating particular rule.
* **CodeFix** – Violation remedy suggestion. Not yet applicable for MSBuild.
* **BuildCheck** - Feature name. The infrastructure within MSBuild allowing pluggability and execution of Analyzers and their Rules


# North Star / Longer-term vision

MSBuild provides a rich OM exposing the build scripts, data and execution so that various quality checking rules can be authored. Identical OM is exposed from live build execution and via post-build log event sourcing – so that users can choose whether the build analysis will happen as part of the build or as a separate process.

Users are able to tune the behavior of the checks via `.editorconfig` which brings unified and standardized experience with other tooling (including built-in and third-party C# analyzers) leveraging `.editorconfig` files.

Powerusers are able to develop, test and publish their custom analyzers easily and contribute them back to community. The local development scenario doesn’t require roundtrip through packaging.

A solid set of in-the-box analyzers is provided by MSBuild and the .NET SDK, extended each release, with high quality reports (pointing exact locations of issue, offering clear and actionable explanations, not repetitive for builds with multi-execution or/and multi-importing of a same script in single build context).

The analysis has small impact on build duration with ability to opt-out from analysis altogether which will remove all the performance costs associated with the analysis.


# Scope of initial iteration

Majority of following cases are included in appropriate context within the scenarios in [User Experience](#user-experience) section. Following is a quick overview.

**In scope**
* Inbox (build-in) analyzers that run during the build execution.
* Inbox analyzers that run when replaying binlog.
* Custom authored analyzers, delivered via nuget.
* Analyzers reports (errors, warnings, messages) are in logger output, VS error window.
* Codes will be distinguishable from standard build warnings/error (to prevent easy mixups and attempts to configure standard msbuild warnings/errors via editorconfig), but otherwise the outputs are very similar.
* Default opt-ins and levels for inbox analyzers set by sdk version (via [`$SdkAnalysisLevel`]((https://github.com/dotnet/designs/blob/main/proposed/sdk-analysis-level.md))) or other agreed mechanism for controling increasing strictness between .NET versions.
* Custom analyzers opted in via `PackageReference` of a particular nuget with the analyzer.
* Explicit overrides of enablement and analysis levels via `.editorconfig` file (with up to a per project scope).
* Standards of `.editorconfig`s will be observed. 
* Simplified authoring experience via template and doc.
* Single analyzer can produce reports for multiple rules. However those need to be declared upfront.
* Opt-in reporting of time spent via specific analyzers and infra overall.
* Collect touched `.editorconfg`s into binlog embedded files.
* Possibility to opt-out from analysis - the perf should not be impacted when done so.
* Team collects performance impact numbers on a set of benchmark builds with the inbox analyzers enabled.

**Non Goals, but subject for consideration**
* Custom anlyzer in a local project (source codes) or a binary.
* Bulk configuration of multiple rules (based on prefixes).
* Specifying scope of MSBuild imports that will be considered for analysis (so that e.g. data from sdk won't even be passed to anlyzer, if not requested).
* Attempts to try to configure standard msbuild warnings/errors via `.editorconfig` should lead to fail fast errors.
* Configuring analysis levels when analysing from binlog - beyond the collected editorconfigs
* Rich information in VS error window.


**Out of scope**
* Custom analyzers has equal data access as the inbox analyzers.
* All inbox analyzers reports have precise location(s) of issues.
* Opt-out of analysis on code-level (something like C# `#pragma warning disable`, but within msbuild xml files).
* Simplified authoring experience via dedicated reference assembly.
* Restore phase analysis.
* Turning analysis off/on based on target (e.g. multi-targetted builds, calling MSBuild task etc.).
* Controling/Configuring lifetime of analyzers - analyzers will currently be held alive, as single instance per analyzer, for the whole duration of the build. But future versions might prevent some of the analyzers to survive beyond a scope of a single project built (means for sharing data would be provided).
* ETW for analyzers.
* Attributing `.editorconfig` configurations to .sln files.
* Attributing `.editorconfig` configurations to lower granularity than whole projects.
* Respecting `.editorconfig` file in msbuild import locations (unless they are in the parent folders hieararchy of particular project file).
 

# User Experience

## Running / UX

### Inbox Analyzers

**TBD** - suggested list of analyzers to be shipped with V1 (along with proposed configuration)

### Live Build

BuildCheck will run as part of the build and execute [inbox analyzers](#inbox-analyzers) and [custom analyzers](#acquisition-of-custom-analyzers) based on the [configuration](#configuration). Users will have an option to completely opt-out from BuildCheck to run via commandline switch.

Findings - reports - of analyzers will be output as build messages/warnings/errors, and the message/warnings/error code should help distinguish BuildCheck produced reports from regular build errors/warnings.

### Binlog Replay mode

Users will have option to explicitly opt-in to run BuildCheck during the binlog replay mode:

```bash
> dotnet build msbuild.binlog /analyze
```

Would there be any analyzers that are not possible to run during the replay mode (subject to internal design - this difference won't be exposed during [custom analyzers authoring](#custom-analyzers-authoring)), replay mode will inform user about those via warnings.

Replay mode will by default consider `.editorconfig` files stored within the binlog and will run analyzers based on those. This may lead to double-reports – as binlog will have the runtime analysis reports stored, plus the replay-time analysis reports will be augumented.

For this reason we will consider following modes (all are non-goals):
* Ability to specify skipping all binlog stored reports
* Ability to specify skipping of the stored .editorconfig files
* Ability to specify single replay-time .editorconfig file and it’s precedence (only the specified, specified as most significant, specified as least significant)

## Configuration

There will be 3 mechanisms of configuring the analyzers and rules:
* The default configuration declared by the analyzers themselves ([more details on implementation](#rules-declaration))
* [Sdk Analysis Level property](https://github.com/dotnet/designs/blob/main/proposed/sdk-analysis-level.md) – mostly for the inbox analyzers
* `.editorconfig` file

For the `.editorconfig` file configuration, following will apply:
* Only `.editorconfig` files collocated with the project file or up the folder hierarchy will be considered.
* `.editorconfig` files placed along with explicitly or implicitly imported msbuild files won’t be considered.
* `.editorconfig` files packaged within nuget packages won’t be considered (unless the unpack action will place them alongside the referencing project).

Non-Goals (but might be considered):
* bulk configuration of multiple rules - based on analyzers/rules prefixes or/and categories.
* attempts to try to configure standard msbuild warnings/errors via `.editorconfig` should lead to fail fast errors.
* configuring analysis levels when analysing from binlog - beyond the collected editorconfigs.
* Aliasing the analyzers/rules, allowing to create multiple instances with different custom configuration (e.g. single analyzer checking configurable list of forbidden properties prefixes can have 2 instance, each initialized with different list to check, each of the instance configurable for individual projects separately).

Out of scope for configuration:
* opt-out of analysis on code-level (analogy to C# pragmas, but within msbuild xml files).
* lower granularity of `.editorconfig` settings other than whole projects.
* attributing configuration to a .sln file and expecting it will apply to all contained projects.
* Support for multiple [custom configurations](#custom-configuration-declaration) within a single build for a single rule. (Not to be mixed with [standardized configuration](#standardized-configuration-declaration) - which can be configured freely per project) If a custom configuration will be used, it will need to be specified identically in each explicit configurations of the rule. This is chosen so that there are no implicit requirements on lifetime of the analyzer or analyzer instancing – each analyzer will be instantiated only once per build (this is however something that will very likely change in future versions – so authors are advised not to take hard dependency on single instance policy).

### User Configurable Options

Initial version of BuildCheck plans a limited set of options configurable by user (via `.editorconfig`) by which users can override default configuration of individual analyzer rules.

#### Enablement

Boolean option `IsEnabled` will be available to allow users to disable/enable particualr rule.

Different rules of a single analyzer can have different enablement status configured.

If all the rules from a single analyzer are disabled - analyzer won't be given any data for such configured part of the build (specific project or a whoe build). If analyzer have some rules enabled and some disabled - it will be still fed with data, but the reports will be post-filtered.

Same rule can have different enablement status for different projects.

#### Severity

Option `Severity` with following values will be available:

* `Message`
* `Warning`
* `Error`

Configuration will dictate transformation of the analyzer report to particular build output type (message, warning or error).

Each rule has a severity, even if multiple rules are defined in a single analyzer. The rule can have different severities for different projects within a single build session.

#### Scope of Analysis

Option `EvaluationAnalysisScope` with following possible options will be avaialable:
* `AnalyzedProjectOnly` - Only the data from currently analyzed project will be sent to the analyzer. Imports will be discarded.
* `AnalyzedProjectWithImportsFromCurrentWorkTree` - Only the data from currently analyzed project and imports from files under the entry project or solution will be sent to the analyzer. Other imports will be discarded.
* `AnalyzedProjectWithImportsWithoutSdks` - Imports from SDKs will not be sent to the analyzer. Other imports will be sent.
* `AnalyzedProjectWithAllImports` - All data will be sent to the analyzer.

All rules of a single analyzer must have the `EvaluationAnalysisScope` configured to a same value. If any rule from the analyzer have the value configured differently - a warning will be issued during the build and analyzer will be deregistered.

Same rule can have `EvaluationAnalysisScope` configured to different values for different projects.

BuildCheck might not be able to guarantee to properly filter the data with this distinction for all [registration types](#RegisterActions) - in case an explicit value is attempted to be configured (either [from the analyzer code](#BuildAnalyzerConfiguration) or from `.editorconfig` file) for an analyzer that has a subscription to unfilterable data - a warning will be issued during the build and analyzer will be deregistered.

### Sample configuration

```
[*.csproj]
msbuild_analyzer.BC0101.IsEnabled=true
msbuild_analyzer.BC0101.Severity=warning

msbuild_analyzer.COND0543.IsEnabled=false
msbuild_analyzer.COND0543.Severity=Error
msbuild_analyzer.COND0543.EvaluationAnalysisScope=AnalyzedProjectOnly
msbuild_analyzer.COND0543.CustomSwitch=QWERTY
```

## Analyzers and Rules Identification

**TBD**

* Recommended and reserved prefixes
* Short vs descriptive names
* Rules categories
* Ability to use prefixes during configuration


## Custom Analyzers Authoring

### Implementation

To author custom analyzer, user will need to implement given contract (delivered in Microsoft.Build package). The contract will provide access to the exposed BuildCheck OM focused on build analysis.

#### Analyzer declaration

Simplified proposal:

```csharp
public abstract class BuildAnalyzer : IDisposable
{
    /// <summary>
    /// Friendly name of the analyzer.
    /// Should be unique - as it will be used in the tracing stats, infrastructure error messages, etc.
    /// </summary>
    public abstract string FriendlyName { get; }

    /// <summary>
    /// Single or multiple rules supported by the analyzer.
    /// </summary>
    public abstract IReadOnlyList<BuildAnalyzerRule> SupportedRules { get; }

    /// <summary>
    /// Optional initialization of the analyzer.
    /// </summary>
    /// <param name="configurationContext">
    /// Custom data (not recognized by the infrastructure) passed from .editorconfig
    /// Currently the custom data has to be identical for all rules in the analyzer and all projects.
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

<a name="RegisterActions" />The context in `RegisterActions` call will enable subscriptions for data pumping from the infrastructure. 

Sample of how registrations might look like:

```csharp
public interface IBuildCheckRegistrationContext
{
    void RegisterEvaluatedPropertiesAction(Action<BuildCheckDataContext<EvaluatedPropertiesAnalysisData>> evaluatedPropertiesAction);

    void RegisterParsedItemsAction(Action<BuildCheckDataContext<ParsedItemsAnalysisData>> parsedItemsAction);

    // ...
}
```

The data provided in callbacks for registered actions will allow the analyzer to submit reports for its rules. A single callback can lead to multiple reports being generated.

Any analyzer will be allowed to produce reports only for Rules that it declared in it’s `SupportedRules` definition.

#### Rules declaration

A single analyzer can declare support of multiple rules – since it might produce reports for those on top of same input data – and for efficiency reasons a single processing of data might be needed.

Simplified proposal of definition of a single rule:

```csharp
public class BuildAnalyzerRule
{
    // Identification/Description fields

    /// <summary>
    /// The default configuration - overridable by the user via .editorconfig.
    /// If no user specified configuration is provided, this default will be used.
    /// </summary>
    public BuildAnalyzerConfiguration DefaultConfiguration { get; }
}
```

<a name="BuildAnalyzerConfiguration" />Each rule will supply its default configuration (mainly enablement and report severity) – those will apply if `.editorconfig` file will not set those settings explicitly. If the rule doesn't provide (some of) its defaults, a global hardcoded default is used (`severity: message, enabled: false`).

#### Standardized configuration declaration

Proposal of configuration specification:

```csharp
/// <summary>
/// Configuration for a build analyzer.
/// Default values can be specified by the Analyzer in code.
/// Users can overwrite the defaults by explicit settings in the .editorconfig file.
/// Each rule can have its own configuration, which can differ per each project.
/// The <see cref="EvaluationAnalysisScope"/> setting must be same for all rules in the same analyzer (but can differ between projects)
/// </summary>
public class BuildAnalyzerConfiguration
{
    /// <summary>
    /// This applies only to specific events, that can distinguish whether they are directly inferred from
    ///  the current project, or from some import. If supported it can help tuning the level of detail or noise from analysis.
    ///
    /// If not supported by the data source - then the setting is ignored
    /// </summary>
    public EvaluationAnalysisScope? EvaluationAnalysisScope { get; internal init; }

    /// <summary>
    /// The default severity of the result for the rule. May be overridden by user configuration.
    /// </summary>
    public BuildAnalyzerResultSeverity? Severity { get; internal init; }

    /// <summary>
    /// Whether the analyzer rule is enabled.
    /// If no rule within the analyzer is enabled, the whole analyzer will not be run.
    /// If some rules are enabled and some are not, the analyzer will be run and reports will be post-filtered.
    /// </summary>
    public bool? IsEnabled { get; internal init; }
}
```

Values for this recognized contract, that are explicitly specified via .editorconfig files are passed only to the BuildCheck infrastructure – they are invisible to the actual analyzers.

#### Custom configuration declaration

However if user will specify additional – unrecognized - values in `.editorconfig` file as part of a particular analyzer configuration – those values will be extracted as key-value pairs and passed to the analyzer initialization call (`Initialize`) via `ConfigurationContext`:

```csharp
/// <summary>
/// Holder of an optional configuration from .editorconfig file (not recognized by the infrastructure)
/// </summary>
public class ConfigurationContext
{
    /// <summary>
    /// Custom configuration data - per each rule that has some specified.
    /// </summary>
    public CustomConfigurationData[] CustomConfigurationData { get; init; }
}
```

This can allow creation of extendable checks – e.g. a check that will validate that properties defined within project do not start with any forbidden prefix, while actual prefixes to check are configurable – so the user of the check can tune the behavior to their needs.

More details on configuration are in [Configuration](#configuration) section.


#### Compatibility

All the publicly exposed contracts will be available within `Microsoft.Build.Experimental.BuildCheck` interface. The interface is expressing that contracts are not guaranteed to be forward compatible (however breakage will be limited to necessary cases). The availability of particular set of BuildCheck API will be queryable via [Feature Query API](https://github.com/dotnet/msbuild/pull/9665):

```csharp
var availability = Features.CheckFeatureAvailability("BuildCheck.Beta");
```

This way the analyzers authors will be equipped to write highly-compatible analyzers even in a possibility of changing API.


### Testing and Debugging

**TBD**

We aim to provide ability to locally test analyzers from local projects or assemblies without a need to roundtrip through packaging them. The exact way is yet to be determined.

At the same time we aim to provide mocks providing the BuildCheck context data – this work is however a non-goal.

### Packaging

Several requirements are mandated for analyzer packages to be properly recognized (Netstandard only, A call to designated property function will need to be part of the packaged build assets, dependencies will need to be packaged, binaries structure flattened).

Also such a dependency is a purely development time harness - so it should be marked as [`DevelopmentDependency`](https://learn.microsoft.com/en-us/nuget/reference/nuspec#developmentdependency).

In order to simplify the packaging process (and meeting above mentioned requiements) a dotnet template will be provided producing proper package on pack action.

**TBD** - dotnet new sample on initiating the development.

## Acquisition of custom analyzers

Apart from [inbox analyzers](#inbox-analyzers) (shipped together with msbuild), users will be able to plug-in packaged analyzers shipped as nugets (this will serve for community contributions, but possibly as a venue for off-cycle distribution of official analyzers).

In order to use an analyzer package users just need to reference them via `<PackageReference>` element as standard package reference. 

```xml
<PackageReference Include="Contoso.Analyzers" Version="1.2.3" />
```

Only projects referencing the package will be able to run its analyzers. Enabling the rules from package on other projects won’t take any effect.