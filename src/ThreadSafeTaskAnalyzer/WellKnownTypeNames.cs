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
        internal const string AnalyzedAttributeFullName = "Microsoft.Build.Framework.MSBuildMultiThreadableTaskAnalyzedAttribute";
        internal const string MultiThreadableTaskAttributeFullName = "Microsoft.Build.Framework.MSBuildMultiThreadableTaskAttribute";
        internal const string ConsoleFullName = "System.Console";
    }
}
