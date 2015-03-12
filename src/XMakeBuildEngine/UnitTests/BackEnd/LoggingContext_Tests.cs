// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Tests for logging contexts.</summary>
//-----------------------------------------------------------------------

using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Tests for logging contexts. 
    /// </summary>
    [TestClass]
    public class LoggingContext_Tests
    {
        /// <summary>
        /// A few simple tests for NodeLoggingContexts. 
        /// </summary>
        [TestMethod]
        public void CreateValidNodeLoggingContexts()
        {
            NodeLoggingContext context = new NodeLoggingContext(new MockLoggingService(), 1, true);
            Assert.AreEqual(true, context.IsInProcNode);
            Assert.IsTrue(context.IsValid);

            context.LogBuildFinished(true);
            Assert.IsFalse(context.IsValid);

            Assert.AreEqual(1, context.BuildEventContext.NodeId);

            NodeLoggingContext context2 = new NodeLoggingContext(new MockLoggingService(), 2, false);
            Assert.AreEqual(false, context2.IsInProcNode);
            Assert.IsTrue(context2.IsValid);

            context2.LogBuildFinished(true);
            Assert.IsFalse(context2.IsValid);

            Assert.AreEqual(2, context2.BuildEventContext.NodeId);
        }

        /// <summary>
        /// Verifies that if an invalid node ID is passed to the NodeLoggingContext, it throws 
        /// an exception -- this is to guarantee that if we're passing around invalid node IDs, 
        /// we'll know about it.  
        /// </summary>
        [ExpectedException(typeof(InternalErrorException))]
        [TestMethod]
        public void InvalidNodeIdOnNodeLoggingContext()
        {
            NodeLoggingContext context = new NodeLoggingContext(new MockLoggingService(), -2, true);
        }
    }
}