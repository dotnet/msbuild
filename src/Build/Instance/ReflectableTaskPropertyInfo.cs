// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

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
        private readonly Func<string, BindingFlags, PropertyInfo> getProperty;
        private readonly string taskName;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReflectableTaskPropertyInfo"/> class.
        /// </summary>
        /// <param name="taskPropertyInfo">The original property info that generated this instance.</param>
        /// <param name="taskType">The type to reflect over to get the reflection propertyinfo later.</param>
        internal ReflectableTaskPropertyInfo(TaskPropertyInfo taskPropertyInfo, Type taskType)
            : base(taskPropertyInfo.Name, taskPropertyInfo.PropertyType, taskPropertyInfo.Output, taskPropertyInfo.Required)
        {
            ErrorUtilities.VerifyThrowArgumentNull(taskType, nameof(taskType));
            getProperty = taskType.GetProperty;
            taskName = taskType.FullName;
        }

        internal ReflectableTaskPropertyInfo(TaskPropertyInfo taskPropertyInfo, TypeInformation typeInformation)
            : base(taskPropertyInfo.Name, taskPropertyInfo.PropertyType, taskPropertyInfo.Output, taskPropertyInfo.Required)
        {
            ErrorUtilities.VerifyThrowArgumentNull(typeInformation, nameof(typeInformation));
            getProperty = typeInformation.GetProperty;
            taskName = typeInformation.TypeName;
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

        internal ReflectableTaskPropertyInfo(TypeInformation.PropertyInfo propertyInfo) :
            base(
                propertyInfo.Name,
                propertyInfo.PropertyType,
                propertyInfo.OutputAttribute,
                propertyInfo.RequiredAttribute)
        {
        }

        /// <summary>
        /// Gets or sets the reflection-produced PropertyInfo.
        /// </summary>
        internal PropertyInfo Reflection
        {
            get
            {
                if (_propertyInfo == null)
                {
                    _propertyInfo = getProperty(Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                    ErrorUtilities.VerifyThrow(_propertyInfo != null, "Could not find property {0} on type {1} that the task factory indicated should exist.", Name, taskName);
                }

                return _propertyInfo;
            }
        }
    }
}
