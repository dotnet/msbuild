// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
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
