// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.ProjectModel.FileSystemGlobbing.Internal;

namespace Microsoft.DotNet.ProjectModel.FileSystemGlobbing.Tests.PatternContexts
{
    internal class MockRecursivePathSegment : IPathSegment
    {
        public bool CanProduceStem {  get { return false; } }

        public bool Match(string value)
        {
            return false;
        }
    }
}