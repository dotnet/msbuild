// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Provide a class which can verify the correct type for both input and output parameters.
    /// </summary>
    internal static class TaskParameterTypeVerifier
    {
        /// <summary>
        /// Checks if a type is ITaskItem&lt;T&gt; where T is a path-like type (AbsolutePath, FileInfo, or DirectoryInfo).
        /// </summary>
        private static bool IsTaskItemOfT(Type parameterType)
        {
            if (!parameterType.GetTypeInfo().IsGenericType)
            {
                return false;
            }

            Type genericTypeDefinition = parameterType.GetGenericTypeDefinition();
            if (genericTypeDefinition != typeof(ITaskItem<>))
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

        /// <summary>
        /// Is the parameter type a valid scalar input value
        /// </summary>
        internal static bool IsValidScalarInputParameter(Type parameterType) =>
            parameterType.GetTypeInfo().IsValueType ||
            parameterType == typeof(string) ||
            parameterType == typeof(ITaskItem)
            || parameterType == typeof(AbsolutePath)
            || parameterType == typeof(FileInfo)
            || parameterType == typeof(DirectoryInfo)
            || IsTaskItemOfT(parameterType)
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

            bool result = elementType.GetTypeInfo().IsValueType ||
                        parameterType == typeof(string[]) ||
                        parameterType == typeof(ITaskItem[])
                        || parameterType == typeof(AbsolutePath[])
                        || parameterType == typeof(FileInfo[])
                        || parameterType == typeof(DirectoryInfo[])
                        || IsTaskItemOfT(elementType)
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
            bool result = typeof(ITaskItem[]).GetTypeInfo().IsAssignableFrom(parameterType.GetTypeInfo()) ||    /* ITaskItem array or derived type, or */
                          typeof(ITaskItem).IsAssignableFrom(parameterType);                                    /* ITaskItem or derived type */

            // Also check for TaskItem<T> or TaskItem<T>[]
            if (!result)
            {
                if (parameterType.IsArray)
                {
                    result = IsTaskItemOfT(parameterType.GetElementType());
                }
                else
                {
                    result = IsTaskItemOfT(parameterType);
                }
            }

            return result;
        }

        /// <summary>
        /// Is the passed parameter a valid value type output parameter
        /// </summary>
        internal static bool IsValueTypeOutputParameter(Type parameterType)
        {
            bool result = (parameterType.IsArray && parameterType.GetElementType().GetTypeInfo().IsValueType) ||    /* array of value types, or */
                          parameterType == typeof(string[]) ||                                                      /* string array, or */
                          parameterType == typeof(AbsolutePath[]) ||                                                /* AbsolutePath array, or */
                          parameterType == typeof(FileInfo[]) ||                                                    /* FileInfo array, or */
                          parameterType == typeof(DirectoryInfo[]) ||                                               /* DirectoryInfo array, or */
                          parameterType.GetTypeInfo().IsValueType ||                                                /* value type, or */
                          parameterType == typeof(string)                                                           /* string, or */
                          || parameterType == typeof(AbsolutePath)                                                  /* AbsolutePath, or */
                          || parameterType == typeof(FileInfo)                                                      /* FileInfo, or */
                          || parameterType == typeof(DirectoryInfo)                                                 /* DirectoryInfo */
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
