// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public class ValueFormsTests : EndToEndTestBase
    {
        [Theory(DisplayName = nameof(ValueFormsTest))]
        [InlineData("TestAssets.TemplateWithValueForms --foo Test.Value6 --param1 MyPascalTestValue --param2 myCamelTestValue --param3 \"my test text\"", "TemplateWithValueForms.json")]
        [InlineData("TestAssets.TemplateWithDerivedSymbolWithValueForms -n Test.AppSeven", "DerivedSymbolWithValueForms.json")]
        public void ValueFormsTest(string args, params string[] scripts)
        {
            Run(args, scripts);
        }
    }
}
