// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.CommandUtils;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

public sealed class DockerTestsFixture : IDisposable
{
    private ITestOutputHelper _diagnosticOutput;

    public DockerTestsFixture(IMessageSink messageSink)
    {
        _diagnosticOutput = new SharedTestOutputHelper(messageSink);
        DockerRegistryManager.StartAndPopulateDockerRegistry(_diagnosticOutput);
        ProjectInitializer.LocateMSBuild();
        Directory.CreateDirectory(TestSettings.TestArtifactsDirectory);
    }

    public void Dispose()
    {
        DockerRegistryManager.ShutdownDockerRegistry(_diagnosticOutput);
        ProjectInitializer.Cleanup();
        //clean up tests artifacts
        try
        {
            if (Directory.Exists(TestSettings.TestArtifactsDirectory))
            {
                Directory.Delete(TestSettings.TestArtifactsDirectory, true);
            }
        }
        catch { }
    }
}
