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
        /// The single source of truth for the closed set of value types <c>T</c> that may be wrapped by
        /// <c>ITaskItem&lt;T&gt;</c> / <c>TaskItem&lt;T&gt;</c> task parameters. Parameter validation, type
        /// detection, and strongly-typed item construction all defer to this so the set is defined in exactly
        /// one place.
        /// </summary>
        internal static bool IsSupportedValueType(Type valueType)
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

            return IsSupportedValueType(genericArguments[0]);
        }

        internal static bool IsPathLikeITaskItemOfT(Type parameterType)
            => IsPathLikeTaskItemOfT(parameterType, GenericITaskItemFullName);

        internal static bool IsPathLikeUtilitiesTaskItemOfT(Type parameterType)
            => IsPathLikeTaskItemOfT(parameterType, UtilitiesTaskItemFullName);
    }
}
