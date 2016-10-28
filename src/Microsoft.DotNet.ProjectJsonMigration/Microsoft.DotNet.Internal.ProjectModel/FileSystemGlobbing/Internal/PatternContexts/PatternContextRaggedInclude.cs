// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.Internal.ProjectModel.FileSystemGlobbing.Abstractions;
using Microsoft.DotNet.Internal.ProjectModel.FileSystemGlobbing.Internal.PathSegments;

namespace Microsoft.DotNet.Internal.ProjectModel.FileSystemGlobbing.Internal.PatternContexts
{
    internal class PatternContextRaggedInclude : PatternContextRagged
    {
        public PatternContextRaggedInclude(IRaggedPattern pattern)
            : base(pattern)
        {
        }

        public override void Declare(Action<IPathSegment, bool> onDeclare)
        {
            if (IsStackEmpty())
            {
                throw new InvalidOperationException("Can't declare path segment before entering a directory.");
            }

            if (Frame.IsNotApplicable)
            {
                return;
            }

            if (IsStartingGroup() && Frame.SegmentIndex < Frame.SegmentGroup.Count)
            {
                onDeclare(Frame.SegmentGroup[Frame.SegmentIndex], false);
            }
            else
            {
                onDeclare(WildcardPathSegment.MatchAll, false);
            }
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

            if (IsStartingGroup() && !TestMatchingSegment(directory.Name))
            {
                // deterministic not-included
                return false;
            }

            return true;
        }
    }
}
