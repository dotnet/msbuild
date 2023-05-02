// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenANETBuildExtensionsError
    {
        [Fact]
        public void It_is_compiled_with_extensions_specific_name()
        {
            // Regression test for https://github.com/dotnet/sdk/issues/2061
            // Infrastructure changes made #if EXTENSIONS that changes the task name to not apply.
            // This test would fail to compile if we made that mistake again.
            new NETBuildExtensionsError();
        }
    }
}
