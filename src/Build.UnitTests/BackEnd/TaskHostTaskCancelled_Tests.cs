// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Unit Tests for TaskHostTaskCancelled packet.
    /// </summary>
    public class TaskHostTaskCancelled_Tests
    {
        /// <summary>
        /// Basic test of the constructor.
        /// </summary>
        [Fact]
        public void TestConstructor()
        {
            TaskHostTaskCancelled cancelled = new TaskHostTaskCancelled();
        }

        /// <summary>
        /// Basic test of serialization / deserialization.
        /// </summary>
        [Fact]
        public void TestTranslation()
        {
            TaskHostTaskCancelled cancelled = new TaskHostTaskCancelled();

            ((ITranslatable)cancelled).Translate(TranslationHelpers.GetWriteTranslator());
            INodePacket packet = TaskHostTaskCancelled.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());
            packet.ShouldBeOfType<TaskHostTaskCancelled>();
        }
    }
}
