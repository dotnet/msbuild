// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.TaskHost.Exceptions;

/// <summary>
/// A catch-all type for remote exceptions that we don't know how to deserialize.
/// </summary>
internal sealed class GeneralBuildTransferredException : BuildExceptionBase
{
    public GeneralBuildTransferredException()
        : base()
    {
    }

    internal GeneralBuildTransferredException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
