// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli
{
    public interface IHostSpecificDataLoader
    {
        HostSpecificTemplateData ReadHostSpecificTemplateData(ITemplateInfo templateInfo);
    }
}
