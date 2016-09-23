// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.LibraryModel;
using NuGet.RuntimeModel;

namespace Microsoft.DotNet.ProjectModel
{
    class RuntimeGraphCollector
    {
        private const string RuntimeJsonFileName = "runtime.json";

        public static RuntimeGraph Collect(IEnumerable<LibraryDescription> libraries)
        {
            var graph = RuntimeGraph.Empty;
            foreach (var library in libraries)
            {
                if (library.Identity.Type == LibraryType.Package)
                {
                    var runtimeJson = ((PackageDescription)library).PackageLibrary.Files.FirstOrDefault(f => f == RuntimeJsonFileName);
                    if (runtimeJson != null)
                    {
                        var runtimeJsonFullName = Path.Combine(library.Path, runtimeJson);
                        graph = RuntimeGraph.Merge(graph, JsonRuntimeFormat.ReadRuntimeGraph(runtimeJsonFullName));
                    }
                }
            }
            return graph;
        }
    }
}