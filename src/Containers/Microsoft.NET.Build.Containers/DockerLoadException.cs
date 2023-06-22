// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


namespace Microsoft.NET.Build.Containers;

internal sealed class DockerLoadException : Exception
{
    public DockerLoadException()
    {
    }

    public DockerLoadException(string? message) : base(message)
    {
    }

    public DockerLoadException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
