// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.NET.TestFramework
{
    /// <summary>
    /// Represents a target that will copy some set of files to a given location after some other target completes.
    /// Useful for verifying the contents of an output group in a test.
    /// </summary>
    public class CopyFilesTarget
    {
        public CopyFilesTarget(string targetName, string targetToRunAfter, string sourceFiles, string destination)
        {
            TargetName = targetName;
            TargetToRunAfter = targetToRunAfter;
            SourceFiles = sourceFiles;
            Destination = destination;
        }

        public string TargetName { get; private set; }
        public string TargetToRunAfter { get; private set; }
        public string SourceFiles { get; private set; }
        public string Destination { get; private set; }
    }
}
