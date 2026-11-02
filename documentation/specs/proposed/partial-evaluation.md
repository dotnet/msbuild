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

The stage flows through the `ProjectInstance` factory methods:

- `ProjectInstance.FromFile(path, options)` / `ProjectInstance.FromProjectRootElement(xml, options)`

`ProjectInstance.EvaluationStage` reports the stage the instance was evaluated to.

Partial evaluation is intentionally **not** supported on the mutable `Project` object model. `Project`
is an editable model that is cached in the `ProjectCollection`; a partially-evaluated `Project` would
either serve stale state from that cache or be silently upgraded to a full evaluation, discarding the
work the partial evaluation saved. The `Project` factory methods (`Project.FromFile`,
`Project.FromProjectRootElement`, `Project.FromXmlReader`) therefore throw `ArgumentException` when a
non-`Full` `EvaluationStage` is requested; use `ProjectInstance` for partial evaluation.

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
- **Reading not-yet-computed state fails fast.** `ProjectInstance` members that expose state from a
  later pass throw `InvalidOperationException` naming the member and the stage the instance reached.
  Guarded members include `ItemDefinitions` (available from `ItemDefinitions` onward), `Items`,
  `GetItems`, `Targets`, and `DefaultTargets`.
- **A partial `ProjectInstance` cannot be built.** Constructing a `BuildRequestData` from a partial
  instance throws `InvalidOperationException`; a build requires a full evaluation.

The default (`Full`) is unchanged, so existing callers are unaffected.

## Evaluation caching

Partial evaluation applies only to `ProjectInstance`, which is an immutable evaluation snapshot that
is **not** stored in the `ProjectCollection` loaded-project cache. A partial `ProjectInstance` can
therefore never be returned in place of a fully-evaluated object for a later `Full` request, so there
is no cache-staleness or upgrade-in-place concern. This is a primary reason partial evaluation is
limited to `ProjectInstance`: the cached, mutable `Project` model cannot expose partial state safely.

## Relationship to `EvaluationContext`

Partial evaluation and a shared `EvaluationContext` are complementary levers. A shared context
caches file-system probes and SDK resolution across projects; partial evaluation skips whole passes
within each project. Batch scenarios that read one property from many projects (such as the SDK
release-property locator) benefit from using both together.
