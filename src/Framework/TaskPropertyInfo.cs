// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Class which represents the parameter information from the using task as a strongly typed class.
    /// </summary>
    [Serializable]
    public class TaskPropertyInfo
    {

        /// <summary>
        /// Encapsulates a list of parameters declared in the UsingTask
        /// </summary>
        /// <param name="name">Name of the parameter</param>
        /// <param name="typeOfParameter">The actual type of the parameter</param>
        /// <param name="output">True if the parameter is both an output and input parameter. False if the parameter is only an input parameter</param>
        /// <param name="required">True if the parameter must be supplied to each invocation of the task.</param>
        public TaskPropertyInfo(string name, Type typeOfParameter, bool output, bool required) : this(name, typeOfParameter, output, required, isPathLike: true)
        {
        }
        
        /// <summary>
        /// Encapsulates a list of parameters declared in the UsingTask
        /// </summary>
        /// <param name="name">Name of the parameter</param>
        /// <param name="typeOfParameter">The actual type of the parameter</param>
        /// <param name="output">True if the parameter is both an output and input parameter. False if the parameter is only an input parameter</param>
        /// <param name="required">True if the parameter must be supplied to each invocation of the task.</param>
        /// <param name="isPathLike">Whether the parameter is path-like. Path-like parameters will be normalized during expansion.</param>
        public TaskPropertyInfo(string name, Type typeOfParameter, bool output, bool required, bool isPathLike)
        {
            Name = name;
            PropertyType = typeOfParameter;
            Output = output;
            Required = required;
            IsPathLike = isPathLike;
            Type elementType = typeOfParameter.IsArray ? typeOfParameter.GetElementType() : typeOfParameter;
            IsValueTypeOutputParameter = elementType.GetTypeInfo().IsValueType || elementType.FullName.Equals("System.String");
            IsAssignableToITask = typeof(ITaskItem).IsAssignableFrom(elementType);
        }

        /// <summary>
        /// The type of the property
        /// </summary>
        public Type PropertyType { get; private set; }

        /// <summary>
        /// Name of the property
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// This task parameter is an output parameter (analogous to [Output] attribute)
        /// </summary>
        public bool Output { get; private set; }

        /// <summary>
        /// This task parameter is required (analogous to the [Required] attribute)
        /// </summary>
        public bool Required { get; private set; }

        /// <summary>
        /// Whether the parameter is file-like (path or item). File-like parameters will be normalized during expansion.
        /// </summary>
        public bool IsPathLike { get; private set; }

        /// <summary>
        /// This task parameter should be logged when LogTaskInputs is set. Defaults to true.
        /// </summary>
        public bool Log { get; set; } = true;

        /// <summary>
        /// When this task parameter is an item list, determines whether to log item metadata. Defaults to true.
        /// </summary>
        public bool LogItemMetadata { get; set; } = true;

        /// <summary>
        /// Whether the Log and LogItemMetadata properties have been assigned already.
        /// </summary>
        internal bool Initialized = false;

        internal bool IsValueTypeOutputParameter { get; private set; }
        internal bool IsAssignableToITask { get; set; }
    }
}
