// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;

#pragma warning disable MSBUILD001

namespace Microsoft.Build.Utilities;

/// <summary>
/// Describes the inputs, execution context, and outputs of a cacheable invocation.
/// </summary>
[Experimental("MSBUILD001")]
public sealed class InvocationDescription
{
    /// <summary>
    /// Gets the ordered input files.
    /// </summary>
    public InvocationInputs Inputs { get; }

    /// <summary>
    /// Gets the ordered execution context, such as effective arguments and relevant environment settings.
    /// </summary>
    public string[] Context { get; }

    /// <summary>
    /// Gets the owned outputs.
    /// </summary>
    public InvocationOutputs Outputs { get; }

    /// <summary>
    /// Creates an invocation description.
    /// </summary>
    /// <param name="inputs">All input files affecting execution.</param>
    /// <param name="context">Ordered context values, kept stable through hashing and execution.</param>
    /// <param name="outputs">The owned directory or ordered file set.</param>
    public InvocationDescription(InvocationInputs inputs, string[] context, InvocationOutputs outputs)
    {
        Inputs = inputs;
        Context = context;
        Outputs = outputs;
    }
}
