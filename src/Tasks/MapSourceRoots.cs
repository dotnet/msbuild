// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Given a list of SourceRoot items produces a list of the same items with added <c>MappedPath</c> metadata that
    /// contains calculated deterministic source path for each SourceRoot.
    /// </summary>
    /// <remarks>
    /// Does not perform any path validation.
    /// </remarks>
    public sealed class MapSourceRoots : TaskExtension
    {
        /// <summary>
        /// SourceRoot items with the following optional well-known metadata:
        /// <list type="bullet">
        ///   <term>SourceControl</term><description>Indicates name of the source control system the source root is tracked by (e.g. Git, TFVC, etc.), if any.</description>
        ///   <term>NestedRoot</term><description>If a value is specified the source root is nested (e.g. git submodule). The value is a path to this root relative to the containing root.</description>
        ///   <term>ContainingRoot</term><description>Identifies another source root item that this source root is nested under.</description>
        /// </list>
        /// </summary>
        [Required]
        public ITaskItem[] SourceRoots { get; set; }

        [Output]
        public ITaskItem[] MappedSourceRoots { get; private set; }

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

                    // The value of ContainingRoot metadata is a file path that is compared with ItemSpec values of SourceRoot items.
                    // Since the paths in ItemSpec have backslashes replaced with slashes on non-Windows platforms we need to do the same for ContainingRoot.
                    if (containingRoot != null && topLevelMappedPaths.TryGetValue(FileUtilities.FixFilePath(containingRoot), out var mappedTopLevelPath))
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
