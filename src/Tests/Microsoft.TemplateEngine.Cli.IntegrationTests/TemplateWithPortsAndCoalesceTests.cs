// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
