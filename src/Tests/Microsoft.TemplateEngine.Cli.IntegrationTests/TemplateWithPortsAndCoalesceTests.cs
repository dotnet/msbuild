// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public class TemplateWithPortsAndCoalesceTests : EndToEndTestBase
    {
        [Theory(DisplayName = nameof(TemplateWithPortsAndCoalesceRenames))]
        [InlineData("TestAssets.TemplateWithPortsAndCoalesce --userPort2 9999", "TemplateWithPortsAndCoalesceTest.json")]
        public void TemplateWithPortsAndCoalesceRenames(string args, params string[] scripts)
        {
            Run(args, scripts);
        }
    }
}
