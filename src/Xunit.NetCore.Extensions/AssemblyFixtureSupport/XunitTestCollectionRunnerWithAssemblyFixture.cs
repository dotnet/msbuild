using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Xunit.NetCore.Extensions
{
    public class XunitTestCollectionRunnerWithAssemblyFixture : XunitTestCollectionRunner
    {
        readonly Dictionary<Type, object> assemblyFixtureMappings;
        readonly IMessageSink diagnosticMessageSink;
        readonly List<AssemblyFixtureAttribute> assemblyFixtureAttributes;

        public XunitTestCollectionRunnerWithAssemblyFixture(Dictionary<Type, object> assemblyFixtureMappings,
                                                            List<AssemblyFixtureAttribute> assemblyFixtureAttributes,
                                                            ITestCollection testCollection,
                                                            IEnumerable<IXunitTestCase> testCases,
                                                            IMessageSink diagnosticMessageSink,
                                                            IMessageBus messageBus,
                                                            ITestCaseOrderer testCaseOrderer,
                                                            ExceptionAggregator aggregator,
                                                            CancellationTokenSource cancellationTokenSource)
            : base(testCollection, testCases, diagnosticMessageSink, messageBus, testCaseOrderer, aggregator, cancellationTokenSource)
        {
            if (assemblyFixtureAttributes == null)
            {
                throw new ArgumentNullException(nameof(assemblyFixtureAttributes));
            }

            this.assemblyFixtureMappings = assemblyFixtureMappings;
            this.assemblyFixtureAttributes = assemblyFixtureAttributes;
            this.diagnosticMessageSink = diagnosticMessageSink;
        }

        protected override Task<RunSummary> RunTestClassAsync(ITestClass testClass, IReflectionTypeInfo @class, IEnumerable<IXunitTestCase> testCases)
        {
            // Don't want to use .Concat + .ToDictionary because of the possibility of overriding types,
            // so instead we'll just let collection fixtures override assembly fixtures.
            var combinedFixtures = new Dictionary<Type, object>(assemblyFixtureMappings);
            foreach (var kvp in CollectionFixtureMappings)
                combinedFixtures[kvp.Key] = kvp.Value;

            
            return new XunitTestClassRunnerWithAssemblyFixture(assemblyFixtureAttributes, testClass, @class, testCases, diagnosticMessageSink, MessageBus, TestCaseOrderer, new ExceptionAggregator(Aggregator), CancellationTokenSource, combinedFixtures).RunAsync();
        }
    }
}
