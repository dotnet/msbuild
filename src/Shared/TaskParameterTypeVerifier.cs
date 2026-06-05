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
        private static bool IsSupportedTaskItemTypeArgument(Type typeArg) =>
            typeArg == typeof(AbsolutePath) ||
            typeArg == typeof(FileInfo) ||
            typeArg == typeof(DirectoryInfo);

        private static bool IsTaskItemGenericType(Type type)
        {
            if (type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof(ITaskItem<>))
            {
                return true;
            }

            if (type.GetTypeInfo().IsGenericType &&
                string.Equals(type.GetGenericTypeDefinition().FullName, "Microsoft.Build.Utilities.TaskItem`1", StringComparison.Ordinal))
            {
                return true;
            }

            foreach (Type implementedInterface in type.GetTypeInfo().ImplementedInterfaces)
            {
                if (implementedInterface.GetTypeInfo().IsGenericType &&
                    implementedInterface.GetGenericTypeDefinition() == typeof(ITaskItem<>))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if a type is ITaskItem&lt;T&gt; or TaskItem&lt;T&gt; where T is supported by MSBuild task binding.
        /// </summary>
        private static bool IsTaskItemOfT(Type parameterType)
        {
            if (!IsTaskItemGenericType(parameterType))
            {
                return false;
            }

            Type taskItemType = parameterType;
            if (!(taskItemType.GetTypeInfo().IsGenericType && taskItemType.GetGenericTypeDefinition() == typeof(ITaskItem<>)))
            {
                if (taskItemType.GetTypeInfo().IsGenericType &&
                    string.Equals(taskItemType.GetGenericTypeDefinition().FullName, "Microsoft.Build.Utilities.TaskItem`1", StringComparison.Ordinal))
                {
                    return IsSupportedTaskItemTypeArgument(taskItemType.GetGenericArguments()[0]);
                }

                taskItemType = null;

                foreach (Type implementedInterface in parameterType.GetTypeInfo().ImplementedInterfaces)
                {
                    if (implementedInterface.GetTypeInfo().IsGenericType &&
                        implementedInterface.GetGenericTypeDefinition() == typeof(ITaskItem<>))
                    {
                        taskItemType = implementedInterface;
                        break;
                    }
                }

                if (taskItemType is null)
                {
                    return false;
                }
            }

            Type[] genericArguments = taskItemType.GetGenericArguments();
            return genericArguments.Length == 1 && IsSupportedTaskItemTypeArgument(genericArguments[0]);
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
            if (parameterType.IsArray)
            {
                Type elementType = parameterType.GetElementType();

                if (IsTaskItemGenericType(elementType))
                {
                    return IsTaskItemOfT(elementType);
                }

                return typeof(ITaskItem).IsAssignableFrom(elementType);
            }

            if (IsTaskItemGenericType(parameterType))
            {
                return IsTaskItemOfT(parameterType);
            }

            return typeof(ITaskItem).IsAssignableFrom(parameterType);
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
            if (parameterType.IsArray)
            {
                Type elementType = parameterType.GetElementType();
                if (IsTaskItemGenericType(elementType) && !IsTaskItemOfT(elementType))
                {
                    return false;
                }
            }
            else if (IsTaskItemGenericType(parameterType) && !IsTaskItemOfT(parameterType))
            {
                return false;
            }

            return IsValueTypeOutputParameter(parameterType) || IsAssignableToITaskItem(parameterType);
        }
    }
}
