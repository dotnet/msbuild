using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.UnitTests.CliMocks
{
    public class MockHostSpecificDataLoader : IHostSpecificDataLoader
    {
        // If needed, extend to return mock data.
        public HostSpecificTemplateData ReadHostSpecificTemplateData(ITemplateInfo templateInfo)
        {
            return HostSpecificTemplateData.Default;
        }
    }
}
