// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
