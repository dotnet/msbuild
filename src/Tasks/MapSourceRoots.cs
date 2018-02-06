// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    public sealed class MapSourceRoots : TaskExtension
    {
        [Required]
        public ITaskItem[] SourceRoots { get; set; }

        [Output]
        public ITaskItem[] MappedSourceRoots { get; set; }

        private static class Names
        {
            public const string SourceRoot = nameof(SourceRoot);

            // Names of well-known SourceRoot metadata items:
            public const string SourceControl = nameof(SourceControl);
            public const string NestedRoot = nameof(NestedRoot);
            public const string ContainingRoot = nameof(ContainingRoot);
            public const string MappedPath = nameof(MappedPath);
        }

        public override bool Execute()
        {
            var topLevelMappedPaths = new Dictionary<string, string>();
            bool success = true;
            int i = 0;

            void SetTopLevelMappedPaths(bool sourceControl)
            {
                foreach (var root in SourceRoots)
                {
                    if (!string.IsNullOrEmpty(root.GetMetadata(Names.SourceControl)) == sourceControl)
                    {
                        string nestedRoot = root.GetMetadata(Names.NestedRoot);
                        if (string.IsNullOrEmpty(nestedRoot))
                        {
                            if (topLevelMappedPaths.ContainsKey(root.ItemSpec))
                            {
                                Log.LogErrorFromResources("MapSourceRoots.ContainsDuplicate", Names.SourceRoot, root.ItemSpec);
                                success = false;
                            }
                            else
                            {
                                var mappedPath = "/_" + (i == 0 ? "" : i.ToString()) + "/";
                                topLevelMappedPaths.Add(root.ItemSpec, mappedPath);
                                root.SetMetadata(Names.MappedPath, mappedPath);
                                i++;
                            }
                        }
                    }
                }
            }

            string EndWithSlash(string path)
                => (path[path.Length - 1] == '/') ? path : path + '/';

            // assign mapped paths to process source control roots first:
            SetTopLevelMappedPaths(sourceControl: true);

            // then assign mapped paths to other source control roots:
            SetTopLevelMappedPaths(sourceControl: false);

            // finally, calculate mapped paths of nested roots:
            foreach (var root in SourceRoots)
            {
                string nestedRoot = root.GetMetadata(Names.NestedRoot);
                if (!string.IsNullOrEmpty(nestedRoot))
                {
                    string containingRoot = root.GetMetadata(Names.ContainingRoot);
                    if (containingRoot != null && topLevelMappedPaths.TryGetValue(containingRoot, out var mappedTopLevelPath))
                    {
                        Debug.Assert(mappedTopLevelPath.EndsWith("/", StringComparison.Ordinal));
                        root.SetMetadata(Names.MappedPath, mappedTopLevelPath + EndWithSlash(nestedRoot.Replace('\\', '/')));
                    }
                    else
                    {
                        Log.LogErrorFromResources("MapSourceRoots.ValueOfNotFoundInItems", Names.SourceRoot + "." + Names.ContainingRoot, Names.SourceRoot, containingRoot);
                        success = false;
                    }
                }
            }

            if (success)
            {
                MappedSourceRoots = SourceRoots;
            }

            return success;
        }
    }
}
