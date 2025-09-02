// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Xunit.Abstractions;
using Xunit.Sdk;

#nullable disable

namespace Xunit.NetCore.Extensions
{
    public class XunitTestFrameworkWithAssemblyFixture : XunitTestFramework
    {
        public XunitTestFrameworkWithAssemblyFixture(IMessageSink messageSink)
            : base(messageSink)
        { }

        protected override ITestFrameworkExecutor CreateExecutor(AssemblyName assemblyName)
            => new XunitTestFrameworkExecutorWithAssemblyFixture(assemblyName, SourceInformationProvider, DiagnosticMessageSink);
    }
}
