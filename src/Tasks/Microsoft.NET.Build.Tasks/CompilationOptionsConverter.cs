// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Extensions.DependencyModel;

namespace Microsoft.NET.Build.Tasks
{
    internal static class CompilationOptionsConverter
    {
        public static CompilationOptions ConvertFrom(ITaskItem compilerOptionsItem)
        {
            if (compilerOptionsItem == null)
            {
                return null;
            }

            return new CompilationOptions(
                compilerOptionsItem.GetMetadata("DefineConstants")?.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries),
                compilerOptionsItem.GetMetadata("LangVersion"),
                compilerOptionsItem.GetMetadata("PlatformTarget"),
                compilerOptionsItem.GetBooleanMetadata("AllowUnsafeBlocks"),
                compilerOptionsItem.GetBooleanMetadata("TreatWarningsAsErrors"),
                compilerOptionsItem.GetBooleanMetadata("Optimize"),
                compilerOptionsItem.GetMetadata("AssemblyOriginatorKeyFile"),
                compilerOptionsItem.GetBooleanMetadata("DelaySign"),
                compilerOptionsItem.GetBooleanMetadata("PublicSign"),
                compilerOptionsItem.GetMetadata("DebugType"),
                "exe".Equals(compilerOptionsItem.GetMetadata("OutputType"), StringComparison.OrdinalIgnoreCase),
                compilerOptionsItem.GetBooleanMetadata("GenerateDocumentationFile"));
        }
    }
}
