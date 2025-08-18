// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Tasks.AssemblyDependency
{
    /// <summary>
    /// Reflection-based discovery of RAR's input and output parameters.
    /// This is nearly identical to how the engine discovers task parameters, but with support for skipping unset values
    /// and filtering for any properties we want to handle ourselves for serialization performance.
    /// </summary>
    internal static class RarTaskParameters
    {
        /// <summary>
        /// A cache of reflected properties for the ResolveAssemblyReference task.
        /// </summary>
        private static ReflectedProperties? s_reflectedProperties;

        // <summary>
        // Selector for task input or output parameters.
        // </summary>
        internal enum ParameterType
        {
            /// <summary>
            /// Task input.
            /// </summary>
            Input,

            /// <summary>
            /// Task output.
            /// </summary>
            Output,
        }

        // <summary>
        // Initializes cached reflected properties.
        // </summary>
        internal static void Init() => s_reflectedProperties ??= new ReflectedProperties();

        /// <summary>
        /// Creates a mapping from each cached parameter name to its current value on the RAR task.
        /// Only properties that differ from their default values are included.
        /// </summary>
        /// <param name="parameterType">The type of parameters to retrieve.</param>
        /// <param name="rar">The RAR task instance to extract parameters from.</param>
        /// <returns>A mapping from each set parameter name to a serializable representation of its value.</returns>
        internal static Dictionary<string, TaskParameter> Get(ParameterType parameterType, ResolveAssemblyReference rar)
        {
            s_reflectedProperties ??= new ReflectedProperties();
            ReflectedPropertyInfo[] properties = parameterType == ParameterType.Input
                ? s_reflectedProperties.Inputs
                : s_reflectedProperties.Outputs;

            Dictionary<string, TaskParameter> taskParameters = new(properties.Length, StringComparer.Ordinal);

            foreach (ReflectedPropertyInfo property in properties)
            {
                object? value = property.Target.GetValue(rar);

                if (IsParameterExplicitlySet(value, property.DefaultValue))
                {
                    taskParameters[property.Target.Name] = new TaskParameter(value);
                }
            }

            return taskParameters;

            // Determines if a parameter is explicitly set by comparing its current value against our cached default value.
            static bool IsParameterExplicitlySet(object? value, object? defaultValue)
            {
                // .NET Framework doesn't have an allocation-free SequenceEqual, so manually compare array elements.
                if (value is string[] stringArray
                    && defaultValue is string[] defaultStringArray
                    && stringArray.Length == defaultStringArray.Length)
                {
                    for (int i = 0; i < stringArray.Length; i++)
                    {
                        if (!string.Equals(stringArray[i], defaultStringArray[i], StringComparison.Ordinal))
                        {
                            return true;
                        }
                    }

                    return false;
                }

                // Use Equals instead of '==' to correctly handle value equality.
                return value?.Equals(defaultValue) == false;
            }
        }

        /// <summary>
        /// Sets each property on the RAR task from a previously extracted mapping of parameters.
        /// </summary>
        /// <param name="parameterType">The type of parameters in the dictionary.</param>
        /// <param name="rar">The RAR task instance to set parameters on.</param>
        /// <param name="parameters">A previously created mapping from parameter name to its wrapped value.</param>
        internal static void Set(ParameterType parameterType, ResolveAssemblyReference rar, Dictionary<string, TaskParameter> parameters)
        {
            s_reflectedProperties ??= new ReflectedProperties();
            ReflectedPropertyInfo[] properties = parameterType == ParameterType.Input
                ? s_reflectedProperties.Inputs
                : s_reflectedProperties.Outputs;

            foreach (ReflectedPropertyInfo property in properties)
            {
                if (parameters.TryGetValue(property.Target.Name, out TaskParameter? parameter))
                {
                    property.Target.SetValue(rar, parameter.WrappedParameter);
                }
            }
        }

        private readonly struct ReflectedPropertyInfo(PropertyInfo target, object? defaultValue)
        {
            internal PropertyInfo Target { get; } = target;

            internal object? DefaultValue { get; } = defaultValue;
        }

        private class ReflectedProperties
        {
            internal ReflectedProperties()
            {
                List<ReflectedPropertyInfo> inputs = [];
                List<ReflectedPropertyInfo> outputs = [];

                PropertyInfo[] properties = typeof(ResolveAssemblyReference)
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

                // Create a throwaway instance to capture the default values for each property.
                ResolveAssemblyReference rar = new();

                foreach (PropertyInfo property in properties)
                {
                    if (property.SetMethod == null)
                    {
                        // This may happen if a new output property is added to RAR without a setter.
                        // Ignore to avoid failing at runtime.
                        // TODO: Consider adding a UT to validate at build time and remove the check.
                        continue;
                    }

                    ReflectedPropertyInfo reflectedProperty = new(property, property.GetValue(rar));

                    // Outputs - must be annotated with OutputAttribute.
                    // Inputs - everything else.
                    if (property.GetCustomAttribute<OutputAttribute>() != null)
                    {
                        // Exclude CopyLocalFiles since it is a list of references - otherwise we'll end up with duplicated task item instances.
                        // Also exclude FilesWritten since we can't externally set it due to visibility. RAR will derive it before returning our result.
                        if (!string.Equals(property.Name, nameof(ResolveAssemblyReference.CopyLocalFiles), StringComparison.Ordinal)
                            && !string.Equals(property.Name, nameof(ResolveAssemblyReference.FilesWritten), StringComparison.Ordinal))
                        {
                            outputs.Add(reflectedProperty);
                        }
                    }
                    else
                    {
                        inputs.Add(reflectedProperty);
                    }
                }

                Inputs = [.. inputs];
                Outputs = [.. outputs];
            }

            internal ReflectedPropertyInfo[] Inputs { get; }

            internal ReflectedPropertyInfo[] Outputs { get; }
        }
    }
}
