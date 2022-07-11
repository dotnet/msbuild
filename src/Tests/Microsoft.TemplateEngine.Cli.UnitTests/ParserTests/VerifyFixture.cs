// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VerifyTests;

namespace Microsoft.TemplateEngine.Cli.UnitTests.ParserTests
{
    public class VerifyFixture : IDisposable
    {
        public VerifyFixture()
        {
            Settings = new VerifySettings();
            Settings.UseDirectory("Approvals");
        }

        internal VerifySettings Settings { get; }

        public void Dispose() { }
    }
}
