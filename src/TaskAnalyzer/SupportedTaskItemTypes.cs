// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Microsoft.Build.TaskAuthoring.Analyzer
{
    internal static class SupportedTaskItemTypes
    {
        // These are the types ValueTypeParser can parse and MSBuildTask0007 may suggest. This set is
        // intentionally broader than the ITaskItem<T> types accepted by the current engine binder below.
        private readonly struct SupportedSpecialType
        {
            internal SupportedSpecialType(SpecialType specialType, string displayName)
            {
                SpecialType = specialType;
                DisplayName = displayName;
            }

            internal SpecialType SpecialType { get; }
            internal string DisplayName { get; }
        }

        private static readonly ImmutableArray<SupportedSpecialType> s_specialTypes =
            ImmutableArray.Create(
                new SupportedSpecialType(SpecialType.System_Boolean, "bool"),
                new SupportedSpecialType(SpecialType.System_Char, "char"),
                new SupportedSpecialType(SpecialType.System_Byte, "byte"),
                new SupportedSpecialType(SpecialType.System_SByte, "sbyte"),
                new SupportedSpecialType(SpecialType.System_Int16, "short"),
                new SupportedSpecialType(SpecialType.System_UInt16, "ushort"),
                new SupportedSpecialType(SpecialType.System_Int32, "int"),
                new SupportedSpecialType(SpecialType.System_UInt32, "uint"),
                new SupportedSpecialType(SpecialType.System_Int64, "long"),
                new SupportedSpecialType(SpecialType.System_UInt64, "ulong"),
                new SupportedSpecialType(SpecialType.System_Single, "float"),
                new SupportedSpecialType(SpecialType.System_Double, "double"),
                new SupportedSpecialType(SpecialType.System_Decimal, "decimal"),
                new SupportedSpecialType(SpecialType.System_DateTime, "DateTime"),
                new SupportedSpecialType(SpecialType.System_String, "string"));

        // Keep the user-facing list next to IsSupportedTaskItemType. Both mirror the runtime Type-based
        // TaskItemTypeDetector, which the Roslyn analyzer cannot call with compile-time ITypeSymbol values.
        internal const string SupportedTaskItemTypeDisplayNames = "AbsolutePath, FileInfo, DirectoryInfo";

        internal static bool TryGetSpecialTypeDisplayName(SpecialType specialType, out string? displayName)
        {
            foreach (SupportedSpecialType supportedType in s_specialTypes)
            {
                if (supportedType.SpecialType == specialType)
                {
                    displayName = supportedType.DisplayName;
                    return true;
                }
            }

            displayName = null;
            return false;
        }

        internal static bool IsSupportedTaskItemType(
            ITypeSymbol type,
            INamedTypeSymbol? absolutePathType,
            INamedTypeSymbol? fileInfoType,
            INamedTypeSymbol? directoryInfoType)
        {
            return (absolutePathType is not null && SymbolEqualityComparer.Default.Equals(type, absolutePathType))
                || (fileInfoType is not null && SymbolEqualityComparer.Default.Equals(type, fileInfoType))
                || (directoryInfoType is not null && SymbolEqualityComparer.Default.Equals(type, directoryInfoType));
        }
    }
}
