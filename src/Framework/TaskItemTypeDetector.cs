// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Shared
{
    internal static class TaskItemTypeDetector
    {
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

        /// <summary>
        /// Gets the value type from a declared <see cref="ITaskItem{T}"/> or <see cref="TaskItem{T}"/> type.
        /// </summary>
        internal static bool TryGetTaskItemValueType(Type parameterType, [NotNullWhen(true)] out Type? valueType)
        {
            if (!parameterType.IsGenericType)
            {
                valueType = null;
                return false;
            }

            Type genericTypeDefinition = parameterType.GetGenericTypeDefinition();
            if (genericTypeDefinition != typeof(ITaskItem<>)
                && genericTypeDefinition != typeof(TaskItem<>))
            {
                valueType = null;
                return false;
            }

            valueType = parameterType.GetGenericArguments()[0];
            return true;
        }
    }
}
