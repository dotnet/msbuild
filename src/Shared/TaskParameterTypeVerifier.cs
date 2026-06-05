// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Provide a class which can verify the correct type for both input and output parameters.
    /// </summary>
    internal static class TaskParameterTypeVerifier
    {
        /// <summary>
        /// Checks if a type is the provided generic TaskItem type where T is a path-like type
        /// (AbsolutePath, FileInfo, or DirectoryInfo).
        /// </summary>
        internal static bool IsPathLikeTaskItemOfT(Type parameterType, string genericTaskItemTypeDefinitionFullName)
            => TaskItemTypeHelper.IsPathLikeTaskItemOfT(parameterType, genericTaskItemTypeDefinitionFullName);

        /// <summary>
        /// Checks if a type is ITaskItem&lt;T&gt; where T is path-like.
        /// </summary>
        internal static bool IsPathLikeITaskItemOfT(Type parameterType)
            => TaskItemTypeHelper.IsPathLikeITaskItemOfT(parameterType);

        /// <summary>
        /// Creates an instance of TaskItem&lt;T&gt; (or a provided TaskItem implementation for ITaskItem&lt;T&gt;)
        /// from an <see cref="ITaskItem"/>.
        /// </summary>
        internal static object CreateTaskItemOfT(Type taskItemType, ITaskItem item, Type taskItemImplementationGenericType)
            => TaskItemTypeHelper.CreateTaskItemOfT(taskItemType, item, taskItemImplementationGenericType);

        /// <summary>
        /// Is the parameter type a valid scalar input value
        /// </summary>
        internal static bool IsValidScalarInputParameter(Type parameterType) =>
            parameterType.IsValueType ||
            parameterType == typeof(string) ||
            parameterType == typeof(ITaskItem)
            || parameterType == typeof(AbsolutePath)
            || parameterType == typeof(FileInfo)
            || parameterType == typeof(DirectoryInfo)
            || IsPathLikeITaskItemOfT(parameterType)
            ;

        /// <summary>
        /// Is the passed in parameterType a valid vector input parameter
        /// </summary>
        internal static bool IsValidVectorInputParameter(Type parameterType)
        {
            if (!parameterType.IsArray)
            {
                return false;
            }

            Type elementType = parameterType.GetElementType();

            bool result = elementType.IsValueType ||
                        parameterType == typeof(string[]) ||
                        parameterType == typeof(ITaskItem[])
                        || parameterType == typeof(AbsolutePath[])
                        || parameterType == typeof(FileInfo[])
                        || parameterType == typeof(DirectoryInfo[])
                        || IsPathLikeITaskItemOfT(elementType)
                        ;
            return result;
        }

        /// <summary>
        /// Is the passed in value type assignable to an ITaskItem or ITaskItem[] object
        /// </summary>
        internal static bool IsAssignableToITaskItem(Type parameterType)
        {
            if (parameterType.IsArray && typeof(ITaskItem).IsAssignableFrom(parameterType.GetElementType()))
            {
                return true;
            }

            // Check if it's directly assignable
            bool result = typeof(ITaskItem[]).IsAssignableFrom(parameterType) ||    /* ITaskItem array or derived type, or */
                          typeof(ITaskItem).IsAssignableFrom(parameterType);        /* ITaskItem or derived type */

            // Also check for TaskItem<T> or TaskItem<T>[]
            if (!result)
            {
                if (parameterType.IsArray)
                {
                    result = IsPathLikeITaskItemOfT(parameterType.GetElementType());
                }
                else
                {
                    result = IsPathLikeITaskItemOfT(parameterType);
                }
            }

            return result;
        }

        /// <summary>
        /// Is the passed parameter a valid value type output parameter
        /// </summary>
        internal static bool IsValueTypeOutputParameter(Type parameterType)
        {
            bool result = (parameterType.IsArray && parameterType.GetElementType().IsValueType) ||    /* array of value types, or */
                          parameterType == typeof(string[]) ||                                        /* string array, or */
                          parameterType == typeof(AbsolutePath[]) ||                                  /* AbsolutePath array, or */
                          parameterType == typeof(FileInfo[]) ||                                      /* FileInfo array, or */
                          parameterType == typeof(DirectoryInfo[]) ||                                 /* DirectoryInfo array, or */
                          parameterType.IsValueType ||                                                /* value type, or */
                          parameterType == typeof(string)                                             /* string, or */
                          || parameterType == typeof(AbsolutePath)                                    /* AbsolutePath, or */
                          || parameterType == typeof(FileInfo)                                        /* FileInfo, or */
                          || parameterType == typeof(DirectoryInfo)                                   /* DirectoryInfo */
                          ;
            return result;
        }

        /// <summary>
        /// Is the parameter type a valid scalar or value type input parameter
        /// </summary>
        internal static bool IsValidInputParameter(Type parameterType)
        {
            return IsValidScalarInputParameter(parameterType) || IsValidVectorInputParameter(parameterType);
        }

        /// <summary>
        /// Is the parameter type a valid scalar or value type output parameter
        /// </summary>
        internal static bool IsValidOutputParameter(Type parameterType)
        {
            return IsValueTypeOutputParameter(parameterType) || IsAssignableToITaskItem(parameterType);
        }
    }
}
