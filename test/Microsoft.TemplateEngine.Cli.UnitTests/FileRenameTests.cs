using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public class FileRenameTests : EndToEndTestBase
    {
        [Theory(DisplayName = nameof(VerifyTemplateContentRenames))]
        [InlineData("TestAssets.TemplateWithRenames --foo baz", "FileRenamesTest.json")]
        [InlineData("TestAssets.TemplateWithSourceName --name baz", "FileRenamesTest.json")]
        [InlineData("TestAssets.TemplateWithUnspecifiedSourceName --name baz", "NegativeFileRenamesTest.json")]
        public void VerifyTemplateContentRenames(string args, params string[] scripts)
        {
            Run(args, scripts);
        }
    }
}
