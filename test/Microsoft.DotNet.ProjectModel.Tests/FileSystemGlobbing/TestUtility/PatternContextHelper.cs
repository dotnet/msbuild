// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.ProjectModel.FileSystemGlobbing.Internal;

namespace Microsoft.DotNet.ProjectModel.FileSystemGlobbing.Tests.TestUtility
{
    internal static class PatternContextHelper
    {
        public static void PushDirectory(IPatternContext context, params string[] directoryNames)
        {
            foreach (var each in directoryNames)
            {
                var directory = new MockDirectoryInfo(null, null, string.Empty, each, null);
                context.PushDirectory(directory);
            }
        }
    }
}