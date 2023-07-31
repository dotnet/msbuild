// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
