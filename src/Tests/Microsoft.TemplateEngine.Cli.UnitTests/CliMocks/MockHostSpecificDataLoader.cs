// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
