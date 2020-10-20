using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Xunit.NetCore.Extensions
{
    public class XunitTestMethodRunnerWithAssemblyFixture : XunitTestMethodRunner
    {
        readonly Dictionary<Type, object> assemblyFixtureMappings = new Dictionary<Type, object>();
        readonly List<AssemblyFixtureAttribute> assemblyFixtureAttributes;

        public XunitTestMethodRunnerWithAssemblyFixture(List<AssemblyFixtureAttribute> assemblyFixtureAttributes,
                             ITestMethod testMethod,
                             IReflectionTypeInfo @class,
                             IReflectionMethodInfo method,
                             IEnumerable<IXunitTestCase> testCases,
                             IMessageSink diagnosticMessageSink,
                             IMessageBus messageBus,
                             ExceptionAggregator aggregator,
                             CancellationTokenSource cancellationTokenSource,
                             object[] constructorArguments)
            : base(testMethod, @class, method, testCases, diagnosticMessageSink, messageBus, aggregator, cancellationTokenSource, constructorArguments)
        {
            this.assemblyFixtureAttributes = assemblyFixtureAttributes;
        }

        protected override void AfterTestMethodStarting()
        {
            base.AfterTestMethodStarting();

            Aggregator.Run(() =>
            {
                // Instantiate all the fixtures
                foreach (var fixtureAttr in assemblyFixtureAttributes.Where(a => a.LifetimeScope == AssemblyFixtureAttribute.Scope.Method))
                    assemblyFixtureMappings[fixtureAttr.FixtureType] = Activator.CreateInstance(fixtureAttr.FixtureType);
            });
        }

        protected override void BeforeTestMethodFinished()
        {
            // Make sure we clean up everybody who is disposable, and use Aggregator.Run to isolate Dispose failures
            foreach (var disposable in assemblyFixtureMappings.Values.OfType<IDisposable>())
                Aggregator.Run(disposable.Dispose);

            base.BeforeTestMethodFinished();
        }
    }
}
