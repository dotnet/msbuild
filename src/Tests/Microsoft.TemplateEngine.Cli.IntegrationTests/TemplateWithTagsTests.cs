// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public class TemplateWithTagsTests : EndToEndTestBase
    {
        [Theory(DisplayName = nameof(TemplateWithTagsBasicTest))]
        [InlineData("TestAssets.TemplateWithTags")]
        public void TemplateWithTagsBasicTest(string args, params string[] scripts)
        {
            Run(args, scripts);
        }
    }
}

