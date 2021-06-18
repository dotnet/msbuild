// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Extensions
{
    internal static class ITemplateInfoExtensions
    {
        internal static bool IsHiddenByHostFile(this ITemplateInfo template, IHostSpecificDataLoader hostSpecificDataLoader)
        {
            HostSpecificTemplateData hostData = hostSpecificDataLoader.ReadHostSpecificTemplateData(template);
            return hostData.IsHidden;
        }
    }
}
