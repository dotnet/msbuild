using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public class DefaultIfOptionWithoutValueTests : EndToEndTestBase
    {
        [Theory(DisplayName = nameof(ConfigurationTest))]
        [InlineData("TestAssets.DefaultIfOptionWithoutValue", "NoValueDefaultsNotUsedIfSwitchesNotSpecified.json")]
        [InlineData("TestAssets.DefaultIfOptionWithoutValue --MyChoice", "NoValueDefaultForChoiceParamIsUsed.json")]
        [InlineData("TestAssets.DefaultIfOptionWithoutValue --MyString", "NoValueDefaultForStringParamIsUsed.json")]
        [InlineData("TestAssets.DefaultIfOptionWithoutValue --MyString UserString --MyChoice OtherChoice", "NoValueDefault_UserProvidedValuesAreIsUsed.json")]
        public void ConfigurationTest(string args, params string[] scripts)
        {
            Run(args, scripts);
        }
    }
}
