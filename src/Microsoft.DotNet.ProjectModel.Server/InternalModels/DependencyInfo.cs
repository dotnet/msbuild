// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.ProjectModel.Server.Models;

namespace Microsoft.DotNet.ProjectModel.Server.InternalModels
{
    internal class DependencyInfo
    {
        public Dictionary<string, DependencyDescription> Dependencies { get; set; }
    }
}
