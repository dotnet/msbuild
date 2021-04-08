using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Cli.UnitTests.CliMocks
{
    internal class MockHostSpecificTemplateData : HostSpecificTemplateData
    {
        public MockHostSpecificTemplateData(Dictionary<string, IReadOnlyDictionary<string, string>> symbolInfo)
            : base(symbolInfo)
        {
        }
    }
}
