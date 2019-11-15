// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Frameworks;

namespace Microsoft.DotNet.CommandFactory
{
    public interface ICommandFactory
    {
        ICommand Create(
            string commandName,
            IEnumerable<string> args,
            NuGetFramework framework = null,
            string configuration = Constants.DefaultConfiguration);
    }
}
