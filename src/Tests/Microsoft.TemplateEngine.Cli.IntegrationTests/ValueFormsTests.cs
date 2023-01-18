// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
