// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace MSBuild.Benchmarks;

/// <summary>
///  Common condition shapes shared by parsing and evaluation benchmarks.
/// </summary>
internal static class ConditionStrings
{
    internal const string SimpleEquality = "'$(Configuration)' == 'Debug'";
    internal const string EmptyCheck = "'$(TargetFramework)' == ''";
    internal const string NonEmptyCheck = "'$(TargetFramework)' != ''";
    internal const string NumericComparison = "$(BuildNumber) >= 100";
    internal const string NumericLessThan = "$(ErrorCount) < 5";

    internal const string BooleanAnd = "'$(Configuration)' == 'Debug' And '$(Platform)' == 'AnyCPU'";
    internal const string BooleanOr = "'$(Configuration)' == 'Debug' Or '$(Configuration)' == 'Release'";
    internal const string Negation = "!Exists('$(MissingPath)')";
    internal const string NegatedEquality = "!('$(Configuration)' == 'Release')";

    internal const string Complex = "'$(Configuration)' == 'Debug' And ('$(Platform)' == 'x64' Or '$(Platform)' == 'AnyCPU')";
    internal const string DeepNesting = "((('$(Configuration)' == 'Debug')))";
    internal const string MultipleAnds = "'$(Configuration)' == 'Debug' And '$(Platform)' == 'AnyCPU' And '$(TargetFramework)' == 'net11.0'";
    internal const string MixedAndOr = "'$(A)' == '1' And ('$(B)' == '2' Or '$(C)' == '3') And '$(D)' == '4'";

    internal const string ExistsCheck = "Exists('$(MSBuildProjectDirectory)')";
    internal const string HasTrailingSlashCheck = "HasTrailingSlash('$(OutputPath)')";
    internal const string ExistsWithConcatenation = "Exists('$(ExistingDirectoryRoot)$(DirectorySeparator)$(ExistingDirectoryLeaf)')";

    internal const string ConcatenatedComparison = "'$(Configuration)|$(Platform)' == 'Debug|AnyCPU'";
    internal const string MultipleProperties = "'$(RootNamespace).$(AssemblyName)' == 'MyApp.MyApp'";

    internal const string BooleanLiteralTrue = "'$(IsPackable)' == 'true'";
    internal const string BooleanLiteralFalse = "'$(GenerateDocumentationFile)' == 'false'";
    internal const string BareBoolean = "$(IsPackable)";

    internal const string ItemListCondition = "'@(Compile)' != ''";
    internal const string MetadataCondition = "'%(Extension)' == '.cs'";

    internal const string RealisticSdkCondition =
        "'$(TargetFrameworkIdentifier)' == '.NETCoreApp' And '$(TargetFrameworkVersion)' >= '5.0' And '$(UseWindowsForms)' == 'true'";

    internal const string RealisticMultiTargeting =
        "'$(TargetFramework)' == 'net11.0' Or '$(TargetFramework)' == 'net10.0' Or '$(TargetFramework)' == 'net9.0' Or '$(TargetFramework)' == 'net472'";

    /// <summary>
    ///  Gets all condition strings used by the parsing and evaluation benchmarks.
    /// </summary>
    internal static ImmutableArray<string> AllConditions { get; } =
    [
        SimpleEquality,
        EmptyCheck,
        NonEmptyCheck,
        NumericComparison,
        NumericLessThan,
        BooleanAnd,
        BooleanOr,
        Negation,
        NegatedEquality,
        Complex,
        DeepNesting,
        MultipleAnds,
        MixedAndOr,
        ExistsCheck,
        HasTrailingSlashCheck,
        ExistsWithConcatenation,
        ConcatenatedComparison,
        MultipleProperties,
        BooleanLiteralTrue,
        BooleanLiteralFalse,
        BareBoolean,
        ItemListCondition,
        MetadataCondition,
        RealisticSdkCondition,
        RealisticMultiTargeting,
    ];
}
