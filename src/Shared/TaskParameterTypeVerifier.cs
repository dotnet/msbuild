// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using Microsoft.Build.Framework;
#if NET35
using Microsoft.Build.Shared;
#endif

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Provide a class which can verify the correct type for both input and output parameters.
    /// </summary>
    internal static class TaskParameterTypeVerifier
    {
#if !TASKHOST
        /// <summary>
        /// Checks if a type is ITaskItem&lt;T&gt; where T is a value type.
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
            return genericArguments.Length == 1 && genericArguments[0].GetTypeInfo().IsValueType;
        }
#endif

        /// <summary>
        /// Is the parameter type a valid scalar input value
        /// </summary>
        internal static bool IsValidScalarInputParameter(Type parameterType) =>
            parameterType.GetTypeInfo().IsValueType ||
            parameterType == typeof(string) ||
            parameterType == typeof(ITaskItem)
#if !TASKHOST
            || parameterType == typeof(AbsolutePath)
            || IsTaskItemOfT(parameterType)
#endif
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
#if !TASKHOST
                        || parameterType == typeof(AbsolutePath[])
                        || IsTaskItemOfT(elementType)
#endif
                        ;
            return result;
        }

        /// <summary>
        /// Is the passed in value type assignable to an ITaskItem or ITaskItem[] object
        /// </summary>
        internal static bool IsAssignableToITaskItem(Type parameterType)
        {
            // Check if it's directly assignable
            bool result = typeof(ITaskItem[]).GetTypeInfo().IsAssignableFrom(parameterType.GetTypeInfo()) ||    /* ITaskItem array or derived type, or */
                          typeof(ITaskItem).IsAssignableFrom(parameterType);                                    /* ITaskItem or derived type */

#if !TASKHOST
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
#endif

            return result;
        }

        /// <summary>
        /// Is the passed parameter a valid value type output parameter
        /// </summary>
        internal static bool IsValueTypeOutputParameter(Type parameterType)
        {
            bool result = (parameterType.IsArray && parameterType.GetElementType().GetTypeInfo().IsValueType) ||    /* array of value types, or */
                          parameterType == typeof(string[]) ||                                                      /* string array, or */
#if !TASKHOST
                          parameterType == typeof(AbsolutePath[]) ||                                                /* AbsolutePath array, or */
#endif
                          parameterType.GetTypeInfo().IsValueType ||                                                /* value type, or */
                          parameterType == typeof(string)                                                           /* string, or */
#if !TASKHOST
                          || parameterType == typeof(AbsolutePath)                                                  /* AbsolutePath */
#endif
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
