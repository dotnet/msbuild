using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public class PrecedenceSelectionTests : EndToEndTestBase
    {
        [Theory(DisplayName = nameof(VerifyTemplateContent))]
        [InlineData("mvc --skip-restore", "MvcNoAuthTest.json", "MvcFramework20Test.json")]
        [InlineData("mvc --skip-restore -au individual", "MvcIndAuthTest.json", "MvcFramework20Test.json")]

        [InlineData("mvc --skip-restore -f netcoreapp1.0", "MvcNoAuthTest.json", "MvcFramework10Test.json")]
        [InlineData("mvc --skip-restore -au individual -f netcoreapp1.0", "MvcIndAuthTest.json", "MvcFramework10Test.json")]

        [InlineData("mvc --skip-restore -f netcoreapp1.1", "MvcNoAuthTest.json", "MvcFramework11Test.json")]
        [InlineData("mvc --skip-restore -au individual -f netcoreapp1.1", "MvcIndAuthTest.json", "MvcFramework11Test.json")]

        [InlineData("mvc --skip-restore -f netcoreapp2.0", "MvcNoAuthTest.json", "MvcFramework20Test.json")]
        [InlineData("mvc --skip-restore -au individual -f netcoreapp2.0", "MvcIndAuthTest.json", "MvcFramework20Test.json")]
        public void VerifyTemplateContent(string args, params string[] scripts)
        {
            Run(args, scripts);
        }
    }
}
