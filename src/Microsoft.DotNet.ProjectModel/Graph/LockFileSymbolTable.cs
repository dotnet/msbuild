// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace Microsoft.DotNet.ProjectModel.Graph
{
    public class LockFileSymbolTable
    {
        private ConcurrentDictionary<string, NuGetVersion> _versionTable = new ConcurrentDictionary<string, NuGetVersion>(Environment.ProcessorCount * 4, 1000, StringComparer.Ordinal);
        private ConcurrentDictionary<string, VersionRange> _versionRangeTable = new ConcurrentDictionary<string, VersionRange>(Environment.ProcessorCount * 4, 1000, StringComparer.Ordinal);
        private ConcurrentDictionary<string, NuGetFramework> _frameworksTable = new ConcurrentDictionary<string, NuGetFramework>(Environment.ProcessorCount * 4, 1000, StringComparer.Ordinal);
        private ConcurrentDictionary<string, string> _stringsTable = new ConcurrentDictionary<string, string>(Environment.ProcessorCount * 4, 1000, StringComparer.Ordinal);

        public NuGetVersion GetVersion(string versionString) => _versionTable.GetOrAdd(versionString, (s) => NuGetVersion.Parse(s));
        public VersionRange GetVersionRange(string versionRangeString) => _versionRangeTable.GetOrAdd(versionRangeString, (s) => VersionRange.Parse(s));
        public NuGetFramework GetFramework(string frameworkString) => _frameworksTable.GetOrAdd(frameworkString, (s) => NuGetFramework.Parse(s));
        public string GetString(string frameworkString) => _stringsTable.GetOrAdd(frameworkString, frameworkString);
    }
}
