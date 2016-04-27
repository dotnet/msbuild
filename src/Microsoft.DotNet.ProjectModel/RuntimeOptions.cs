// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.ProjectModel
{
    public class RuntimeOptions
    {
        public bool GcServer { get; set; }

        public bool GcConcurrent { get; set; }
    }
}