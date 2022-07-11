// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using VerifyTests;

namespace Dotnet_new3.IntegrationTests
{
    public class VerifySettingsFixture : IDisposable
    {
        public VerifySettingsFixture()
        {
            Settings = new VerifySettings();
            Settings.UseDirectory("Approvals");
        }

        internal VerifySettings Settings { get; }

        public void Dispose() { }
    }
}
