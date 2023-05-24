// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace Microsoft.NET.Build.Containers.UnitTests;

public class DockerAvailableTheoryAttribute : TheoryAttribute
{
    private static bool IsAvailable = new DockerCli(Console.WriteLine).IsAvailable();
    public DockerAvailableTheoryAttribute()
    {
        if (!IsAvailable)
        {
            base.Skip = "Skipping test because Docker is not available on this host.";
        }
    }
}

public class DockerAvailableFactAttribute : FactAttribute
{
    // tiny optimization - since there are many instances of this attribute we should only get
    // the daemon status once
    private static bool IsAvailable = new DockerCli(Console.WriteLine).IsAvailable();
    public DockerAvailableFactAttribute()
    {
        if (!IsAvailable)
        {
            base.Skip = "Skipping test because Docker is not available on this host.";
        }
    }
}
