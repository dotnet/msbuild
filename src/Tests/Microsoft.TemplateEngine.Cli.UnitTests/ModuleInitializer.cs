// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Runtime.CompilerServices;
using VerifyTests.DiffPlex;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    public static class ModuleInitializer
    {
        [ModuleInitializer]
        public static void Init()
        {
            DerivePathInfo(
                   (_, _, type, method) => new(
                       directory: "Approvals",
                       typeName: type.Name,
                       methodName: method.Name));

            // Customize diff output of verifier
            VerifyDiffPlex.Initialize(OutputType.Compact);
        }
    }
}
