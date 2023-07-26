// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable


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
