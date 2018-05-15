// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
