// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Build.AotValidation;

/// <summary>
/// Validates the host-registration API <see cref="SdkResolver.Register"/> under Native AOT - the seam an
/// AOT host (the .NET SDK CLI) uses to contribute a reflection-free SDK resolver without MSBuild having to
/// discover and load it from disk by reflection.
///
/// The target scenario: a host that runs the engine in-process and is itself trimmed/AOT-compiled hands
/// MSBuild a pre-constructed resolver instance (built with <c>new</c>, e.g. an AOT-ready workload-locator
/// resolver). MSBuild folds it into the same reflection-free pass as its built-in resolver, so it resolves
/// SDKs with no Assembly.LoadFrom and never reaches the dynamic-loading failure (MSB4282) this harness bakes
/// off via <c>EnableSdkResolverDynamicLoading=false</c>.
///
/// Registration is process-global and the engine snapshots its default-resolver list on first use, so the
/// resolver is registered from a <see cref="ModuleInitializerAttribute"/> - before any test evaluates a
/// project - mirroring how a host registers during startup, ahead of the first evaluation. The resolver
/// claims only its own SDK name and defers (returns <see langword="null"/>) for every other SDK, so it
/// leaves all other harness resolution unchanged.
/// </summary>
[TestClass]
public sealed class RegisteredSdkResolverAotTests
{
    [TestMethod]
    public void RegisteredResolver_ResolvesSdk_AndImportsItsPropsAndTargets()
    {
        using var projectDir = new TempDirectory();
        string projectPath = Path.Combine(projectDir.Path, "App.csproj");

        // A <Project Sdk="..."> for an SDK that does NOT live under MSBuildSDKsPath, so the in-box
        // DefaultSdkResolver's directory probe cannot find it. Only the registered resolver can resolve it,
        // which is precisely what makes this a test of the registration seam rather than the in-box path.
        File.WriteAllText(
            projectPath,
            $"""<Project Sdk="{RegisteredSdkResolverFixture.SdkName}"><PropertyGroup><Configuration>Debug</Configuration></PropertyGroup></Project>""");

        using var collection = new ProjectCollection();
        var project = new Project(projectPath, globalProperties: null, toolsVersion: null, collection);

        // The registered resolver returned the fixture directory, so the SDK's implicit Sdk.props (imported
        // at the top) and Sdk.targets (imported at the bottom) both loaded - end-to-end proof that a
        // host-registered, reflection-free resolver participates in resolution under Native AOT.
        Assert.AreEqual("props-value", project.GetPropertyValue("FromRegisteredSdkProps"));
        Assert.AreEqual("targets-value", project.GetPropertyValue("FromRegisteredSdkTargets"));
        Assert.AreEqual(2, project.Imports.Count);
    }
}

/// <summary>
/// Lays down a tiny on-disk SDK (just an Sdk.props and Sdk.targets) and registers a reflection-free resolver
/// for it at module load, mirroring an AOT host that calls <see cref="SdkResolver.Register"/> during startup
/// before the first evaluation. Module-load registration is what guarantees the resolver is present when the
/// engine snapshots its default-resolver list on the first SDK resolution in the process.
/// </summary>
internal static class RegisteredSdkResolverFixture
{
    /// <summary>The SDK name the registered resolver owns. Distinct so nothing else in the harness resolves it.</summary>
    internal const string SdkName = "Harness.Registered.Sdk";

    // The on-disk SDK fixture is process-scoped: it must outlive every evaluation, so it is held in a static
    // field (which also transfers disposal ownership off the module initializer) and disposed at process exit.
    private static TempDirectory? s_sdkFixture;

    [ModuleInitializer]
    internal static void Register()
    {
        TempDirectory sdkFixture = new();
        s_sdkFixture = sdkFixture;
        File.WriteAllText(
            Path.Combine(sdkFixture.Path, "Sdk.props"),
            "<Project><PropertyGroup><FromRegisteredSdkProps>props-value</FromRegisteredSdkProps></PropertyGroup></Project>");
        File.WriteAllText(
            Path.Combine(sdkFixture.Path, "Sdk.targets"),
            "<Project><PropertyGroup><FromRegisteredSdkTargets>targets-value</FromRegisteredSdkTargets></PropertyGroup></Project>");

        // Construct the resolver directly (no reflection, no Assembly.LoadFrom) and register it - the exact
        // call an AOT host makes. It works under AOT because Register only stores the instance and the engine
        // folds it into the reflection-free default-resolver pass, by Priority, alongside the built-in resolver.
        SdkResolver.Register(new FixedPathSdkResolver(SdkName, sdkFixture.Path));

        AppDomain.CurrentDomain.ProcessExit += (_, _) => sdkFixture.Dispose();
    }
}

/// <summary>
/// A minimal reflection-free <see cref="SdkResolver"/>: it resolves one SDK name to a fixed directory and
/// defers (returns <see langword="null"/>) for everything else. This is the shape a host bakes in for AOT
/// (for example a workload-locator resolver) - constructed with <c>new</c>, with no assembly loading and no
/// reflection on the resolution path.
/// </summary>
internal sealed class FixedPathSdkResolver : SdkResolver
{
    private readonly string _sdkName;
    private readonly string _sdkPath;

    internal FixedPathSdkResolver(string sdkName, string sdkPath)
    {
        _sdkName = sdkName;
        _sdkPath = sdkPath;
    }

    public override string Name => nameof(FixedPathSdkResolver);

    // Below the in-box DefaultSdkResolver's 10000 so this is consulted first, but it claims only its own SDK;
    // every other SDK falls through to the in-box resolver exactly as before.
    public override int Priority => 100;

    public override SdkResult? Resolve(SdkReference sdkReference, SdkResolverContext resolverContext, SdkResultFactory factory)
        => string.Equals(sdkReference.Name, _sdkName, StringComparison.OrdinalIgnoreCase)
            ? factory.IndicateSuccess(_sdkPath, sdkReference.Version ?? string.Empty)
            : null;
}
