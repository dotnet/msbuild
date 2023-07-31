// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
