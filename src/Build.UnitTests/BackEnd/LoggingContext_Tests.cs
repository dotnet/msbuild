// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;
using Shouldly;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Tests for logging contexts.
    /// </summary>
    [TestClass]
    public class LoggingContext_Tests
    {
        private readonly TestContext _output;

        public LoggingContext_Tests(TestContext outputHelper)
        {
            _output = outputHelper;
        }

        /// <summary>
        /// A few simple tests for NodeLoggingContexts.
        /// </summary>
        [MSBuildTestMethod]
        public void CreateValidNodeLoggingContexts()
        {
            NodeLoggingContext context = new NodeLoggingContext(new MockLoggingService(_output.WriteLine), 1, true);
            context.IsInProcNode.ShouldBeTrue();
            context.IsValid.ShouldBeTrue();

            context.LogBuildFinished(true);
            context.IsValid.ShouldBeFalse();

            context.BuildEventContext.NodeId.ShouldBe(1);

            NodeLoggingContext context2 = new NodeLoggingContext(new MockLoggingService(_output.WriteLine), 2, false);
            context2.IsInProcNode.ShouldBeFalse();
            context2.IsValid.ShouldBeTrue();

            context2.LogBuildFinished(true);
            context2.IsValid.ShouldBeFalse();

            context2.BuildEventContext.NodeId.ShouldBe(2);
        }

        /// <summary>
        /// Verifies that if an invalid node ID is passed to the NodeLoggingContext, it throws
        /// an exception -- this is to guarantee that if we're passing around invalid node IDs,
        /// we'll know about it.
        /// </summary>
        [MSBuildTestMethod]
        public void InvalidNodeIdOnNodeLoggingContext()
        {
            Assert.ThrowsExactly<InternalErrorException>(() =>
            {
                _ = new NodeLoggingContext(new MockLoggingService(), -2, true);
            });
        }

        [MSBuildTestMethod]
        public void HasLoggedErrors()
        {
            NodeLoggingContext context = new NodeLoggingContext(new MockLoggingService(_output.WriteLine), 1, true);
            context.HasLoggedErrors.ShouldBeFalse();

            context.LogCommentFromText(Framework.MessageImportance.High, "Test message");
            context.HasLoggedErrors.ShouldBeFalse();

            context.LogWarningFromText(null, null, null, null, "Test warning");
            context.HasLoggedErrors.ShouldBeFalse();

            context.LogErrorFromText(null, null, null, null, "Test error");
            context.HasLoggedErrors.ShouldBeTrue();
        }
    }
}
