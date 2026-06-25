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
        private const string ConcreteTaskItemFullName = "Microsoft.Build.Framework.TaskItem`1";

        internal static bool IsAbsolutePathType(Type type)
            => string.Equals(type.FullName, AbsolutePathFullName, StringComparison.Ordinal);

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

        internal static bool IsPathLikeConcreteTaskItemOfT(Type parameterType)
            => IsPathLikeTaskItemOfT(parameterType, ConcreteTaskItemFullName);
    }
}
