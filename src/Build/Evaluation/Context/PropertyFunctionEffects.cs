// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Microsoft.Build.Evaluation.Context;

/// <summary>
/// How a property function depends on the world outside its arguments.
/// </summary>
internal enum PropertyFunctionEffect
{
    /// <summary>Depends only on its arguments or on state that does not change while the process lives.</summary>
    Pure,
    ProbeFile,
    ProbeDirectory,
    ProbePath,
    ReadFile,
    ReadDirectory,
    ReadEnvironment,
    /// <summary>Reads every environment variable referenced as <c>%NAME%</c> in the first argument.</summary>
    ExpandEnvironment,
    /// <summary>Reads installed state, constant for the process, unless an argument names a directory or file to search.</summary>
    PureUnlessPathArgument,
    Registry,
    Volatile,
    Unsupported,
}

/// <summary>
/// Classifies the property functions MSBuild allows (see <c>AvailableStaticMethods</c>) so the recorder knows
/// which argument is a file system or environment input and which results can never be cached.
/// Anything not listed is unsupported: that keeps a new allowed function from silently going unrecorded.
/// </summary>
internal static class PropertyFunctionEffects
{
    private const string ToolLocationHelperTypeName = "Microsoft.Build.Utilities.ToolLocationHelper";

    private static readonly FrozenDictionary<Type, TypeEffects> s_effectsByType = CreateEffectsByType();

    private static readonly FrozenSet<string> s_pureFileSystemInfoMembers = FrozenSet.ToFrozenSet(
        ["Name", "FullName", "Extension", "Parent", "Root", "DirectoryName", "ToString"],
        StringComparer.OrdinalIgnoreCase);

    // ToolLocationHelper members that read what the caller names rather than what is installed, or change state.
    private static readonly FrozenSet<string> s_probingToolLocationHelperMembers = FrozenSet.ToFrozenSet(
        ["FindRootFolderWhereAllFilesExist", "GetAssemblyFoldersFromConfigInfo", "ClearSDKStaticCache"],
        StringComparer.OrdinalIgnoreCase);

    internal static PropertyFunctionEffect Classify(Type receiverType, string member, bool isInstance, int argumentCount)
    {
        if (isInstance)
        {
            // Instance calls run on values evaluation already produced. Only file system objects reach the disk.
            bool touchesFileSystem = typeof(FileSystemInfo).IsAssignableFrom(receiverType) || receiverType == typeof(DriveInfo);
            return touchesFileSystem && !s_pureFileSystemInfoMembers.Contains(member)
                ? PropertyFunctionEffect.Unsupported
                : PropertyFunctionEffect.Pure;
        }

        if (!s_effectsByType.TryGetValue(receiverType, out TypeEffects? effects))
        {
            if (!string.Equals(receiverType.FullName, ToolLocationHelperTypeName, StringComparison.Ordinal))
            {
                return PropertyFunctionEffect.Unsupported;
            }

            // ToolLocationHelper reads installed SDK and framework locations, constant while the process lives, unless
            // the caller names the roots to search or the file to read.
            return s_probingToolLocationHelperMembers.Contains(member)
                ? PropertyFunctionEffect.Unsupported
                : PropertyFunctionEffect.PureUnlessPathArgument;
        }

        PropertyFunctionEffect effect = effects.Members.TryGetValue(member, out PropertyFunctionEffect known) ? known : effects.Default;
        return effect switch
        {
            PropertyFunctionEffect.ReadEnvironment or PropertyFunctionEffect.ExpandEnvironment when argumentCount != 1 => PropertyFunctionEffect.Unsupported,
            PropertyFunctionEffect.ReadDirectory when argumentCount > 2 => PropertyFunctionEffect.Unsupported, // a SearchOption argument may recurse
            _ => effect,
        };
    }

    private static FrozenDictionary<Type, TypeEffects> CreateEffectsByType()
    {
        var byType = new Dictionary<Type, TypeEffects>
        {
            [typeof(Environment)] = new(PropertyFunctionEffect.Unsupported, new()
            {
                ["GetEnvironmentVariable"] = PropertyFunctionEffect.ReadEnvironment,
                ["ExpandEnvironmentVariables"] = PropertyFunctionEffect.ExpandEnvironment,
                ["StackTrace"] = PropertyFunctionEffect.Volatile,
                ["TickCount"] = PropertyFunctionEffect.Volatile,
                ["TickCount64"] = PropertyFunctionEffect.Volatile,
                ["WorkingSet"] = PropertyFunctionEffect.Volatile,
                ["CommandLine"] = PropertyFunctionEffect.Pure,
                ["GetFolderPath"] = PropertyFunctionEffect.Pure,
                ["Is64BitOperatingSystem"] = PropertyFunctionEffect.Pure,
                ["Is64BitProcess"] = PropertyFunctionEffect.Pure,
                ["MachineName"] = PropertyFunctionEffect.Pure,
                ["NewLine"] = PropertyFunctionEffect.Pure,
                ["OSVersion"] = PropertyFunctionEffect.Pure,
                ["ProcessorCount"] = PropertyFunctionEffect.Pure,
                ["SystemDirectory"] = PropertyFunctionEffect.Pure,
                ["SystemPageSize"] = PropertyFunctionEffect.Pure,
                ["UserDomainName"] = PropertyFunctionEffect.Pure,
                ["UserInteractive"] = PropertyFunctionEffect.Pure,
                ["UserName"] = PropertyFunctionEffect.Pure,
                ["Version"] = PropertyFunctionEffect.Pure,
            }),
            // The manifest holds a path's kind, last write time, and length, so reads of other fields (attributes,
            // creation and access times) stay unsupported: a change to them alone would validate as unchanged.
            [typeof(File)] = new(PropertyFunctionEffect.Unsupported, new()
            {
                ["Exists"] = PropertyFunctionEffect.ProbeFile,
                ["ReadAllText"] = PropertyFunctionEffect.ReadFile,
                ["ReadAllBytes"] = PropertyFunctionEffect.ReadFile,
                ["ReadAllLines"] = PropertyFunctionEffect.ReadFile,
                ["GetLastWriteTime"] = PropertyFunctionEffect.ReadFile,
                ["GetLastWriteTimeUtc"] = PropertyFunctionEffect.ReadFile,
            }),
            [typeof(Directory)] = new(PropertyFunctionEffect.Unsupported, new()
            {
                ["Exists"] = PropertyFunctionEffect.ProbeDirectory,
                ["GetDirectories"] = PropertyFunctionEffect.ReadDirectory,
                ["GetFiles"] = PropertyFunctionEffect.ReadDirectory,
                ["GetFileSystemEntries"] = PropertyFunctionEffect.ReadDirectory,
                ["EnumerateDirectories"] = PropertyFunctionEffect.ReadDirectory,
                ["EnumerateFiles"] = PropertyFunctionEffect.ReadDirectory,
                ["EnumerateFileSystemEntries"] = PropertyFunctionEffect.ReadDirectory,
                ["GetLastWriteTime"] = PropertyFunctionEffect.ReadDirectory,
                ["GetLastWriteTimeUtc"] = PropertyFunctionEffect.ReadDirectory,
                ["GetParent"] = PropertyFunctionEffect.Pure,
                ["GetDirectoryRoot"] = PropertyFunctionEffect.Pure,
                ["GetCurrentDirectory"] = PropertyFunctionEffect.Pure,
            }),
            // The working directory is part of the key, so resolving a relative path against it is pure.
            [typeof(Path)] = new(PropertyFunctionEffect.Pure, new()
            {
                ["Exists"] = PropertyFunctionEffect.ProbePath,
                ["GetTempFileName"] = PropertyFunctionEffect.Unsupported,
                ["GetRandomFileName"] = PropertyFunctionEffect.Volatile,
            }),
            // Parsing a time without a date fills in today's date, so the parse functions count as volatile.
            [typeof(DateTime)] = new(PropertyFunctionEffect.Pure, new()
            {
                ["Now"] = PropertyFunctionEffect.Volatile,
                ["UtcNow"] = PropertyFunctionEffect.Volatile,
                ["Today"] = PropertyFunctionEffect.Volatile,
                ["Parse"] = PropertyFunctionEffect.Volatile,
                ["ParseExact"] = PropertyFunctionEffect.Volatile,
                ["TryParse"] = PropertyFunctionEffect.Volatile,
                ["TryParseExact"] = PropertyFunctionEffect.Volatile,
            }),
            [typeof(DateTimeOffset)] = new(PropertyFunctionEffect.Pure, new()
            {
                ["Now"] = PropertyFunctionEffect.Volatile,
                ["UtcNow"] = PropertyFunctionEffect.Volatile,
                ["Parse"] = PropertyFunctionEffect.Volatile,
                ["ParseExact"] = PropertyFunctionEffect.Volatile,
                ["TryParse"] = PropertyFunctionEffect.Volatile,
                ["TryParseExact"] = PropertyFunctionEffect.Volatile,
            }),
            [typeof(Convert)] = new(PropertyFunctionEffect.Pure, new()
            {
                ["ToDateTime"] = PropertyFunctionEffect.Volatile,
            }),
            [typeof(Guid)] = new(PropertyFunctionEffect.Pure, new()
            {
                ["NewGuid"] = PropertyFunctionEffect.Volatile,
            }),
            // Every intrinsic is listed so that a new one fails closed until it is classified. GetPathOfFileAbove and
            // GetDirectoryNameOfFileAbove probe through the evaluation file system, which the recording wrapper observes.
            // Install-location getters and platform checks are constant for the process.
            [typeof(IntrinsicFunctions)] = new(PropertyFunctionEffect.Unsupported, new()
            {
                ["FileExists"] = PropertyFunctionEffect.ProbeFile,
                ["DirectoryExists"] = PropertyFunctionEffect.ProbeDirectory,
                ["GetRegistryValue"] = PropertyFunctionEffect.Registry,
                ["GetRegistryValueFromView"] = PropertyFunctionEffect.Registry,
                ["Add"] = PropertyFunctionEffect.Pure,
                ["Subtract"] = PropertyFunctionEffect.Pure,
                ["Multiply"] = PropertyFunctionEffect.Pure,
                ["Divide"] = PropertyFunctionEffect.Pure,
                ["Modulo"] = PropertyFunctionEffect.Pure,
                ["BitwiseAnd"] = PropertyFunctionEffect.Pure,
                ["BitwiseOr"] = PropertyFunctionEffect.Pure,
                ["BitwiseXor"] = PropertyFunctionEffect.Pure,
                ["BitwiseNot"] = PropertyFunctionEffect.Pure,
                ["LeftShift"] = PropertyFunctionEffect.Pure,
                ["RightShift"] = PropertyFunctionEffect.Pure,
                ["RightShiftUnsigned"] = PropertyFunctionEffect.Pure,
                ["AreFeaturesEnabled"] = PropertyFunctionEffect.Pure,
                ["CheckFeatureAvailability"] = PropertyFunctionEffect.Pure,
                ["ConvertFromBase64"] = PropertyFunctionEffect.Pure,
                ["ConvertToBase64"] = PropertyFunctionEffect.Pure,
                ["Escape"] = PropertyFunctionEffect.Pure,
                ["Unescape"] = PropertyFunctionEffect.Pure,
                ["EnsureTrailingSlash"] = PropertyFunctionEffect.Pure,
                ["NormalizeDirectory"] = PropertyFunctionEffect.Pure,
                ["NormalizePath"] = PropertyFunctionEffect.Pure,
                ["MakeRelative"] = PropertyFunctionEffect.Pure,
                ["ValueOrDefault"] = PropertyFunctionEffect.Pure,
                ["StableStringHash"] = PropertyFunctionEffect.Pure,
                ["SubstringByAsciiChars"] = PropertyFunctionEffect.Pure,
                ["FilterTargetFrameworks"] = PropertyFunctionEffect.Pure,
                ["GetTargetFrameworkIdentifier"] = PropertyFunctionEffect.Pure,
                ["GetTargetFrameworkVersion"] = PropertyFunctionEffect.Pure,
                ["GetTargetPlatformIdentifier"] = PropertyFunctionEffect.Pure,
                ["GetTargetPlatformVersion"] = PropertyFunctionEffect.Pure,
                ["IsTargetFrameworkCompatible"] = PropertyFunctionEffect.Pure,
                ["VersionEquals"] = PropertyFunctionEffect.Pure,
                ["VersionNotEquals"] = PropertyFunctionEffect.Pure,
                ["VersionGreaterThan"] = PropertyFunctionEffect.Pure,
                ["VersionGreaterThanOrEquals"] = PropertyFunctionEffect.Pure,
                ["VersionLessThan"] = PropertyFunctionEffect.Pure,
                ["VersionLessThanOrEquals"] = PropertyFunctionEffect.Pure,
                ["GetPathOfFileAbove"] = PropertyFunctionEffect.Pure,
                ["GetDirectoryNameOfFileAbove"] = PropertyFunctionEffect.Pure,
                ["GetCurrentToolsDirectory"] = PropertyFunctionEffect.Pure,
                ["GetToolsDirectory32"] = PropertyFunctionEffect.Pure,
                ["GetToolsDirectory64"] = PropertyFunctionEffect.Pure,
                ["GetMSBuildExtensionsPath"] = PropertyFunctionEffect.Pure,
                ["GetMSBuildSDKsPath"] = PropertyFunctionEffect.Pure,
                ["GetProgramFiles32"] = PropertyFunctionEffect.Pure,
                ["GetVsInstallRoot"] = PropertyFunctionEffect.Pure,
                ["IsRunningFromVisualStudio"] = PropertyFunctionEffect.Pure,
                ["DoesTaskHostExist"] = PropertyFunctionEffect.Pure,
                ["IsOSPlatform"] = PropertyFunctionEffect.Pure,
                ["IsOsUnixLike"] = PropertyFunctionEffect.Pure,
                ["IsOsBsdLike"] = PropertyFunctionEffect.Pure,
            }),
        };

        foreach (Type pureType in (ReadOnlySpan<Type>)
            [
                typeof(string), typeof(char), typeof(bool), typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
                typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal),
                typeof(Math), typeof(Enum), typeof(Version), typeof(TimeSpan), typeof(Regex), typeof(Uri),
                typeof(UriBuilder), typeof(StringComparer), typeof(CultureInfo), typeof(RuntimeInformation), typeof(OSPlatform),
            ])
        {
            byType[pureType] = new TypeEffects(PropertyFunctionEffect.Pure, []);
        }

        return byType.ToFrozenDictionary();
    }

    private sealed class TypeEffects
    {
        internal TypeEffects(PropertyFunctionEffect defaultEffect, Dictionary<string, PropertyFunctionEffect> members)
        {
            Default = defaultEffect;
            Members = members.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        }

        internal PropertyFunctionEffect Default { get; }

        internal FrozenDictionary<string, PropertyFunctionEffect> Members { get; }
    }
}
