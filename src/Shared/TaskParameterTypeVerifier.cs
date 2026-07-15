// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
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
        /// The non-value scalar types (and array element types) that MSBuild supports as task parameters
        /// by converting them to/from their string representation.
        /// </summary>
        private static readonly HashSet<Type> s_supportedTypes =
        [
            typeof(string),
            typeof(AbsolutePath),
            typeof(FileInfo),
            typeof(DirectoryInfo),
        ];

        /// <summary>
        /// Checks if a type is the provided generic TaskItem type where T is a path-like type
        /// (AbsolutePath, FileInfo, or DirectoryInfo).
        /// </summary>
        internal static bool IsPathLikeTaskItemOfT(Type parameterType, string genericTaskItemTypeDefinitionFullName)
            => TaskItemTypeDetector.IsPathLikeTaskItemOfT(parameterType, genericTaskItemTypeDefinitionFullName);

        /// <summary>
        /// Checks if a type is ITaskItem&lt;T&gt; where T is path-like.
        /// </summary>
        internal static bool IsPathLikeITaskItemOfT(Type parameterType)
            => TaskItemTypeDetector.IsPathLikeITaskItemOfT(parameterType);

        /// <summary>
        /// Is the parameter type a valid scalar input value
        /// </summary>
        internal static bool IsValidScalarInputParameter(Type parameterType) =>
            parameterType.IsValueType ||
            parameterType == typeof(ITaskItem) ||
            s_supportedTypes.Contains(parameterType) ||
            IsPathLikeITaskItemOfT(parameterType);

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

            return elementType.IsValueType ||
                        parameterType == typeof(ITaskItem[]) ||
                        s_supportedTypes.Contains(elementType) ||
                        IsPathLikeITaskItemOfT(elementType);
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
            => parameterType switch
            {
                { IsValueType: true } => true,                                      // value type
                { IsArray: true } => parameterType.GetElementType() is { } element
                    && (element.IsValueType || s_supportedTypes.Contains(element)), // array of value type or supported type
                _ => s_supportedTypes.Contains(parameterType),                      // supported scalar type
            };

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
