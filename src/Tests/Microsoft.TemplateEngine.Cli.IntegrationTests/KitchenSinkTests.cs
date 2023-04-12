// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
