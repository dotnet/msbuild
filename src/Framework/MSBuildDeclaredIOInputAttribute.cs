// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Framework;

/// <summary>
/// Identifies a task parameter whose values name logical input files.
/// </summary>
/// <remarks>
/// Apply this attribute to the task class, not to the parameter property. The named
/// public instance property may be declared on a base class. Prefer <c>nameof</c>
/// when specifying its name.
/// A string or <see cref="ITaskItem"/> represents one path; an array represents one
/// path per element. For items, the path is <see cref="ITaskItem.ItemSpec"/>, not
/// metadata. Relative paths use <see cref="TaskEnvironment.ProjectDirectory"/>
/// at invocation entry, normally the directory containing the project. The task
/// must interpret paths consistently with that execution environment or use
/// absolute paths; the process-wide current directory need not match in
/// multithreaded execution.
/// Input access includes inspecting file contents, attributes, or existence.
/// This declaration is unconditional. The parameter must not also have an
/// <see cref="MSBuildDeclaredIOOutputAttribute"/>, and its file paths must not
/// overlap declared output paths.
/// This attribute neither asserts determinism nor makes a parameter required.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class MSBuildDeclaredIOInputAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MSBuildDeclaredIOInputAttribute"/> class.
    /// </summary>
    /// <param name="parameterName">The name of the task parameter containing input file paths.</param>
    /// <exception cref="ArgumentNullException"><paramref name="parameterName"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="parameterName"/> is empty or whitespace.</exception>
    public MSBuildDeclaredIOInputAttribute(string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterName);
        ParameterName = parameterName;
    }

    /// <summary>
    /// Gets the name of the task parameter containing input file paths.
    /// </summary>
    public string ParameterName { get; }
}
