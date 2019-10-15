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
    }
}
