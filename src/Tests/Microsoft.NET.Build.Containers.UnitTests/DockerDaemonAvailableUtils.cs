// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Xunit;

namespace Microsoft.NET.Build.Containers.UnitTests;

public class DockerDaemonAvailableTheoryAttribute : TheoryAttribute
{
    private static readonly ILogger s_logger = LoggerFactory
        .Create(
        builder =>
            builder
                .AddSimpleConsole(c => c.ColorBehavior = LoggerColorBehavior.Disabled)
                .SetMinimumLevel(LogLevel.Trace))
        .CreateLogger(nameof(DockerDaemonAvailableTheoryAttribute));

    private static readonly bool s_isDaemonAvailable = new LocalDocker(s_logger).IsAvailable();

    public DockerDaemonAvailableTheoryAttribute()
    {
        if (!s_isDaemonAvailable)
        {
            base.Skip = "Skipping test because Docker is not available on this host.";
        }
    }
}

public class DockerDaemonAvailableFactAttribute : FactAttribute
{
    // tiny optimization - since there are many instances of this attribute we should only get
    // the daemon status once
    private static readonly ILogger s_logger = LoggerFactory
        .Create(
        builder =>
            builder
                .AddSimpleConsole(c => c.ColorBehavior = LoggerColorBehavior.Disabled)
                .SetMinimumLevel(LogLevel.Trace))
        .CreateLogger(nameof(DockerDaemonAvailableTheoryAttribute));

    private static readonly bool s_isDaemonAvailable = new LocalDocker(s_logger).IsAvailable();

    public DockerDaemonAvailableFactAttribute()
    {
        if (!s_isDaemonAvailable)
        {
            base.Skip = "Skipping test because Docker is not available on this host.";
        }
    }
}
