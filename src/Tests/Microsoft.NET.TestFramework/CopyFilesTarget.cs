// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework
{
    /// <summary>
    /// Represents a target that will copy some set of files to a given location after some other target completes.
    /// Useful for verifying the contents of an output group in a test.
    /// </summary>
    public class CopyFilesTarget
    {
        public CopyFilesTarget(string targetName, string targetToRunAfter, string sourceFiles, string condition, string destination)
        {
            TargetName = targetName;
            TargetToRunAfter = targetToRunAfter;
            SourceFiles = sourceFiles;
            Condition = condition;
            Destination = destination;
        }

        public string TargetName { get; private set; }
        public string TargetToRunAfter { get; private set; }
        public string SourceFiles { get; private set; }
        public string Condition { get; private set; }
        public string Destination { get; private set; }
    }
}
