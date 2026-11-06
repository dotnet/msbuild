// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;

namespace Microsoft.Build.Framework;

/// <summary>
/// Marks a task class whose externally observable filesystem inputs and outputs are completely described
/// by its <c>DeclaredInputs</c> and <c>DeclaredOutputs</c> parameters.
/// </summary>
/// <remarks>
/// Each cacheable invocation must explicitly supply both parameters. Explicit empty values represent empty lists.
/// Invocation constraints are described with <see cref="MSBuildDeclaredIORequiresUnsetAttribute"/>.
/// Temporary files whose useful lifetime is contained within one invocation and which do not remain after
/// successful execution are implementation details rather than declared outputs.
/// MSBuild detects this attribute by its namespace and name only, ignoring the defining assembly.
/// This allows task authors to define a compatible attribute alongside tasks that target older versions
/// of Microsoft.Build.Framework. Compatible definitions must also specify <c>Inherited = false</c>.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class MSBuildDeclaredIOTaskAttribute : Attribute;
