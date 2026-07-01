// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Shared
{
    internal static class TaskItemTypeDetector
    {
        // The generic type definitions (ITaskItem<T>/TaskItem<T>) are matched by full type name because
        // callers pass the FullName of whichever definition they care about (see IsPathLikeTaskItemOfT).
        private const string GenericITaskItemFullName = "Microsoft.Build.Framework.ITaskItem`1";

        internal static bool IsAbsolutePathType(Type type) => type == typeof(AbsolutePath);

        /// <summary>
        /// Returns true if <paramref name="type"/> is one of the strongly-typed path types supported as a
        /// task parameter or as the <c>T</c> in <c>ITaskItem&lt;T&gt;</c>/<c>TaskItem&lt;T&gt;</c>
        /// (<see cref="AbsolutePath"/>, <see cref="FileInfo"/>, or <see cref="DirectoryInfo"/>). This is the
        /// single definition of "supported path type" shared by the engine parameter-binding code and
        /// <c>TaskItem&lt;T&gt;</c>.
        /// </summary>
        internal static bool IsSupportedPathType(Type type)
            => type == typeof(AbsolutePath) || type == typeof(FileInfo) || type == typeof(DirectoryInfo);

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

            Type typeArg = genericArguments[0];
            return IsSupportedPathType(typeArg);
        }

        internal static bool IsPathLikeITaskItemOfT(Type parameterType)
            => IsPathLikeTaskItemOfT(parameterType, GenericITaskItemFullName);
    }
}
