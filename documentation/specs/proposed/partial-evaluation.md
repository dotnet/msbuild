# Partial (stop-after-pass) project evaluation

Tracking issue: [dotnet/msbuild#14288](https://github.com/dotnet/msbuild/issues/14288)
Related SDK consumer: [dotnet/sdk#55193](https://github.com/dotnet/sdk/issues/55193)

## Motivation

Some callers only need data produced by an early evaluation pass. The most common example is
reading a single property (for example the SDK's `ReleasePropertyProjectLocator` reads
`PublishRelease`/`PackRelease`), which today forces a **full** evaluation of the project — all
passes, including item globbing, using-task registration, and target registration.

MSBuild evaluation runs a fixed sequence of passes:

| Pass | Work |
| ---- | ---- |
| 0 | Initial properties (environment, global, toolset, reserved) |
| 1 | Properties + imports (also gathers item/item-definition/using-task/target *elements* and `InitialTargets`) |
| 2 | Item definitions |
| 3 / 3.1 | Items (includes wildcard/glob expansion) |
| 4 | Using-tasks (task registry) |
| 5 | Targets (registration, `DefaultTargets`, before/after maps) |

Property values are final after pass 1: properties cannot depend on items (item references inside
property values expand to empty even in a full evaluation), so a stop-at-properties evaluation
produces property values identical to a full evaluation.

On a file-heavy project (500 source files, ~200 properties, 50 targets), stopping after the
properties pass measured roughly a **46%** reduction in per-evaluation wall-clock versus a full
evaluation (Debug engine build; relative comparison). Passes 2–5 dominate the remainder, with item
globbing being the largest single contributor as source-file count grows.

## API

A new opt-in knob on `ProjectOptions` selects how far evaluation proceeds:

```csharp
namespace Microsoft.Build.Evaluation
{
    public enum ProjectEvaluationStage
    {
        Properties,         // stop after pass 1
        ItemDefinitions,    // stop after pass 2
        Items,              // stop after pass 3 / 3.1
        UsingTasks,         // stop after pass 4
        Full = int.MaxValue // default: run every pass (pass 5)
    }
}
```

```csharp
public class ProjectOptions
{
    public ProjectEvaluationStage EvaluationStage { get; set; } = ProjectEvaluationStage.Full;
}
```

The stage flows through the existing factory methods:

- `ProjectInstance.FromFile(path, options)` / `ProjectInstance.FromProjectRootElement(xml, options)`
- `Project.FromFile(path, options)` / `Project.FromProjectRootElement(xml, options)` / `Project.FromXmlReader(reader, options)`

Both `Project.EvaluationStage` and `ProjectInstance.EvaluationStage` report the stage the object was
evaluated to.

Example:

```csharp
var options = new ProjectOptions
{
    EvaluationStage = ProjectEvaluationStage.Properties,
    EvaluationContext = sharedContext, // complements partial evaluation; see sdk#55193
};

ProjectInstance instance = ProjectInstance.FromFile(projectPath, options);
string value = instance.GetPropertyValue("PublishRelease"); // fast: only passes 0-1 ran
```

## Behavior of a partially-evaluated object

- **Properties are always valid** for any stage ≥ `Properties` (`GetProperty`, `GetPropertyValue`,
  `Properties`, `GlobalProperties`). `InitialTargets` is also available from `Properties` onward
  because it is computed during pass 1.
- **Reading not-yet-computed state fails fast.** Members that expose state from a later pass throw
  `InvalidOperationException` naming the member and the stage the object reached. Guarded members
  include `ItemDefinitions` (available from `ItemDefinitions` onward), `Items`, `GetItems`,
  `ItemsIgnoringCondition`, `AllEvaluatedItems`, `Targets`, and `DefaultTargets` (and their
  `ProjectInstance` equivalents).
- **A partial `ProjectInstance` cannot be built.** Constructing a `BuildRequestData` from a partial
  instance throws `InvalidOperationException`; a build requires a full evaluation.

The default (`Full`) is unchanged, so existing callers are unaffected.

## Evaluation caching

`ProjectCollection` caches loaded `Project`s keyed on (path, global properties, tools version) — the
evaluation stage is not part of the key. To avoid serving stale partial state:

- A cached project satisfies a request only if `cachedStage >= requestedStage`.
- `ProjectCollection.LoadProject` requests `Full`. If the only cached project for a key was
  partially evaluated, it is **upgraded in place** (re-evaluated to `Full`) and returned, rather than
  returning partial state or creating a duplicate cache entry.
- Calling the public `Project.ReevaluateIfNecessary()` on a partial project upgrades it to `Full`
  (a partial evaluation leaves the project non-dirty, so the re-evaluation is forced).

## Relationship to `EvaluationContext`

Partial evaluation and a shared `EvaluationContext` are complementary levers. A shared context
caches file-system probes and SDK resolution across projects; partial evaluation skips whole passes
within each project. Batch scenarios that read one property from many projects (such as the SDK
release-property locator) benefit from using both together.
