// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public class DefaultIfOptionWithoutValueTests : EndToEndTestBase
    {
        [Theory(DisplayName = nameof(DefaultIfOptionWithoutValueTest))]
        [InlineData("TestAssets.DefaultIfOptionWithoutValue", "NoValueDefaultsNotUsedIfSwitchesNotSpecified.json")]
        [InlineData("TestAssets.DefaultIfOptionWithoutValue --MyChoice", "NoValueDefaultForChoiceParamIsUsed.json")]
        [InlineData("TestAssets.DefaultIfOptionWithoutValue --MyString", "NoValueDefaultForStringParamIsUsed.json")]
        [InlineData("TestAssets.DefaultIfOptionWithoutValue --MyString UserString --MyChoice OtherChoice", "NoValueDefault_UserProvidedValuesAreIsUsed.json")]
        public void DefaultIfOptionWithoutValueTest(string args, params string[] scripts)
        {
            Run(args, scripts);
        }
    }
}
