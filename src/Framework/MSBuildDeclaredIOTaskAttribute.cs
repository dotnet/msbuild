// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build.Framework;

/// <summary>
/// Marks a task class whose externally observable filesystem inputs and outputs are completely described
/// by publicly readable <c>IEnumerable&lt;string&gt;</c> properties named <c>InputFiles</c> and
/// <c>OutputFiles</c> without public setters.
/// </summary>
/// <remarks>
/// MSBuild reads <c>InputFiles</c> after binding task parameters and <c>OutputFiles</c> after successful execution.
/// <c>OutputFiles</c> should include every path whose final presence or absence is part of the task result.
/// Every supported invocation must satisfy this contract: its results must depend only on the task's
/// parameters, declared inputs, and other values included in the cache key.
/// This API is experimental and may change or be removed without preserving compatibility.
/// Temporary files whose useful lifetime is contained within one invocation and which do not remain after
/// successful execution are implementation details rather than declared outputs.
/// MSBuild detects this attribute by its namespace and name only, ignoring the defining assembly.
/// This allows task authors to define a compatible attribute alongside tasks that target older versions
/// of Microsoft.Build.Framework. Compatible definitions must also specify <c>Inherited = false</c>.
/// </remarks>
[Experimental("MSBEXP0001")]
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class MSBuildDeclaredIOTaskAttribute : Attribute;
