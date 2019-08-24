// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Xunit.NetCore.Extensions
{
    /// <summary>Wraps another test case that should be skipped.</summary>
    internal sealed class SkippedTestCase : LongLivedMarshalByRefObject, IXunitTestCase
    {
        private readonly IXunitTestCase _testCase;
        private readonly string _skippedReason;

        internal SkippedTestCase(IXunitTestCase testCase, string skippedReason)
        {
            _testCase = testCase;
            _skippedReason = skippedReason;
        }

        public string DisplayName => _testCase.DisplayName;

        public IMethodInfo Method => _testCase.Method;

        public string SkipReason => _skippedReason;

        public ISourceInformation SourceInformation { get => _testCase.SourceInformation; set => _testCase.SourceInformation = value; }

        public ITestMethod TestMethod => _testCase.TestMethod;

        public object[] TestMethodArguments => _testCase.TestMethodArguments;

        public Dictionary<string, List<string>> Traits => _testCase.Traits;

        public string UniqueID => _testCase.UniqueID;

        public int Timeout => _testCase.Timeout;

        public Exception InitializationException => _testCase.InitializationException;

        public void Deserialize(IXunitSerializationInfo info) { _testCase.Deserialize(info); }

        public Task<RunSummary> RunAsync(
            IMessageSink diagnosticMessageSink, IMessageBus messageBus, object[] constructorArguments,
            ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource)
        {
            return new XunitTestCaseRunner(this, DisplayName, _skippedReason, constructorArguments, TestMethodArguments, messageBus, aggregator, cancellationTokenSource).RunAsync();
        }

        public void Serialize(IXunitSerializationInfo info) { _testCase.Serialize(info); }
    }
}
