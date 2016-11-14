// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.BindingRedirects.Tests
{
    public class GivenAnAppWithRedirectsAndExecutableDependency : TestBase, IClassFixture<TestSetupFixture>
    {
        public string _appWithConfigProjectRoot;
        public string _appWithoutConfigProjectRoot;

        public GivenAnAppWithRedirectsAndExecutableDependency(TestSetupFixture testSetup)
        {
            _appWithConfigProjectRoot = testSetup.AppWithConfigProjectRoot;
            _appWithoutConfigProjectRoot = testSetup.AppWithoutConfigProjectRoot;
        }

        [Fact(Skip="https://github.com/dotnet/cli/issues/4514")]
        public void Tool_Command_Runs_Executable_Dependency_For_App_With_Config()
        {
            new DependencyToolInvokerCommand()
                .WithWorkingDirectory(_appWithConfigProjectRoot)
                .ExecuteWithCapturedOutput("desktop-binding-redirects", "net46", "")
                .Should().Pass().And.NotHaveStdErr();
        }

        [Fact(Skip="https://github.com/dotnet/cli/issues/4514")]
        public void Tool_Command_Runs_Executable_Dependency_For_App_Without_Config()
        {
            new DependencyToolInvokerCommand()
                .WithWorkingDirectory(_appWithoutConfigProjectRoot)
                .ExecuteWithCapturedOutput("desktop-binding-redirects", "net46", "")
                .Should().Pass().And.NotHaveStdErr();
        }
    }
}
