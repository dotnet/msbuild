using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Xunit.NetCore.Extensions
{
    public class XunitTestAssemblyRunnerWithAssemblyFixture : XunitTestAssemblyRunner
    {
        readonly Dictionary<Type, object> assemblyFixtureMappings = new Dictionary<Type, object>();
        List<AssemblyFixtureAttribute> assemblyFixtureAttributes;

        public XunitTestAssemblyRunnerWithAssemblyFixture(ITestAssembly testAssembly,
                                                          IEnumerable<IXunitTestCase> testCases,
                                                          IMessageSink diagnosticMessageSink,
                                                          IMessageSink executionMessageSink,
                                                          ITestFrameworkExecutionOptions executionOptions)
            : base(testAssembly, testCases, diagnosticMessageSink, executionMessageSink, executionOptions)
        { }

        protected override async Task AfterTestAssemblyStartingAsync()
        {
            // Let everything initialize
            await base.AfterTestAssemblyStartingAsync();

            // Go find all the AssemblyFixtureAttributes adorned on the test assembly
            Aggregator.Run(() =>
            {
                var fixturesAttrs = ((IReflectionAssemblyInfo)TestAssembly.Assembly).Assembly
                                                                                    .GetCustomAttributes(typeof(AssemblyFixtureAttribute), false)
                                                                                    .Cast<AssemblyFixtureAttribute>()
                                                                                    .ToList();

                this.assemblyFixtureAttributes = fixturesAttrs;

                // Instantiate all the fixtures
                foreach (var fixtureAttr in fixturesAttrs.Where(a => a.LifetimeScope == AssemblyFixtureAttribute.Scope.Assembly))
                    assemblyFixtureMappings[fixtureAttr.FixtureType] = Activator.CreateInstance(fixtureAttr.FixtureType);
            });
        }

        protected override Task BeforeTestAssemblyFinishedAsync()
        {
            // Make sure we clean up everybody who is disposable, and use Aggregator.Run to isolate Dispose failures
            foreach (var disposable in assemblyFixtureMappings.Values.OfType<IDisposable>())
                Aggregator.Run(disposable.Dispose);

            return base.BeforeTestAssemblyFinishedAsync();
        }

        protected override Task<RunSummary> RunTestCollectionAsync(IMessageBus messageBus,
                                                                   ITestCollection testCollection,
                                                                   IEnumerable<IXunitTestCase> testCases,
                                                                   CancellationTokenSource cancellationTokenSource)
            => new XunitTestCollectionRunnerWithAssemblyFixture(assemblyFixtureMappings, assemblyFixtureAttributes,
                testCollection, testCases, DiagnosticMessageSink, messageBus, TestCaseOrderer, new ExceptionAggregator(Aggregator), cancellationTokenSource).RunAsync();
    }
}
