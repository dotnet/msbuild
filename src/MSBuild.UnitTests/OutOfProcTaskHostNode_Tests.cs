// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Microsoft.Build.BackEnd;
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
    /// carried by <see cref="TaskHostConfiguration"/> is assigned from each incoming configuration
    /// before it is read -- which makes the few remaining items easy to drop by accident.
    /// </remarks>
    public class OutOfProcTaskHostNode_Tests
    {
        [Fact]
        public void PrepareForNextBuild_ResetsStateTheNextBuildDoesNotReestablish()
        {
            OutOfProcTaskHostNode node = new();

            // Created through the field's own type: the cache type is internal to another assembly.
            FieldInfo cacheField = typeof(OutOfProcTaskHostNode).GetField("_registeredTaskObjectCache", BindingFlags.Instance | BindingFlags.NonPublic)!;
            cacheField.SetValue(node, Activator.CreateInstance(cacheField.FieldType, nonPublic: true));

            // SetEnvironment deletes anything absent from the snapshot, so hand it the environment
            // this process already has and the restore becomes a no-op.
            Dictionary<string, string> currentEnvironment = new(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                currentEnvironment[(string)entry.Key] = (string?)entry.Value ?? string.Empty;
            }

            SetPrivateField(node, "_savedEnvironment", currentEnvironment);

            // A task can set this through IBuildEngine, and nothing re-establishes it per task, so
            // it would otherwise be inherited by the next build.
            node.AllowFailureWithoutError = true;

            // A cancellation arriving as the build ends would otherwise stay signalled and spin the
            // next build's wait loop.
            ManualResetEvent taskCancelledEvent = (ManualResetEvent)GetPrivateField(node, "_taskCancelledEvent")!;
            taskCancelledEvent.Set();

            // The reset restores the working directory, which is process-wide state in this test.
            string originalDirectory = Directory.GetCurrentDirectory();

            try
            {
                typeof(OutOfProcTaskHostNode)
                    .GetMethod("PrepareForNextBuild", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(node, null);
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDirectory);
            }

            node.AllowFailureWithoutError.ShouldBeFalse("a task host must not inherit AllowFailureWithoutError from the previous build");
            taskCancelledEvent.WaitOne(0).ShouldBeFalse("a cancellation from the previous build must not still be signalled");
        }

        private static void SetPrivateField(OutOfProcTaskHostNode node, string name, object value)
            => typeof(OutOfProcTaskHostNode)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(node, value);

        private static object? GetPrivateField(OutOfProcTaskHostNode node, string name)
            => typeof(OutOfProcTaskHostNode)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(node);
    }
}
