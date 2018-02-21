// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.ToolPackage
{
    internal interface IToolPackage
    {
        string PackageId { get; }

        string PackageVersion { get; }

        DirectoryPath PackageDirectory { get; }

        IReadOnlyList<CommandSettings> Commands { get; }

        void Uninstall();
    }
}
