// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.ProjectModel.FileSystemGlobbing.Internal
{
    public interface IPathSegment
    {
        bool CanProduceStem { get; }

        bool Match(string value);
    }
}