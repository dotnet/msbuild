// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tests.Commands
{
    internal class FakeDangerousFileDetector : IDangerousFileDetector
    {
        public FakeDangerousFileDetector(params string[] filesHaveIt)
        {
            FilesHaveIt = filesHaveIt;
        }

        private string[] FilesHaveIt { get; }

        public bool IsDangerous(string filePath)
        {
            if (FilesHaveIt != null && FilesHaveIt.Any(f => f == filePath))
            {
                return true;
            }

            return false;
        }
    }
}
