// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.ProjectModel.FileSystemGlobbing.Abstractions;

namespace Microsoft.DotNet.ProjectModel.FileSystemGlobbing.Internal.PatternContexts
{
    public class PatternContextLinearExclude : PatternContextLinear
    {
        public PatternContextLinearExclude(ILinearPattern pattern)
            : base(pattern)
        {
        }

        public override bool Test(DirectoryInfoBase directory)
        {
            if (IsStackEmpty())
            {
                throw new InvalidOperationException("Can't test directory before entering a directory.");
            }

            if (Frame.IsNotApplicable)
            {
                return false;
            }

            return IsLastSegment() && TestMatchingSegment(directory.Name);
        }
    }
}
