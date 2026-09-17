// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.TaskAuthoring.Analyzer
{
    /// <summary>
    /// Well-known fully-qualified type names used by both analyzers.
    /// </summary>
    internal static class WellKnownTypeNames
    {
        internal const string ITaskFullName = "Microsoft.Build.Framework.ITask";
        internal const string IMultiThreadableTaskFullName = "Microsoft.Build.Framework.IMultiThreadableTask";
        internal const string TaskEnvironmentFullName = "Microsoft.Build.Framework.TaskEnvironment";
        internal const string AbsolutePathFullName = "Microsoft.Build.Framework.AbsolutePath";
        internal const string ITaskItemFullName = "Microsoft.Build.Framework.ITaskItem";
        internal const string ITaskItem2FullName = "Microsoft.Build.Framework.ITaskItem2";
        internal const string ITaskItemOfTFullName = "Microsoft.Build.Framework.ITaskItem`1";
        internal const string OutputAttributeFullName = "Microsoft.Build.Framework.OutputAttribute";
        internal const string RequiredAttributeFullName = "Microsoft.Build.Framework.RequiredAttribute";
        internal const string AnalyzedAttributeFullName = "Microsoft.Build.Framework.MSBuildMultiThreadableTaskAnalyzedAttribute";
        internal const string MultiThreadableTaskAttributeFullName = "Microsoft.Build.Framework.MSBuildMultiThreadableTaskAttribute";
        internal const string ConsoleFullName = "System.Console";
        internal const string FileSystemInfoFullName = "System.IO.FileSystemInfo";
        internal const string FileInfoFullName = "System.IO.FileInfo";
        internal const string DirectoryInfoFullName = "System.IO.DirectoryInfo";
    }
}
