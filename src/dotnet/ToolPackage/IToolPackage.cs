// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Versioning;

namespace Microsoft.DotNet.ToolPackage
{
    internal interface IToolPackage
    {
        PackageId Id { get; }

        NuGetVersion Version { get; }

        DirectoryPath PackageDirectory { get; }

        IReadOnlyList<RestoredCommand> Commands { get; }

        IEnumerable<string> Warnings { get; }

        IReadOnlyList<FilePath> PackagedShims { get; }
    }
}
