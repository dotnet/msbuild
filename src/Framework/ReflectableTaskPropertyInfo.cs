// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// A reflection-generated TaskPropertyInfo instance.
    /// </summary>
    internal class ReflectableTaskPropertyInfo : TaskPropertyInfo
    {
        /// <summary>
        /// The reflection-produced PropertyInfo.
        /// </summary>
        private PropertyInfo _propertyInfo;

        /// <summary>
        /// The type of the generated tasks.
        /// </summary>
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
        private Type _taskType;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReflectableTaskPropertyInfo"/> class.
        /// </summary>
        /// <param name="taskPropertyInfo">The original property info that generated this instance.</param>
        /// <param name="taskType">The type to reflect over to get the reflection propertyinfo later.</param>
        internal ReflectableTaskPropertyInfo(
            TaskPropertyInfo taskPropertyInfo,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
            Type taskType)
            : base(taskPropertyInfo.Name, taskPropertyInfo.PropertyType, taskPropertyInfo.Output, taskPropertyInfo.Required)
        {
            ArgumentNullException.ThrowIfNull(taskType);
            _taskType = taskType;
            IsAssignableToITask = taskPropertyInfo.IsAssignableToITask;
            IsValueTypeOutputParameter = taskPropertyInfo.IsValueTypeOutputParameter;
            if (taskPropertyInfo is ReflectableTaskPropertyInfo reflectableProperty)
            {
                IsTypeUnresolved = reflectableProperty.IsTypeUnresolved;
                ParameterTypeForExpansion = reflectableProperty.ParameterTypeForExpansion;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ReflectableTaskPropertyInfo"/> class.
        /// </summary>
        /// <param name="propertyInfo">The PropertyInfo used to discover this task property.</param>
        internal ReflectableTaskPropertyInfo(PropertyInfo propertyInfo)
            : base(
            propertyInfo.Name,
            propertyInfo.PropertyType,
            propertyInfo.GetCustomAttributes(typeof(OutputAttribute), true).Any(),
            propertyInfo.GetCustomAttributes(typeof(RequiredAttribute), true).Any())
        {
            _propertyInfo = propertyInfo;
        }

        /// <summary>
        /// Initializes a new <see cref="ReflectableTaskPropertyInfo"/> with precomputed parameters. This is specifically
        /// used with MetadataLoadContext, as these parameters cannot be computed for the property type passed in directly but
        /// rather the relevant base type.
        /// </summary>
        internal ReflectableTaskPropertyInfo(
            PropertyInfo propertyInfo,
            Type propertyType,
            bool output,
            bool required,
            bool isAssignableToITaskItemType,
            Type parameterTypeForExpansion,
            bool isTypeUnresolved)
            : base(
            propertyInfo.Name,
            propertyType,
            output,
            required)
        {
            _propertyInfo = propertyInfo;
            IsAssignableToITask = isAssignableToITaskItemType;
            IsValueTypeOutputParameter |= IsPathType(parameterTypeForExpansion);
            ParameterTypeForExpansion = parameterTypeForExpansion;
            IsTypeUnresolved = isTypeUnresolved;
        }

        internal ReflectableTaskPropertyInfo(PropertyInfo propertyInfo, bool output, bool required, Type parameterTypeForExpansion)
            : base(propertyInfo.Name, parameterTypeForExpansion ?? typeof(object), output, required)
        {
            _propertyInfo = propertyInfo;
            IsTypeUnresolved = true;
            IsValueTypeOutputParameter |= IsPathType(parameterTypeForExpansion);
            ParameterTypeForExpansion = parameterTypeForExpansion;
        }

        private static bool IsPathType(Type type)
        {
            Type elementType = type?.IsArray == true ? type.GetElementType() : type;
            return elementType == typeof(System.IO.FileInfo)
                || elementType == typeof(System.IO.DirectoryInfo);
        }

        internal bool IsTypeUnresolved { get; }

        internal Type ParameterTypeForExpansion { get; }

        /// <summary>
        /// Gets or sets the reflection-produced PropertyInfo.
        /// </summary>
        internal PropertyInfo Reflection
        {
            get
            {
                if (_propertyInfo == null)
                {
                    PropertyInfo foundProperty = null;
                    foreach (PropertyInfo propertyInfo in _taskType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                    {
                        if (string.Equals(propertyInfo.Name, Name, StringComparison.OrdinalIgnoreCase))
                        {
                            if (foundProperty != null)
                            {
                                // Multiple case-insensitive matches indicate shadowed properties or a malformed task type.
                                throw new AmbiguousMatchException($"""
                                    Multiple properties matching '{Name}' (case-insensitive) found on type '{_taskType.FullName}'.
                                    Shadowed or duplicate property definitions are not supported.
                                    """);
                            }
                            foundProperty = propertyInfo;
                        }
                    }

                    Assumed.NotNull(foundProperty, $"Could not find property {Name} on type {_taskType.FullName} that the task factory indicated should exist.");
                    _propertyInfo = foundProperty;
                }

                return _propertyInfo;
            }
        }
    }
}
