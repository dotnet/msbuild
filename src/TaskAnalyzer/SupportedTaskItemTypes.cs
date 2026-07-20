// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Microsoft.Build.TaskAuthoring.Analyzer
{
    internal static class SupportedTaskItemTypes
    {
        // These are the special types supported by ValueTypeParser and the ITaskItem<T> binder.
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

        // These types have dedicated parsing paths and do not rely on Convert.ChangeType.
        internal const string DirectlyParsedTaskItemTypeDisplayNames =
            "string, bool, AbsolutePath, FileInfo, DirectoryInfo";

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

        internal static bool IsConvertChangeTypeTaskItemType(SpecialType specialType)
            => specialType is
                SpecialType.System_Char or
                SpecialType.System_Byte or
                SpecialType.System_SByte or
                SpecialType.System_Int16 or
                SpecialType.System_UInt16 or
                SpecialType.System_Int32 or
                SpecialType.System_UInt32 or
                SpecialType.System_Int64 or
                SpecialType.System_UInt64 or
                SpecialType.System_Single or
                SpecialType.System_Double or
                SpecialType.System_Decimal or
                SpecialType.System_DateTime;

        internal static bool IsSupportedTaskItemType(
            ITypeSymbol type,
            INamedTypeSymbol? absolutePathType,
            INamedTypeSymbol? fileInfoType,
            INamedTypeSymbol? directoryInfoType)
        {
            return TryGetSpecialTypeDisplayName(type.SpecialType, out _)
                || (absolutePathType is not null && SymbolEqualityComparer.Default.Equals(type, absolutePathType))
                || (fileInfoType is not null && SymbolEqualityComparer.Default.Equals(type, fileInfoType))
                || (directoryInfoType is not null && SymbolEqualityComparer.Default.Equals(type, directoryInfoType));
        }
    }
}
