// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using VerifyTests;

namespace Microsoft.DotNet.New.Tests
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
