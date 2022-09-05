// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public class KitchenSinkTests : EndToEndTestBase
    {
        [Theory(DisplayName = nameof(KitchenSinkTest))]
        [InlineData("TestAssets.ConfigurationKitchenSink --replaceThings Stuff --replaceThere You", "ConfigurationKitchenSink.json")]
        public void KitchenSinkTest(string args, params string[] scripts)
        {
            Run(args, scripts);
        }
    }
}
