using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Xunit.NetCore.Extensions
{
    public class XunitTestClassRunnerWithAssemblyFixture : XunitTestClassRunner
    {
        readonly Dictionary<Type, object> assemblyFixtureMappings = new Dictionary<Type, object>();
        readonly List<AssemblyFixtureAttribute> assemblyFixtureAttributes;

        public XunitTestClassRunnerWithAssemblyFixture(
            List<AssemblyFixtureAttribute> assemblyFixtureAttributes,
            ITestClass testClass, IReflectionTypeInfo @class, IEnumerable<IXunitTestCase> testCases,
            IMessageSink diagnosticMessageSink, IMessageBus messageBus, ITestCaseOrderer testCaseOrderer, ExceptionAggregator aggregator,
            CancellationTokenSource cancellationTokenSource, IDictionary<Type, object> collectionFixtureMappings)
            : base(testClass, @class, testCases, diagnosticMessageSink, messageBus, testCaseOrderer, aggregator, cancellationTokenSource, collectionFixtureMappings)
        {
            this.assemblyFixtureAttributes = assemblyFixtureAttributes;
        }

        protected override async Task AfterTestClassStartingAsync()
        {
            await base.AfterTestClassStartingAsync();

            Aggregator.Run(() =>
            {
                // Instantiate all the fixtures
                foreach (var fixtureAttr in assemblyFixtureAttributes.Where(a => a.LifetimeScope == AssemblyFixtureAttribute.Scope.Class))
                    assemblyFixtureMappings[fixtureAttr.FixtureType] = Activator.CreateInstance(fixtureAttr.FixtureType);
            });
        }
        protected override Task BeforeTestClassFinishedAsync()
        {
            // Make sure we clean up everybody who is disposable, and use Aggregator.Run to isolate Dispose failures
            foreach (var disposable in assemblyFixtureMappings.Values.OfType<IDisposable>())
                Aggregator.Run(disposable.Dispose);

            return base.BeforeTestClassFinishedAsync();
        }

        protected override Task<RunSummary> RunTestMethodAsync(ITestMethod testMethod, IReflectionMethodInfo method, IEnumerable<IXunitTestCase> testCases, object[] constructorArguments)
        {
            return new XunitTestMethodRunnerWithAssemblyFixture(assemblyFixtureAttributes,
                testMethod, Class, method, testCases, DiagnosticMessageSink, MessageBus,
                new ExceptionAggregator(Aggregator), CancellationTokenSource, constructorArguments).RunAsync();
        }
    }
}
