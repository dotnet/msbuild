// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Cli
{
    public interface IInstaller
    {
        void InstallPackages(IEnumerable<string> installationRequests);
    }
}
