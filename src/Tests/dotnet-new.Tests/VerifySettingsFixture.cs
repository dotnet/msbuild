// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using VerifyTests.DiffPlex;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    public class VerifySettingsFixture : IDisposable
    {
        private static Lazy<bool> _called = new Lazy<bool>(() =>
        {
            Verifier.DerivePathInfo(
               (_, _, type, method) => new(
                   directory: "Approvals",
                   typeName: type.Name,
                   methodName: method.Name));

            // Customize diff output of verifier
            VerifyDiffPlex.Initialize(OutputType.Compact);

            return true;
        });

        public VerifySettingsFixture() => _ = _called.Value;

        public void Dispose() { }
    }
}
