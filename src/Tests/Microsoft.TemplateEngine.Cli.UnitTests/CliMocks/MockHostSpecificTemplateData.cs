// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
