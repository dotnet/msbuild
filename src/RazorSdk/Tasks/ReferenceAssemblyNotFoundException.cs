// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;

namespace Microsoft.AspNetCore.Razor.Tasks
{
    internal class ReferenceAssemblyNotFoundException : Exception
    {
        public ReferenceAssemblyNotFoundException(string fileName)
        {
            FileName = fileName;
        }

        public string FileName { get; }
    }
}
