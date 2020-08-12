// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

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
        private Type _taskType;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReflectableTaskPropertyInfo"/> class.
        /// </summary>
        /// <param name="taskPropertyInfo">The original property info that generated this instance.</param>
        /// <param name="taskType">The type to reflect over to get the reflection propertyinfo later.</param>
        internal ReflectableTaskPropertyInfo(TaskPropertyInfo taskPropertyInfo, Type taskType)
            : base(taskPropertyInfo.Name, taskPropertyInfo.PropertyType, taskPropertyInfo.Output, taskPropertyInfo.Required)
        {
            ErrorUtilities.VerifyThrowArgumentNull(taskType, nameof(taskType));
            _taskType = taskType;
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
        /// Gets or sets the reflection-produced PropertyInfo.
        /// </summary>
        internal PropertyInfo Reflection
        {
            get
            {
                if (_propertyInfo == null)
                {
                    _propertyInfo = _taskType.GetProperty(Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                    ErrorUtilities.VerifyThrow(_propertyInfo != null, "Could not find property {0} on type {1} that the task factory indicated should exist.", Name, _taskType.FullName);
                }

                return _propertyInfo;
            }
        }
    }
}
