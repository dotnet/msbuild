// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.IntegrationTests;

public sealed class DockerTestsFixture : IDisposable
{
    private readonly SharedTestOutputHelper _diagnosticOutput;

    public DockerTestsFixture(IMessageSink messageSink)
    {
        _diagnosticOutput = new SharedTestOutputHelper(messageSink);
        try
        {
            DockerRegistryManager.StartAndPopulateDockerRegistry(_diagnosticOutput);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        try
        {
            DockerRegistryManager.ShutdownDockerRegistry(_diagnosticOutput);
        }
        catch
        {
            _diagnosticOutput.WriteLine("Failed to shutdown docker registry, shut down it manually");
        }
    }
}
