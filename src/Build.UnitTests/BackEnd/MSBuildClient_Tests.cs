// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Threading;
using Microsoft.Build.Experimental;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    /// <summary>
    /// Tests for the <see cref="MSBuildClient"/> fallback behaviour.
    /// </summary>
    /// <remarks>
    /// Regression coverage for the .NET 10.0.300 / Aspire timeout: when
    /// <c>DOTNET_CLI_USE_MSBUILD_SERVER=true</c> is honoured but the server child cannot start
    /// (e.g. the apphost can't find the .NET runtime), <see cref="MSBuildClient.Execute"/> must
    /// not propagate a <see cref="System.TimeoutException"/> &#8212; it must return an exit type that
    /// causes the host (<c>MSBuildClientApp</c>) to fall back to in-proc execution.
    /// </remarks>
    public sealed class MSBuildClient_Tests
    {
        /// <summary>
        /// When the configured msbuild executable does not exist, launching the server fails.
        /// The client must report a recoverable exit type (LaunchError / UnableToConnect /
        /// UnknownServerState / ServerBusy) rather than letting an exception escape.
        /// </summary>
        [Fact]
        public void Execute_WithUnreachableServer_DoesNotPropagateException()
        {
            string[] commandLine = ["dummy.proj"];
            string nonexistentMsBuild = Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid().ToString("N"), "MSBuild.dll");

            MSBuildClient client = new MSBuildClient(commandLine, nonexistentMsBuild);

            // The whole point of the regression fix: this must NOT throw. Any of the recoverable
            // exit types is acceptable here &#8212; what matters is that MSBuildClientApp gets a chance
            // to fall back to in-proc execution.
            MSBuildClientExitResult result = client.Execute(CancellationToken.None);

            result.MSBuildClientExitType.ShouldBeOneOf(
                MSBuildClientExitType.LaunchError,
                MSBuildClientExitType.UnableToConnect,
                MSBuildClientExitType.UnknownServerState,
                MSBuildClientExitType.ServerBusy);
        }
    }
}
