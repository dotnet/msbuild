// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.DotNet.ProjectModel.FileSystemGlobbing
{
    public class PatternMatchingResult
    {
        public PatternMatchingResult(IEnumerable<FilePatternMatch> files)
        {
            Files = files;
        }

        public IEnumerable<FilePatternMatch> Files { get; set; }
    }
}