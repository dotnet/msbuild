// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli
{
    public interface IHostSpecificDataLoader
    {
        HostSpecificTemplateData ReadHostSpecificTemplateData(ITemplateInfo templateInfo);
    }
}
