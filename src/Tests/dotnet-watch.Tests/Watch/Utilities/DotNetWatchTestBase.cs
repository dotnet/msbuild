// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watcher.Tests;

/// <summary>
/// Base class for all tests that create dotnet watch process.
/// </summary>
public abstract class DotNetWatchTestBase : IDisposable
{
    internal TestAssetsManager TestAssets { get; }
    internal WatchableApp App { get; }

    public DotNetWatchTestBase(ITestOutputHelper logger)
    {
        App = new WatchableApp(logger);
        TestAssets = new TestAssetsManager(logger);

        // disposes the test class if the test execution is cancelled:
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Dispose();
    }

    public ITestOutputHelper Logger => App.Logger;

    public void Dispose()
    {
        App.Dispose();
    }
}
