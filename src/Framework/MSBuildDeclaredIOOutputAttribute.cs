// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Framework;

/// <summary>
/// Identifies a task parameter whose values name logical output files.
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
/// Output access includes creating, modifying, or deleting a file and does not
/// guarantee that the file exists after execution.
/// This attribute is independent of <see cref="OutputAttribute"/>, which exposes
/// values through a task's Output elements. It neither asserts determinism nor
/// makes output paths available before the task supplies their values.
/// This declaration is unconditional. The parameter must not also have an
/// <see cref="MSBuildDeclaredIOInputAttribute"/>, and its file paths must not
/// overlap declared input paths.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class MSBuildDeclaredIOOutputAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MSBuildDeclaredIOOutputAttribute"/> class.
    /// </summary>
    /// <param name="parameterName">The name of the task parameter containing output file paths.</param>
    /// <exception cref="ArgumentNullException"><paramref name="parameterName"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="parameterName"/> is empty or whitespace.</exception>
    public MSBuildDeclaredIOOutputAttribute(string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterName);
        ParameterName = parameterName;
    }

    /// <summary>
    /// Gets the name of the task parameter containing output file paths.
    /// </summary>
    public string ParameterName { get; }
}
