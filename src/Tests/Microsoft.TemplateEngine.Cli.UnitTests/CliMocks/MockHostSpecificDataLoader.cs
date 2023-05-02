// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
