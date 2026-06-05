// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.TaskHost.BackEnd;

/// <summary>
/// Provide a class which can verify the correct type for both input and output parameters.
/// </summary>
internal static class TaskParameterTypeVerifier
{
    /// <summary>
    /// Checks if a type is the provided generic TaskItem type where T is a path-like type.
    /// </summary>
    internal static bool IsPathLikeTaskItemOfT(Type parameterType, string genericTaskItemTypeDefinitionFullName)
        => TaskItemTypeHelper.IsPathLikeTaskItemOfT(parameterType, genericTaskItemTypeDefinitionFullName);

    /// <summary>
    /// Checks if a type is ITaskItem&lt;T&gt; where T is path-like.
    /// </summary>
    internal static bool IsPathLikeITaskItemOfT(Type parameterType)
        => TaskItemTypeHelper.IsPathLikeITaskItemOfT(parameterType);

    /// <summary>
    /// Is the parameter type a valid scalar input value.
    /// </summary>
    internal static bool IsValidScalarInputParameter(Type parameterType)
        => parameterType.IsValueType ||
            parameterType == typeof(string) ||
            parameterType == typeof(ITaskItem) ||
            parameterType == typeof(AbsolutePath) ||
            parameterType == typeof(FileInfo) ||
            parameterType == typeof(DirectoryInfo) ||
            IsPathLikeITaskItemOfT(parameterType);

    /// <summary>
    /// Is the passed in parameterType a valid vector input parameter.
    /// </summary>
    internal static bool IsValidVectorInputParameter(Type parameterType)
    {
        if (!parameterType.IsArray)
        {
            return false;
        }

        Type elementType = parameterType.GetElementType();

        return elementType.IsValueType ||
               parameterType == typeof(string[]) ||
               parameterType == typeof(ITaskItem[]) ||
               parameterType == typeof(AbsolutePath[]) ||
               parameterType == typeof(FileInfo[]) ||
               parameterType == typeof(DirectoryInfo[]) ||
               IsPathLikeITaskItemOfT(elementType);
    }

    /// <summary>
    /// Is the passed in value type assignable to an ITaskItem or ITaskItem[] object.
    /// </summary>
    internal static bool IsAssignableToITask(Type parameterType)
    {
        if (parameterType.IsArray && typeof(ITaskItem).IsAssignableFrom(parameterType.GetElementType()))
        {
            return true;
        }

        bool result = typeof(ITaskItem[]).IsAssignableFrom(parameterType) || typeof(ITaskItem).IsAssignableFrom(parameterType);

        if (!result)
        {
            result = parameterType.IsArray
                ? IsPathLikeITaskItemOfT(parameterType.GetElementType())
                : IsPathLikeITaskItemOfT(parameterType);
        }

        return result;
    }

    /// <summary>
    /// Is the passed parameter a valid value type output parameter.
    /// </summary>
    internal static bool IsValueTypeOutputParameter(Type parameterType)
        => (parameterType.IsArray && parameterType.GetElementType().IsValueType) ||
            parameterType == typeof(string[]) ||
            parameterType == typeof(AbsolutePath[]) ||
            parameterType == typeof(FileInfo[]) ||
            parameterType == typeof(DirectoryInfo[]) ||
            parameterType.IsValueType ||
            parameterType == typeof(string) ||
            parameterType == typeof(AbsolutePath) ||
            parameterType == typeof(FileInfo) ||
            parameterType == typeof(DirectoryInfo);

    /// <summary>
    /// Is the parameter type a valid scalar or value type input parameter.
    /// </summary>
    internal static bool IsValidInputParameter(Type parameterType)
        => IsValidScalarInputParameter(parameterType) || IsValidVectorInputParameter(parameterType);

    /// <summary>
    /// Is the parameter type a valid scalar or value type output parameter.
    /// </summary>
    internal static bool IsValidOutputParameter(Type parameterType)
        => IsValueTypeOutputParameter(parameterType) || IsAssignableToITask(parameterType);
}
