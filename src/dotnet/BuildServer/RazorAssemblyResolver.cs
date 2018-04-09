// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Build.Execution;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.BuildServer
{
    internal class RazorAssemblyResolver : IRazorAssemblyResolver
    {
        private readonly IDirectory _directory;

        public RazorAssemblyResolver(IDirectory directory = null)
        {
            _directory = directory ?? FileSystemWrapper.Default.Directory;
        }

        public IEnumerable<FilePath> EnumerateRazorToolAssemblies()
        {
            HashSet<string> seen = new HashSet<string>();

            var globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // This property disables default item globbing to improve performance
                // This should be safe because we are not evaluating items, only properties
                { Constants.EnableDefaultItems, "false" }
            };

            foreach (var projectFile in _directory.EnumerateFiles(Directory.GetCurrentDirectory(), "*.*proj"))
            {
                var project = new ProjectInstance(projectFile, globalProperties, null);
                var path = project.GetPropertyValue("_RazorToolAssembly");
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                if (!seen.Add(path))
                {
                    continue;
                }

                yield return new FilePath(path);
            }
        }
    }
}
