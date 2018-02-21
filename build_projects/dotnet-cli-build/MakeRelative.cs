// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Cli.Build.Framework;

namespace Microsoft.DotNet.Cli.Build
{
    public class MakeRelative : Task
    {
        [Required]
        public string Path1 { get; set; }

        [Required]
        public string Path2 { get; set; }

        [Output]
        public ITaskItem RelativePath { get; set; }

        public override bool Execute()
        {
            RelativePath = ToTaskItem(Path1, Path2, Path.GetRelativePath(Path1, Path2));

            return true;
        }

        private static TaskItem ToTaskItem(string path1, string path2, string relativePath)
        {
            var framework = new TaskItem();
            framework.ItemSpec = relativePath;

            framework.SetMetadata("Path1", path1);
            framework.SetMetadata("Path2", path2);
            framework.SetMetadata("RelativePath", relativePath);

            return framework;
        }
    }
}
