using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Cli.UnitTests.CliMocks
{
    public class MockHostSpecificTemplateData : HostSpecificTemplateData
    {
        public MockHostSpecificTemplateData(Dictionary<string, IReadOnlyDictionary<string, string>> symbolInfo)
            : base(symbolInfo)
        {
        }
    }
}
