using System.Collections.Generic;
using System.Reflection;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace AssemblyFixtureExample.XunitExtensions
{
    public class XunitTestFrameworkExecutorWithAssemblyFixture : XunitTestFrameworkExecutor
    {
        public XunitTestFrameworkExecutorWithAssemblyFixture(AssemblyName assemblyName, ISourceInformationProvider sourceInformationProvider, IMessageSink diagnosticMessageSink)
            : base(assemblyName, sourceInformationProvider, diagnosticMessageSink)
        { }

        protected override async void RunTestCases(IEnumerable<IXunitTestCase> testCases, IMessageSink executionMessageSink, ITestFrameworkExecutionOptions executionOptions)
        {
            using (var assemblyRunner = new XunitTestAssemblyRunnerWithAssemblyFixture(TestAssembly, testCases, DiagnosticMessageSink, executionMessageSink, executionOptions))
                await assemblyRunner.RunAsync();
        }
    }
}
