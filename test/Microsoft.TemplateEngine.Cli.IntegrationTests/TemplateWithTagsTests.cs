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

