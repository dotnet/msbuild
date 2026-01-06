// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Unit tests for the callback packet interface implementation.
    /// </summary>
    public class TaskHostCallbackCorrelation_Tests
    {
        /// <summary>
        /// Verifies that callback packets implement ITaskHostCallbackPacket
        /// and expose RequestId correctly through the interface.
        /// </summary>
        [Fact]
        public void ResponseTypeChecking_CorrectTypesAccepted()
        {
            var queryResponse = new TaskHostQueryResponse(1, true);
            var resourceResponse = new TaskHostResourceResponse(2, 4);

            // Both should implement ITaskHostCallbackPacket
            queryResponse.ShouldBeAssignableTo<ITaskHostCallbackPacket>();
            resourceResponse.ShouldBeAssignableTo<ITaskHostCallbackPacket>();

            // Verify RequestId is accessible through interface
            ((ITaskHostCallbackPacket)queryResponse).RequestId.ShouldBe(1);
            ((ITaskHostCallbackPacket)resourceResponse).RequestId.ShouldBe(2);
        }
    }
}
