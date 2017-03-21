using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public class KitchenSinkTests : EndToEndTestBase
    {
        [Theory(DisplayName = nameof(ConfigurationTest))]
        [InlineData("TestAssets.ConfigurationKitchenSink --replaceThings Stuff --replaceThere You", "ConfigurationKitchenSink.json")]
        public void ConfigurationTest(string args, params string[] scripts)
        {
            Run(args, scripts);
        }
    }
}
