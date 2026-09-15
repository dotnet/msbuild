// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Framework;

/// <summary>
/// Marks a concrete task class whose logical file inputs and outputs are completely
/// described by <see cref="MSBuildDeclaredIOInputAttribute"/> and
/// <see cref="MSBuildDeclaredIOOutputAttribute"/>.
/// </summary>
/// <remarks>
/// Declarations are unconditional and must conservatively cover all supported parameter
/// modes. The declared input and output files must be disjoint, including when different
/// parameters name the same file. Tasks whose modes require overlapping inputs and
/// outputs cannot provide this contract.
/// A marked task promises that its successful results are determined by its bound
/// parameters, declared file inputs, and execution context, and that restoring its
/// declared files and referenced output values can replace execution. This promise
/// does not imply thread safety. Tasks with hidden observable effects cannot provide
/// this contract.
/// Private temporary files with no externally observable effects are not task data
/// outputs. A marked task with no input or output declarations asserts that it has
/// no logical file inputs or outputs.
/// Each concrete task class must declare this attribute and its complete set of input
/// and output attributes explicitly; none of these attributes are inherited.
/// After successful execution, output getters referenced by the project must be safe
/// to read even when their output binding conditions are false. Task caching captures
/// each referenced getter once before task cleanup and reuses its raw value for bindings.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class MSBuildDeclaredIOTaskAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MSBuildDeclaredIOTaskAttribute"/> class.
    /// </summary>
    public MSBuildDeclaredIOTaskAttribute()
    {
    }
}
