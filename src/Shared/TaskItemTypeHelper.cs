// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Shared
{
    internal static class TaskItemTypeHelper
    {
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
            return typeArg == typeof(AbsolutePath) || typeArg == typeof(FileInfo) || typeArg == typeof(DirectoryInfo);
        }

        internal static bool IsPathLikeITaskItemOfT(Type parameterType)
            => IsPathLikeTaskItemOfT(parameterType, typeof(ITaskItem<>).FullName ?? string.Empty);

        internal static object CreateTaskItemOfT(Type taskItemType, ITaskItem item, Type taskItemImplementationGenericType)
        {
            Type[] genericArguments = taskItemType.GetGenericArguments();
            Type valueType = genericArguments[0];

            Type genericTypeDefinition = taskItemType.GetGenericTypeDefinition();
            Type constructedType = genericTypeDefinition == typeof(ITaskItem<>)
                ? taskItemImplementationGenericType.MakeGenericType(valueType)
                : genericTypeDefinition.MakeGenericType(valueType);
            ConstructorInfo? constructor = constructedType.GetConstructor(new[] { typeof(ITaskItem) });
            if (constructor == null)
            {
                throw new InvalidOperationException($"Type '{constructedType.FullName}' does not have a constructor that takes ITaskItem.");
            }

            try
            {
                return constructor.Invoke(new object[] { item });
            }
            catch (TargetInvocationException e) when (e.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                throw;
            }
        }
    }
}
