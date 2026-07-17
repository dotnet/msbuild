// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Provide a class which can verify the correct type for both input and output parameters.
    /// </summary>
    internal static class TaskParameterTypeVerifier
    {
        internal static bool TryGetSupportedTaskItemValueType(Type parameterType, [NotNullWhen(true)] out Type? valueType)
        {
            if (TaskItemTypeDetector.TryGetTaskItemValueType(parameterType, out valueType))
            {
                return ValueTypeParser.IsSupportedType(valueType);
            }

            return false;
        }

        private static bool IsValidValueParameterType(Type parameterType)
            => parameterType.IsValueType || ValueTypeParser.IsSupportedType(parameterType);

        private static bool IsValidInputElementType(Type parameterType)
        {
            if (TaskItemTypeDetector.TryGetTaskItemValueType(parameterType, out Type? valueType))
            {
                return ValueTypeParser.IsSupportedType(valueType);
            }

            if (typeof(ITaskItem).IsAssignableFrom(parameterType))
            {
                return parameterType == typeof(ITaskItem);
            }

            return IsValidValueParameterType(parameterType);
        }

        /// <summary>
        /// Is the parameter type a valid scalar input value - meaning not an array and a valid input element type
        /// </summary>
        internal static bool IsValidScalarInputParameter(Type parameterType) =>
            !TryGetArrayElement(parameterType, out _) && IsValidInputElementType(parameterType);

        /// <summary>
        /// Is the passed in parameterType a valid vector input parameter - meaning is an array and the element type is a valid input element type
        /// </summary>
        internal static bool IsValidVectorInputParameter(Type parameterType) =>
            TryGetArrayElement(parameterType, out Type? elementType) && IsValidInputElementType(elementType);

        /// <summary>
        /// Helper that returns the element type of an array parameter type, or null if the parameter type is not an array - useful to handle nullability flow analysis for array element types in the calling code.
        /// </summary>
        private static bool TryGetArrayElement(Type parameterType, [NotNullWhen(true)] out Type? elementType)
        {
            if (parameterType.IsArray)
            {
                elementType = parameterType.GetElementType()!; // GetElementType is non-null because parameterType is an array
                return true;
            }

            elementType = null;
            return false;
        }

        /// <summary>
        /// Is the passed in value type assignable to an ITaskItem or ITaskItem[] object
        /// </summary>
        internal static bool IsAssignableToITaskItem(Type parameterType)
        {
            Type elementType = TryGetArrayElement(parameterType, out Type? arrayElementType) ? arrayElementType : parameterType;
            if (TaskItemTypeDetector.TryGetTaskItemValueType(elementType, out Type? valueType))
            {
                return ValueTypeParser.IsSupportedType(valueType);
            }

            return typeof(ITaskItem).IsAssignableFrom(elementType);
        }

        /// <summary>
        /// Is the passed parameter a valid value type output parameter
        /// </summary>
        internal static bool IsValueTypeOutputParameter(Type parameterType)
        {
            Type elementType = TryGetArrayElement(parameterType, out Type? arrayElementType) ? arrayElementType : parameterType;
            return !typeof(ITaskItem).IsAssignableFrom(elementType)
                && IsValidValueParameterType(elementType);
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
