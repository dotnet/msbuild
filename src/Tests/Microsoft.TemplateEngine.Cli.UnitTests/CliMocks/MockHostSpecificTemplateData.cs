// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
