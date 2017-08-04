using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public class FileRenameTests : EndToEndTestBase
    {
        [Theory(DisplayName = nameof(VerifyTemplateContentRenames))]
        [InlineData("TestAssets.TemplateWithRenames --foo baz", "FileRenamesTest.json")]
        [InlineData("TestAssets.TemplateWithSourceName --name baz", "FileRenamesTest.json")]
        [InlineData("TestAssets.TemplateWithUnspecifiedSourceName --name baz", "NegativeFileRenamesTest.json")]
        [InlineData("TestAssets.TemplateWithSourceNameAndCustomSourcePath --name bar", "CustomSourcePathRenameTest.json")]
        [InlineData("TestAssets.TemplateWithSourceNameAndCustomTargetPath --name bar", "CustomTargetPathRenameTest.json")]
        [InlineData("TestAssets.TemplateWithSourceNameAndCustomSourceAndTargetPath --name bar", "CustomSourceAndTargetPathRenameTest.json")]
        public void VerifyTemplateContentRenames(string args, params string[] scripts)
        {
            Run(args, scripts);
        }
    }
}
