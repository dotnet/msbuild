// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.Build.Shared
{
    internal static class TaskItemTypeDetector
    {
        private const string AbsolutePathFullName = "Microsoft.Build.Framework.AbsolutePath";
        private const string GenericITaskItemFullName = "Microsoft.Build.Framework.ITaskItem`1";
        private const string UtilitiesTaskItemFullName = "Microsoft.Build.Utilities.TaskItem`1";

        internal static bool IsAbsolutePathType(Type type)
            => string.Equals(type.FullName, AbsolutePathFullName, StringComparison.Ordinal);

        /// <summary>
        /// The canonical membership test for the closed set of types <c>T</c> that may be wrapped by
        /// <c>ITaskItem&lt;T&gt;</c> / <c>TaskItem&lt;T&gt;</c> task parameters (currently the path-like types
        /// <c>AbsolutePath</c>, <c>FileInfo</c>, and <c>DirectoryInfo</c>). Parameter validation and type detection
        /// call this directly. The engine's construction switch (TaskExecutionHost.CreateStronglyTypedTaskItem)
        /// cannot defer to it — instantiating the generic requires the static type — so that switch must be kept in
        /// sync with this predicate.
        /// </summary>
        internal static bool IsSupportedPathLikeType(Type valueType)
            => IsAbsolutePathType(valueType)
            || valueType == typeof(FileInfo)
            || valueType == typeof(DirectoryInfo);

        internal static bool IsPathLikeTaskItemOfT(Type parameterType, string genericTaskItemTypeDefinitionFullName)
        {
            if (!parameterType.IsGenericType)
            {
                return false;
            }

            Type genericTypeDefinition = parameterType.GetGenericTypeDefinition();
            if (!string.Equals(genericTypeDefinition.FullName, genericTaskItemTypeDefinitionFullName, StringComparison.Ordinal))
            {
                return false;
            }

            Type[] genericArguments = parameterType.GetGenericArguments();
            if (genericArguments.Length != 1)
            {
                return false;
            }

            return IsSupportedPathLikeType(genericArguments[0]);
        }

        internal static bool IsPathLikeITaskItemOfT(Type parameterType)
            => IsPathLikeTaskItemOfT(parameterType, GenericITaskItemFullName);

        internal static bool IsPathLikeUtilitiesTaskItemOfT(Type parameterType)
            => IsPathLikeTaskItemOfT(parameterType, UtilitiesTaskItemFullName);
    }
}
