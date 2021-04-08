using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.UnitTests.CliMocks
{
    internal class MockHostSpecificDataLoader : IHostSpecificDataLoader
    {
        // If needed, extend to return mock data.
        public HostSpecificTemplateData ReadHostSpecificTemplateData(ITemplateInfo templateInfo)
        {
            return HostSpecificTemplateData.Default;
        }
    }
}
