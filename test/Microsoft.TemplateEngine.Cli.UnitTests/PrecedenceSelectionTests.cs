using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public class PrecedenceSelectionTests : EndToEndTestBase
    {
        [Theory(DisplayName = nameof(VerifyTemplateContent))]
        [InlineData("mvc", "MvcNoAuthTest.json", "MvcFramework21Test.json")]
        [InlineData("mvc -au individual", "MvcIndAuthTest.json", "MvcFramework21Test.json")]

        [InlineData("mvc -f netcoreapp1.0", "MvcNoAuthTest.json", "MvcFramework10Test.json")]
        [InlineData("mvc -au individual -f netcoreapp1.0", "MvcIndAuthTest.json", "MvcFramework10Test.json")]

        [InlineData("mvc -f netcoreapp1.1", "MvcNoAuthTest.json", "MvcFramework11Test.json")]
        [InlineData("mvc -au individual -f netcoreapp1.1", "MvcIndAuthTest.json", "MvcFramework11Test.json")]

        [InlineData("mvc -f netcoreapp2.0", "MvcNoAuthTest.json", "MvcFramework20Test.json")]
        [InlineData("mvc -au individual -f netcoreapp2.0", "MvcIndAuthTest.json", "MvcFramework20Test.json")]

        [InlineData("mvc -f netcoreapp2.1", "MvcNoAuthTest.json", "MvcFramework21Test.json")]
        [InlineData("mvc -au individual -f netcoreapp2.1", "MvcIndAuthTest.json", "MvcFramework21Test.json")]
        public void VerifyTemplateContent(string args, params string[] scripts)
        {
            Run(args, scripts);
        }
    }
}
