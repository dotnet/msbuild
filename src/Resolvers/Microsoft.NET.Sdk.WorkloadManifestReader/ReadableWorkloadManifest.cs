// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    public class ReadableWorkloadManifest
    {
        public string ManifestId { get; }

        public string ManifestDirectory { get; }

        public string ManifestPath { get; }

        public string ManifestFeatureBand { get; }

        readonly Func<Stream> _openManifestStreamFunc;


        readonly Func<Stream?> _openLocalizationStream;

        public ReadableWorkloadManifest(string manifestId, string manifestDirectory, string manifestPath, string manifestFeatureBand, Func<Stream> openManifestStreamFunc, Func<Stream?> openLocalizationStream)
        {
            ManifestId = manifestId;
            ManifestPath = manifestPath;
            ManifestDirectory = manifestDirectory;
            ManifestFeatureBand = manifestFeatureBand;
            _openManifestStreamFunc = openManifestStreamFunc;
            _openLocalizationStream = openLocalizationStream;
        }

        public Stream OpenManifestStream()
        {
            return _openManifestStreamFunc();
        }

        public Stream? OpenLocalizationStream()
        {
            return _openLocalizationStream();
        }

    }
}
