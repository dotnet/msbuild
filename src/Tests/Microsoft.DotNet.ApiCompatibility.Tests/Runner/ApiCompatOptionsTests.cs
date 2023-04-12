// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using Xunit;

namespace Microsoft.DotNet.ApiCompatibility.Runner.Tests
{
    public class ApiCompatOptionsTests
    {
        [Fact]
        public void Ctor_ValidArguments_PropertiesSet()
        {
            bool enableStrictMode = true;
            bool isBaselineComparison = true;

            ApiCompatRunnerOptions options = new(enableStrictMode, isBaselineComparison);

            Assert.Equal(enableStrictMode, options.EnableStrictMode);
            Assert.Equal(isBaselineComparison, options.IsBaselineComparison);
        }
    }
}
