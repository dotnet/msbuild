// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;

namespace Microsoft.NET.Sdk.Razor.Tool
{
    internal abstract class ExtensionDependencyChecker
    {
        public abstract bool Check(IEnumerable<string> extensionFilePaths);
    }
}
