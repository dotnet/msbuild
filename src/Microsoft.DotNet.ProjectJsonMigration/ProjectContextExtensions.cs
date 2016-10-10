// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Construction;
using System.Linq;
using Microsoft.DotNet.ProjectModel;
using System.IO;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    public static class ProjectContextExtensions
    {
        public static string GetProjectName(this ProjectContext projectContext)
        {
            // _ here is just an arbitrary configuration value so we can obtain the output name
            return Path.GetFileNameWithoutExtension(projectContext.GetOutputPaths("_").CompilationFiles.Assembly);
        }
    }
}
