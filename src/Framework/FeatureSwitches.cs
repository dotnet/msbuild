// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build.Framework;

/// <summary>
/// Aggregates MSBuild's trimmer feature switches.
/// </summary>
/// <remarks>
/// This is the single registry for MSBuild feature switches across the product. It lives in
/// Microsoft.Build.Framework because Framework is the lowest assembly in the stack - the engine and
/// tasks reference it but it references neither - so every assembly can read these switches. Each
/// property is a <c>[FeatureSwitchDefinition]</c> mapped to an AppContext switch (so the trimmer can
/// substitute a constant and remove the guarded branch); where it gates trim-unsafe reflection it is
/// also a <c>[FeatureGuard]</c> (so the analyzer treats the guarded branch as safe). Trimmed defaults
/// are declared by matching <c>RuntimeHostConfigurationOption</c> items in Microsoft.Build.Framework.csproj.
/// New feature switches should be added here so they can be discovered and configured in one place.
/// </remarks>
internal static class FeatureSwitches
{
    private const bool EnableCustomPluginProbingByDefault = true;

    /// <summary>
    /// Whether MSBuild may probe for and load plugin and task assemblies by path at run time. When
    /// <see langword="true"/> (the default under the JIT) the custom assembly resolvers
    /// (<c>MSBuildLoadContext</c> for plugin dependencies and <c>TaskEngineAssemblyResolver</c> for task
    /// assemblies) resolve assemblies themselves, which is reflection incompatible with trimming. When
    /// <see langword="false"/> (the substituted default in a trimmed or AOT application) custom probing
    /// is skipped and resolution falls back to the default load behavior.
    /// </summary>
    /// <remarks>
    /// This is both a <c>[FeatureSwitchDefinition]</c> - so the trimmer substitutes a constant
    /// <see langword="false"/> and removes the probing branch from a trimmed application - and a
    /// <c>[FeatureGuard]</c> for <c>RequiresUnreferencedCode</c>, so the analyzer treats
    /// <c>if (EnableCustomPluginProbing)</c> as guarding the trim-unsafe <c>LoadFromAssemblyPath</c>
    /// calls and no per-call suppression is required. The trimmed default is declared by a matching
    /// <c>RuntimeHostConfigurationOption</c> in Microsoft.Build.Framework.csproj.
    /// </remarks>
    [FeatureSwitchDefinition("Microsoft.Build.EnableCustomPluginProbing")]
    [FeatureGuard(typeof(RequiresUnreferencedCodeAttribute))]
#pragma warning disable IL4000 // The analyzer can't model the AppContext-switch body (it can't see the trimmed default in the csproj), so it can't prove the guard is false when trimming; ILLink applies that substitution and removes the guarded probing branch. Same pattern as the BCL feature guards (e.g. DataSet.XmlSerializationIsSupported).
    internal static bool EnableCustomPluginProbing =>
        AppContext.TryGetSwitch("Microsoft.Build.EnableCustomPluginProbing", out bool isEnabled)
            ? isEnabled
            : EnableCustomPluginProbingByDefault;
#pragma warning restore IL4000

    /// <summary>
    /// Whether MSBuild may probe and load assemblies at run time to resolve arbitrary property-function
    /// receiver types. When <see langword="false"/> (the default in a trimmed or AOT application),
    /// receiver types are restricted to the curated allowlist in <c>AvailableStaticMethods</c>, all of
    /// which are statically known and preserved. When <see langword="true"/>, MSBuild additionally probes
    /// assemblies at run time - reflection that is incompatible with trimming.
    /// </summary>
    /// <remarks>
    /// This is both a <c>[FeatureSwitchDefinition]</c> - so the trimmer substitutes a constant
    /// <see langword="false"/> and removes the probing path from a trimmed application - and a
    /// <c>[FeatureGuard]</c> for <c>RequiresUnreferencedCode</c>, so the analyzer treats
    /// <c>if (EnableAllPropertyFunctions)</c> as guarding the trim-unsafe probing and no per-call
    /// suppression is required. In untrimmed builds, the legacy <c>MSBUILDENABLEALLPROPERTYFUNCTIONS</c>
    /// environment variable still enables run-time type probing when the AppContext switch is unset. In
    /// trimmed/AOT builds, the trimmer substitutes this property to <see langword="false"/> before the body
    /// runs, so that environment variable cannot re-open the removed probing path. The trimmed default is
    /// declared by a matching <c>RuntimeHostConfigurationOption</c> in Microsoft.Build.Framework.csproj and
    /// flows to package consumers through the package's buildTransitive targets.
    /// </remarks>
    [FeatureSwitchDefinition("Microsoft.Build.EnableAllPropertyFunctions")]
    [FeatureGuard(typeof(RequiresUnreferencedCodeAttribute))]
#pragma warning disable IL4000 // The Roslyn analyzer can't see the trimmed default (the RuntimeHostConfigurationOption in Microsoft.Build.Framework.csproj), so it can't prove this guard is false when trimming; the ILLink trimmer applies that substitution and removes the guarded probing branch. Same pattern as BCL feature guards (e.g. DataSet.XmlSerializationIsSupported, TypeDescriptor.IsComObjectDescriptorSupported).
    internal static bool EnableAllPropertyFunctions =>
        AppContext.TryGetSwitch("Microsoft.Build.EnableAllPropertyFunctions", out bool isEnabled)
            ? isEnabled
            : Environment.GetEnvironmentVariable("MSBUILDENABLEALLPROPERTYFUNCTIONS") == "1";
#pragma warning restore IL4000

    /// <summary>
    /// Whether instance property-function calls are limited to a curated set of receiver types.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When enabled, instance "dotting in" is restricted to a curated set of receiver types (the engine's
    /// <c>PropertyFunctionReceiver</c>), so the members reachable by reflection are predictable and
    /// statically known. When disabled, any public instance member except <c>GetType</c> is callable,
    /// preserving the historical behavior.
    /// </para>
    /// <para>
    /// The untrimmed default is <see langword="false"/>; under trimming the constant is substituted
    /// <see langword="true"/> so the unrestricted branch is removed, keeping the property-function path
    /// trim compatible. This switch is set only through its AppContext switch; it has no environment
    /// variable.
    /// </para>
    /// </remarks>
    [FeatureSwitchDefinition("Microsoft.Build.RestrictPropertyFunctionReceivers")]
    internal static bool RestrictPropertyFunctionReceivers =>
        AppContext.TryGetSwitch("Microsoft.Build.RestrictPropertyFunctionReceivers", out bool isEnabled) && isEnabled;

    private const bool EnableSdkResolverDynamicLoadingByDefault = true;

    /// <summary>
    /// Whether MSBuild may load SDK resolver plugin assemblies from disk by reflection. When
    /// <see langword="true"/> (the default under the JIT) MSBuild discovers and loads SDK resolver
    /// assemblies to resolve SDKs that the built-in, reflection-free <c>DefaultSdkResolver</c> cannot.
    /// When <see langword="false"/> (substituted by the trimmer) an SDK that can only be resolved by a
    /// dynamically loaded resolver fails observably with a reported project error instead, and the
    /// reflective resolver-loading path is removed from a trimmed application.
    /// </summary>
    /// <remarks>
    /// Both a <c>[FeatureSwitchDefinition]</c> (so the trimmer substitutes the constant and removes the
    /// guarded loading branch) and a <c>[FeatureGuard]</c> for <c>RequiresUnreferencedCode</c> (so the
    /// analyzer treats <c>if (EnableSdkResolverDynamicLoading)</c> as guarding the trim-unsafe load and
    /// no per-call suppression is required). A pure AppContext switch; the trimmed default is declared by
    /// a matching <c>RuntimeHostConfigurationOption</c> in Microsoft.Build.Framework.csproj.
    /// </remarks>
    [FeatureSwitchDefinition("Microsoft.Build.EnableSdkResolverDynamicLoading")]
    [FeatureGuard(typeof(RequiresUnreferencedCodeAttribute))]
#pragma warning disable IL4000 // The Roslyn analyzer can't see the trimmed default (the RuntimeHostConfigurationOption in Microsoft.Build.Framework.csproj), so it can't prove this guard is false when trimming; the ILLink trimmer applies that substitution and removes the guarded loading branch. Same pattern as BCL feature guards.
    internal static bool EnableSdkResolverDynamicLoading =>
        AppContext.TryGetSwitch("Microsoft.Build.EnableSdkResolverDynamicLoading", out bool isEnabled)
            ? isEnabled
            : EnableSdkResolverDynamicLoadingByDefault;
#pragma warning restore IL4000

    private const bool EnableConfigurationFileToolsetsByDefault = true;

    /// <summary>
    /// Whether MSBuild reads toolset definitions from the application configuration file (the
    /// <c>&lt;msbuildToolsets&gt;</c> section of an <c>.exe.config</c>/app.config) when a caller requests
    /// <c>ToolsetDefinitionLocations.ConfigurationFile</c>. When <see langword="true"/> (the default under
    /// the JIT) the configuration reader runs. When <see langword="false"/> (substituted by the trimmer)
    /// the configuration-reading branch is removed, which lets the trimmer drop the entire
    /// <c>ToolsetConfigurationReader</c> subtree and the <c>System.Configuration.ConfigurationManager</c>
    /// dependency from a trimmed/AOT application. The configuration file is not one of the default toolset
    /// locations on .NET, so hosts that do not opt in are unaffected; a host that disables the switch and
    /// still requests <c>ToolsetDefinitionLocations.ConfigurationFile</c> gets an <see cref="ArgumentException"/>
    /// (failing observably) rather than silently missing those toolsets.
    /// </summary>
    /// <remarks>
    /// A pure AppContext switch with a <c>[FeatureSwitchDefinition]</c> so the trimmer folds it to the
    /// constant declared by the matching <c>RuntimeHostConfigurationOption</c> in Microsoft.Build.Framework.csproj.
    /// No <c>[FeatureGuard]</c> is needed: the guarded code is not <c>[RequiresUnreferencedCode]</c>; the
    /// switch exists purely to let the trimmer remove an otherwise-reachable assembly reference.
    /// </remarks>
    [FeatureSwitchDefinition("Microsoft.Build.EnableConfigurationFileToolsets")]
    internal static bool EnableConfigurationFileToolsets =>
        AppContext.TryGetSwitch("Microsoft.Build.EnableConfigurationFileToolsets", out bool isEnabled)
            ? isEnabled
            : EnableConfigurationFileToolsetsByDefault;

    private const bool EnableReflectiveTaskExecutionByDefault = true;

    /// <summary>
    /// Whether MSBuild may load and execute tasks by reflecting over task assemblies and task types
    /// discovered at run time. When <see langword="true"/> (the default under the JIT) the engine
    /// instantiates tasks by reflection (loading the task assembly, resolving the task type, calling
    /// the task factory, and binding parameters) - the reflective leaf the whole build-execution path
    /// reaches. When <see langword="false"/> (the substituted default in a trimmed or AOT application)
    /// the engine does not attempt reflective task execution: the gated leaves report an observable
    /// build error (<c>ReflectiveTaskExecutionNotSupported</c>, MSB4283) and the trimmer removes the
    /// reflective instantiation path from the image, so a trimmed/AOT host fails observably and can fall
    /// back to a JIT MSBuild instead of crashing in reflection.
    /// </summary>
    /// <remarks>
    /// Both a <c>[FeatureSwitchDefinition]</c> (so the trimmer substitutes the constant and removes the
    /// reflective instantiation branch) and a <c>[FeatureGuard]</c> for <c>RequiresUnreferencedCode</c>
    /// (so the analyzer treats <c>if (EnableReflectiveTaskExecution)</c> as guarding the trim-unsafe
    /// reflection and no per-call suppression is required up the build-execution chain). This is the
    /// leaf gate that lets the engine-internal build-execution methods drop their
    /// <c>[RequiresUnreferencedCode]</c>; the public <c>ITaskFactory</c> contract keeps its honest RUC
    /// for callers that reach it directly. The task-registration API
    /// (<c>Microsoft.Build.Utilities.Task.RegisterTask</c> backed by <c>TaskClassRegistry</c>, see
    /// task-class-registration-api.md) lets host-registered tasks run with this switch <em>off</em> - the
    /// engine constructs them reflection-free - and the intrinsic <c>MSBuild</c>/<c>CallTarget</c> tasks
    /// resolve the same way. A pure AppContext switch; the trimmed default is declared by a matching
    /// <c>RuntimeHostConfigurationOption</c> in Microsoft.Build.Framework.csproj.
    /// </remarks>
    [FeatureSwitchDefinition("Microsoft.Build.EnableReflectiveTaskExecution")]
    [FeatureGuard(typeof(RequiresUnreferencedCodeAttribute))]
#pragma warning disable IL4000 // The analyzer can't model the AppContext-switch body (it can't see the trimmed default in the csproj), so it can't prove the guard is false when trimming; ILLink applies that substitution and removes the guarded reflective-execution branch. Same pattern as the other feature guards (e.g. EnableCustomPluginProbing).
    internal static bool EnableReflectiveTaskExecution =>
        AppContext.TryGetSwitch("Microsoft.Build.EnableReflectiveTaskExecution", out bool isEnabled)
            ? isEnabled
            : EnableReflectiveTaskExecutionByDefault;
#pragma warning restore IL4000

    private const bool EnableReflectiveTaskParameterTypesByDefault = true;

    /// <summary>
    /// Whether MSBuild may resolve a task parameter type declared by name (the <c>ParameterType</c> of a
    /// <c>&lt;UsingTask&gt;</c> <c>&lt;ParameterGroup&gt;</c> parameter) by reflecting over the loaded
    /// assemblies with <see cref="System.Type.GetType(string)"/>. When <see langword="true"/> (the default
    /// under the JIT) an unregistered type name falls back to <c>Type.GetType</c>. When <see langword="false"/>
    /// (the substituted default in a trimmed or AOT application) only types in the statically-known
    /// <c>TaskParameterTypeRegistry</c> (the intrinsic value types, <c>string</c>, and the MSBuild
    /// <see cref="ITaskItem"/> types, plus any a host has registered) resolve; an unregistered name fails
    /// observably with a reported project error instead, and the trimmer removes the reflective
    /// name-resolution branch from the image.
    /// </summary>
    /// <remarks>
    /// Both a <c>[FeatureSwitchDefinition]</c> (so the trimmer substitutes the constant and removes the
    /// reflective <c>Type.GetType</c> branch) and a <c>[FeatureGuard]</c> for <c>RequiresUnreferencedCode</c>
    /// (so the analyzer treats <c>if (EnableReflectiveTaskParameterTypes)</c> as guarding the trim-unsafe
    /// by-name resolution and no per-call suppression is required). The registry is always consulted first,
    /// reflection-free, on both the JIT and trimmed paths; this switch only gates the fallback for names the
    /// registry does not know. A pure AppContext switch; the trimmed default is declared by a matching
    /// <c>RuntimeHostConfigurationOption</c> in Microsoft.Build.Framework.csproj.
    /// </remarks>
    [FeatureSwitchDefinition("Microsoft.Build.EnableReflectiveTaskParameterTypes")]
    [FeatureGuard(typeof(RequiresUnreferencedCodeAttribute))]
#pragma warning disable IL4000 // The analyzer can't model the AppContext-switch body (it can't see the trimmed default in the csproj), so it can't prove the guard is false when trimming; ILLink applies that substitution and removes the guarded by-name resolution branch. Same pattern as the other feature guards (e.g. EnableReflectiveTaskExecution).
    internal static bool EnableReflectiveTaskParameterTypes =>
        AppContext.TryGetSwitch("Microsoft.Build.EnableReflectiveTaskParameterTypes", out bool isEnabled)
            ? isEnabled
            : EnableReflectiveTaskParameterTypesByDefault;
#pragma warning restore IL4000

    private const bool EnableReflectiveLoggerLoadingByDefault = true;

    /// <summary>
    /// Whether MSBuild may create a logger from a <c>LoggerDescription</c> (a logger named by its assembly
    /// and class) by reflecting over the logger assembly at run time. When <see langword="true"/> (the
    /// default under the JIT) a distributed/forwarding logger described by name is loaded and instantiated by
    /// reflection. When <see langword="false"/> (the substituted default in a trimmed or AOT application)
    /// that reflective load is not attempted: a logger described by name fails observably with a reported
    /// error instead, and the trimmer removes the reflective logger-loading path (and its
    /// <c>MetadataLoadContext</c>/<c>TypeLoader</c> dependency) from the image. Loggers supplied to the
    /// engine as already-constructed <see cref="ILogger"/> instances are unaffected and remain the supported
    /// way to log under trimming/AOT.
    /// </summary>
    /// <remarks>
    /// Both a <c>[FeatureSwitchDefinition]</c> (so the trimmer substitutes the constant and removes the
    /// reflective forwarding-logger creation branch) and a <c>[FeatureGuard]</c> for
    /// <c>RequiresUnreferencedCode</c> (so the analyzer treats <c>if (EnableReflectiveLoggerLoading)</c> as
    /// guarding the trim-unsafe <c>LoggerDescription.CreateForwardingLogger</c> call and no per-call
    /// suppression is required). A pure AppContext switch; the trimmed default is declared by a matching
    /// <c>RuntimeHostConfigurationOption</c> in Microsoft.Build.Framework.csproj.
    /// </remarks>
    [FeatureSwitchDefinition("Microsoft.Build.EnableReflectiveLoggerLoading")]
    [FeatureGuard(typeof(RequiresUnreferencedCodeAttribute))]
#pragma warning disable IL4000 // The analyzer can't model the AppContext-switch body (it can't see the trimmed default in the csproj), so it can't prove the guard is false when trimming; ILLink applies that substitution and removes the guarded reflective logger-loading branch. Same pattern as the other feature guards (e.g. EnableReflectiveTaskExecution).
    internal static bool EnableReflectiveLoggerLoading =>
        AppContext.TryGetSwitch("Microsoft.Build.EnableReflectiveLoggerLoading", out bool isEnabled)
            ? isEnabled
            : EnableReflectiveLoggerLoadingByDefault;
#pragma warning restore IL4000
}
