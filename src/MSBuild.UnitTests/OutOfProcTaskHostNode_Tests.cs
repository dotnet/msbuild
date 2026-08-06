// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.Build.CommandLine;
using Shouldly;
using Xunit;

#nullable enable

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Tests for the state a task host resets when it serves more than one build.
    /// </summary>
    /// <remarks>
    /// A task host that exits at the end of a build is reset by construction: the next build gets a
    /// brand new node. One that stays connected to its owner across builds is not, so it resets in
    /// place. Only state the next build does not re-establish for itself belongs there -- everything
    /// carried by the incoming TaskHostConfiguration is assigned before it is read -- which makes
    /// the few remaining items easy to drop by accident.
    /// </remarks>
    public class OutOfProcTaskHostNode_Tests
    {
        [Fact]
        public void PrepareForNextBuild_ResetsStateTheNextBuildDoesNotReestablish()
        {
            OutOfProcTaskHostNode node = new();

            // A task can set this through IBuildEngine, and nothing re-establishes it per task, so
            // it would otherwise be inherited by the next build.
            node.AllowFailureWithoutError = true;

            // A cancellation arriving as the build ends would otherwise stay signalled and spin the
            // next build's wait loop.
            node.TaskCancelledEvent.Set();

            // The reset restores the working directory, which is process-wide state in this test.
            string originalDirectory = Directory.GetCurrentDirectory();

            try
            {
                node.PrepareForNextBuild();
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDirectory);
            }

            node.AllowFailureWithoutError.ShouldBeFalse("a task host must not inherit AllowFailureWithoutError from the previous build");
            node.TaskCancelledEvent.WaitOne(0).ShouldBeFalse("a cancellation from the previous build must not still be signalled");
        }
    }
}
