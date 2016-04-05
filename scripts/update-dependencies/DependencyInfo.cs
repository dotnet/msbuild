// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Scripts
{
    public class DependencyInfo
    {
        public string Name { get; set; }
        public string IdPattern { get; set; }
        public string IdExclusionPattern { get; set; }
        public string NewReleaseVersion { get; set; }

        public bool IsUpdated { get; set; }
    }
}
