// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.AspNetCore.Razor.Tasks
{
    public class AssemblyItem
    {
        public string Path { get; set; }

        public bool IsFrameworkReference { get; set; }

        public string AssemblyName { get; set; }
    }
}
