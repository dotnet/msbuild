// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Extensions.DependencyModel;

namespace Microsoft.DotNet.Cli.Build
{
    /// <summary>
    /// Merges additional .deps.json files into target .deps.json files.
    /// </summary>
    public class AddToDeps : Task
    {
        /// <summary>
        /// Paths to target .deps.json files, into which <see cref="AdditionalDeps" /> will be merged.
        /// These files will be overwritten with the merge result.
        /// </summary>
        [Required]
        public string[] TargetDeps { get; set; }

        /// <summary>
        /// Paths to additional .deps.json files to merge into <see cref="TargetDeps" />.
        /// </summary>
        [Required]
        public string[] AdditionalDeps { get; set; }

        public override bool Execute()
        {
            DependencyContext additionalContext = Read(AdditionalDeps.First());

            foreach (string additionalPath in AdditionalDeps.Skip(1))
            {
                additionalContext = additionalContext.Merge(Read(additionalPath));
            }

            foreach (string targetPath in TargetDeps)
            {
                DependencyContext targetContext = Read(targetPath).Merge(additionalContext);
                Write(targetContext, targetPath);
            }

            return true;
        }

        private static DependencyContext Read(string path)
        {
            using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new DependencyContextJsonReader())
            {
                return reader.Read(stream);
            }
        }

        private static void Write(DependencyContext context, string path)
        {
            using (FileStream stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                var writer = new DependencyContextWriter(); // unlike reader, writer is not disposable
                writer.Write(context, stream);
            }
        }
    }
}