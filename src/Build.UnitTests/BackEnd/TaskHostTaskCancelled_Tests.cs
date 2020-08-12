// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Build.BackEnd;
using Xunit;
using Shouldly;

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
