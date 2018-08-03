// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Shared;
using System;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Tests for logging contexts. 
    /// </summary>
    public class LoggingContext_Tests
    {
        /// <summary>
        /// A few simple tests for NodeLoggingContexts. 
        /// </summary>
        [Fact]
        public void CreateValidNodeLoggingContexts()
        {
            NodeLoggingContext context = new NodeLoggingContext(new MockLoggingService(), 1, true);
            Assert.Equal(true, context.IsInProcNode);
            Assert.True(context.IsValid);

            context.LogBuildFinished(true);
            Assert.False(context.IsValid);

            Assert.Equal(1, context.BuildEventContext.NodeId);

            NodeLoggingContext context2 = new NodeLoggingContext(new MockLoggingService(), 2, false);
            Assert.Equal(false, context2.IsInProcNode);
            Assert.True(context2.IsValid);

            context2.LogBuildFinished(true);
            Assert.False(context2.IsValid);

            Assert.Equal(2, context2.BuildEventContext.NodeId);
        }

        /// <summary>
        /// Verifies that if an invalid node ID is passed to the NodeLoggingContext, it throws 
        /// an exception -- this is to guarantee that if we're passing around invalid node IDs, 
        /// we'll know about it.  
        /// </summary>
        [Fact]
        public void InvalidNodeIdOnNodeLoggingContext()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                NodeLoggingContext context = new NodeLoggingContext(new MockLoggingService(), -2, true);
            }
           );
        }
    }
}