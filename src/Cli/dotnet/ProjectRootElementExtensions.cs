// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Construction;

namespace Microsoft.DotNet.Tools.Common
{
    public static class ProjectRootElementExtensions
    {
        public static string GetProjectTypeGuid(this ProjectRootElement rootElement)
        {
            return rootElement
                .Properties
                .FirstOrDefault(p => string.Equals(p.Name, "ProjectTypeGuids", StringComparison.OrdinalIgnoreCase))
                ?.Value
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault(g => !string.IsNullOrWhiteSpace(g));
        }
    }
}
