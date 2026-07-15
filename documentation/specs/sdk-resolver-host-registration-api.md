# API Proposal: Host registration of reflection-free SDK resolvers

**Status:** Implemented as the static `SdkResolver.Register(SdkResolver)` member
([SdkResolver.cs](../../src/Framework/Sdk/SdkResolver.cs)). The proposal below is the original design
rationale.

## Background and motivation

MSBuild resolves `<Project Sdk="..." />` references through a chain of `SdkResolver`s. On .NET, that chain is:

1. The built-in `DefaultSdkResolver` - a reflection-free directory probe of `MSBuildSDKsPath\<name>\Sdk`,
   constructed with `new` and tried first
   ([`SdkResolverLoader.GetDefaultResolvers()`](../../src/Build/BackEnd/Components/SdkResolution/SdkResolverLoader.cs)).
2. Plugin resolvers discovered on disk under the `SdkResolvers` directory and **loaded by reflection**
   (`Assembly.LoadFrom` + `Activator.CreateInstance`) - notably
   `Microsoft.NET.Sdk.WorkloadMSBuildSdkResolver` and `Microsoft.Build.NuGetSdkResolver`.

The plugin step is incompatible with a trimmed / Native AOT host. MSBuild already gates it behind the
`Microsoft.Build.EnableSdkResolverDynamicLoading` feature switch; when that switch is off (as it is for an
AOT host), [`SdkResolverService.GetResolvers`](../../src/Build/BackEnd/Components/SdkResolution/SdkResolverService.cs)
fails observably with **MSB4282** instead of attempting an unsupported assembly load.

This is a real wall for evaluating stock SDK projects under AOT. Every `Microsoft.NET.Sdk` project
unconditionally imports two **workload-locator SDKs** -
`Microsoft.NET.SDK.WorkloadAutoImportPropsLocator` and `Microsoft.NET.SDK.WorkloadManifestTargetsLocator` -
which only the (reflection-loaded) workload resolver knows how to resolve. With dynamic loading off, a
plain `dotnet new console` project cannot be evaluated; the only workaround today is to set
`MSBuildEnableWorkloadResolver=false`, which *disables* workload resolution rather than supporting it.

A host that runs the MSBuild engine in-process and is itself trimmed/AOT-compiled (the .NET SDK CLI is the
motivating case) already references its resolver assemblies statically. It needs a way to hand MSBuild a
pre-constructed resolver instance and have it participate in resolution **without any assembly loading or
reflection**. No such API exists today: resolvers can only be contributed by being discovered on disk.

This proposal adds a minimal host-registration surface so an in-process host can "bake in" resolvers - for
example a Native-AOT-compatible workload resolver - that run on the existing reflection-free code path.

## API Proposal

```diff
 namespace Microsoft.Build.Framework;

 // Existing public contract a resolver implements; this proposal adds a single static member to it.
 public abstract class SdkResolver
 {
     public abstract string Name { get; }
     public abstract int Priority { get; }
     public abstract SdkResult Resolve(SdkReference sdkReference, SdkResolverContext resolverContext, SdkResultFactory factory);

+    /// <summary>
+    /// Registers an <see cref="SdkResolver"/> to be consulted during SDK resolution by a host that runs
+    /// the MSBuild engine in-process (for example the .NET SDK CLI), without MSBuild discovering and
+    /// loading it from disk by reflection.
+    /// </summary>
+    /// <param name="resolver">The resolver instance to register.</param>
+    /// <remarks>
+    /// <para>
+    /// This is the supported way to provide SDK resolvers in a trimmed or Native AOT host, where the
+    /// on-disk <c>SdkResolvers</c> probing and reflection-based loading used for plugin resolvers are
+    /// unavailable. The registered resolver is consulted on the same reflection-free code path as
+    /// MSBuild's built-in resolver, so it never triggers the dynamic-loading failure (MSB4282).
+    /// </para>
+    /// <para>
+    /// The registered resolver participates in resolution in <see cref="Priority"/> order alongside
+    /// MSBuild's built-in resolver, with no assembly loading or reflection. It is consulted for every SDK
+    /// reference in the process.
+    /// </para>
+    /// <para>
+    /// Intended to be called once per resolver during host initialization, before the first project is
+    /// evaluated. The set of registered resolvers is captured the first time an SDK is resolved in the
+    /// process; registrations performed after that point are not guaranteed to take effect.
+    /// </para>
+    /// <para>
+    /// This method is thread-safe. Registering the same instance more than once has no additional effect.
+    /// </para>
+    /// </remarks>
+    /// <exception cref="System.ArgumentNullException"><paramref name="resolver"/> is <see langword="null"/>.</exception>
+    public static void Register(SdkResolver resolver);
 }
```

### Semantics

- **Reflection-free.** Registered resolvers are appended to the list returned by the engine's existing
  reflection-free default-resolver step. On .NET that list is tried first, before any manifest is read, so
  registered resolvers bypass `EnableSdkResolverDynamicLoading` / MSB4282 entirely.
- **Ordering.** The built-in `DefaultSdkResolver` (`Priority` 10000) and all registered resolvers are
  consulted in ascending `Priority` order, matching how disk-discovered resolvers are ordered today. A
  workload resolver with `Priority` 4000 is therefore tried before the in-box probe; it returns `null` for
  SDKs it does not own, so the in-box probe still resolves `Microsoft.NET.Sdk`.
- **Process scope and lifetime.** Resolution is served by a process-wide singleton
  (`SdkResolverService.Instance`) that all `Project`/`ProjectInstance` evaluations share. Registration is
  correspondingly process-wide and is meant to be performed once at host startup. See **Risks** for the
  caching/timing nuance.
- **No removal.** There is intentionally no `Unregister`/`Clear` on the public surface (host startup is a
  one-time operation). A test-only reset is discussed under **Alternative Designs**.

## API Usage

### Host (the in-process, AOT SDK CLI) registers its resolver at startup

```csharp
using Microsoft.Build.Framework;

// During SDK CLI initialization, before the first evaluation. The resolver type is referenced
// statically and constructed with `new` - no Assembly.LoadFrom, so it works under Native AOT.
SdkResolver.Register(new Microsoft.NET.Sdk.WorkloadMSBuildSdkResolver.WorkloadSdkResolver());

// Optionally also the NuGet-based SDK resolver, for projects that reference `<Project Sdk="Pkg/1.2.3" />`.
// SdkResolver.Register(new Microsoft.Build.NuGetSdkResolver.NuGetSdkResolver());
```

### A minimal in-box-style resolver (the existing shape registered resolvers implement)

```csharp
internal sealed class WorkloadAutoImportLocatorResolver : SdkResolver
{
    public override string Name => "InProcWorkloadLocatorResolver";

    public override int Priority => 4000;

    public override SdkResult Resolve(SdkReference sdk, SdkResolverContext context, SdkResultFactory factory)
    {
        // Returns the <pack>/Sdk directories (or an empty set when no workloads are installed) for the
        // Microsoft.NET.SDK.Workload*Locator SDKs; null for everything else. No reflection, no plugin load.
        if (TryResolveWorkloadLocator(sdk.Name, out IEnumerable<string> paths))
        {
            return factory.IndicateSuccess(paths, sdk.Version);
        }

        return null; // defer to the next resolver
    }
}
```

### Effect on the validation harness

The AOT validation harness today must disable workloads to evaluate a stock template
([`DotnetTemplateAotTests.cs`](../../src/aot-validation/DotnetTemplateAotTests.cs)):

```csharp
Dictionary<string, string> globalProperties = new() { ["MSBuildEnableWorkloadResolver"] = "false" };
return new Project(projectPath, globalProperties, toolsVersion: null, collection);
```

With this API, the harness can instead register a baked-in locator resolver once and evaluate the project
with no special global property, proving the end-to-end path the SDK CLI would use.

## Alternative Designs

1. **Internal API + `InternalsVisibleTo` for the SDK.** Keep the registration `internal` and grant the SDK
   assembly visibility. Smaller public surface, but couples the SDK to an unversioned internal contract and
   does not help any other in-process host (custom build servers, test platforms). A public, documented
   surface is preferred for a cross-repo boundary.

2. **Per-`ProjectCollection` / `EvaluationContext` registration.** Attach resolvers to the evaluation scope
   instead of the process. More precise lifetime, but SDK resolution is served by a process singleton today;
   threading a per-collection resolver set through evaluation is a substantially larger change and is not
   required by the motivating scenario (the host's resolver set is process-global and fixed).

3. **A provider abstraction instead of imperative registration.**
   `public static void RegisterProvider(Func<IEnumerable<SdkResolver>> provider);` or an
   `ISdkResolverProvider` interface. More flexible (lazy, recomputable) but heavier than the scenario needs;
   the host knows its fixed resolver set at startup.

4. **`params SdkResolver[]` / `IEnumerable<SdkResolver>` overload.** Convenience for registering several at
   once. Compatible with this proposal and can be added later; the single-instance method is the primitive.

5. **A dedicated `SdkResolverRegistry` static class** instead of a static method on `SdkResolver`. This keeps
   the registration mutator off the abstract contract that resolver authors implement, which is arguably a
   cleaner separation of concerns, and is the natural fallback if API review prefers not to add a static,
   factory-style member to the abstract base. The trade-off is a second new public type for a one-method
   feature; the proposed `SdkResolver.Register` adds a single member to an existing type, which is why it is
   the primary design.

6. **Reuse `MSBUILDADDITIONALSDKRESOLVERSFOLDER`.** Rejected: that hook still discovers and loads resolvers
   from disk by reflection, which is exactly what is unavailable under AOT.

## Risks

- **Process-global mutable static.** The registration state is process-wide and mutable, which is generally
  discouraged. It is justified here because SDK resolution is already a process singleton and the resolver
  set is established once per host process. The surface is deliberately minimal (a single `Register`).

- **Registration timing vs. caching.** The engine caches the default-resolver list on first use
  (`CachingSdkResolverLoader`). Resolvers registered after the first SDK resolution in the process may be
  ignored. Mitigations to decide during review: (a) document "register during host startup" (proposed);
  (b) throw `InvalidOperationException` from `Register` once the set has been captured, making the misuse
  loud; or (c) re-read the registry on each evaluation (gives up the cache).

- **Ordering / precedence.** Folding registered resolvers into the reflection-free first pass changes the
  order in which resolvers run relative to the in-box probe (now `Priority`-ordered within that pass).
  Behavior is unchanged for in-box SDKs (a workload resolver returns `null` for them), but the contract
  ("consulted in `Priority` order alongside the built-in resolver") must be explicit.

- **Layering.** The API lives on `Microsoft.Build.Framework.SdkResolver` and accepts only that public type;
  the engine (`Microsoft.Build`) reads the registration state. MSBuild gains no dependency on any SDK type,
  so the dependency direction stays SDK -> MSBuild.

- **Thread-safety.** `Register` must be safe to call concurrently and concurrently with the first
  resolution; the implementation uses a thread-safe collection and an immutable snapshot at capture time.

- **Test isolation.** Because the registration state is process-global, tests that register resolvers need a
  reset hook. Proposed as an `internal` test-only reset (exposed via `InternalsVisibleTo`), not part of the
  public surface.

- **Resolver AOT-readiness is out of scope.** This API only provides the injection seam. The registered
  resolver and its dependencies must themselves be trim/AOT-safe (for the workload resolver this means
  removing `Assembly.Location` use and making the workload-manifest reader trim-safe). That work lives in
  the resolver's owning repo (dotnet/sdk).

## Implementation status

The primary design has been implemented:

- **API.** `public static void Register(SdkResolver resolver)` on
  [`Microsoft.Build.Framework.SdkResolver`](../../src/Framework/Sdk/SdkResolver.cs). Null-checks its
  argument, is thread-safe (`lock`), and de-duplicates the same instance. Registration state is held in a
  process-global `private static List<SdkResolver>`.
- **Engine wiring.**
  [`SdkResolverLoader.GetDefaultResolvers()`](../../src/Build/BackEnd/Components/SdkResolution/SdkResolverLoader.cs)
  folds the registered resolvers into the reflection-free default-resolver pass and sorts the combined set
  by `Priority`. This is the path tried first on .NET (before any manifest is read) and the one pre-populated
  as a general manifest on .NET Framework, so registered resolvers never reach the dynamic-loading failure
  (MSB4282). The engine reads the registration through an `internal` snapshot property
  (`SdkResolver.RegisteredResolvers`) exposed via the existing Framework -> Microsoft.Build `InternalsVisibleTo`.
- **Caching/timing nuance.** Resolved as option (a) from **Risks**: documented "register during host startup,
  before the first evaluation." No `InvalidOperationException`-on-late-registration guard was added.
- **Test isolation.** An `internal static void ClearRegisteredResolversForTests()` reset hook (test-only, via
  `InternalsVisibleTo`) was added; no removal API is on the public surface.
- **Tests.** Four tests in
  [`SdkResolverService_Tests.cs`](../../src/Build.UnitTests/BackEnd/SdkResolverService_Tests.cs) cover the
  null-argument throw, idempotent re-registration, `Priority`-ordered inclusion in
  `GetDefaultResolvers()`, and an end-to-end resolve through `SdkResolverService`. They run on both
  `net10.0` and `net472`.
