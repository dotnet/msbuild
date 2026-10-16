// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
#if NETFRAMEWORK
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
#endif

using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Utilities;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Verify the functioning of the BuildMessageEventArgs class.
    /// </summary>
    public class BuildMessageEventArgs_Tests
    {
        /// <summary>
        /// Trivially exercise event args default ctors to boost Frameworks code coverage
        /// </summary>
        [Fact]
        public void EventArgsCtors()
        {
            BuildMessageEventArgs bmea = new BuildMessageEventArgs2();
            bmea = new BuildMessageEventArgs("Message", "HelpKeyword", "Sender", MessageImportance.Low);
            bmea = new BuildMessageEventArgs("Message", "HelpKeyword", "Sender", MessageImportance.Low, DateTime.Now);
            bmea = new BuildMessageEventArgs("{0}", "HelpKeyword", "Sender", MessageImportance.Low, DateTime.Now, "Message");
            bmea = new BuildMessageEventArgs("Subcategory", "Code", "File", 1, 2, 3, 4, "Message", "HelpKeyword", "Sender", MessageImportance.Low);
            bmea = new BuildMessageEventArgs("Subcategory", "Code", "File", 1, 2, 3, 4, "Message", "HelpKeyword", "Sender", MessageImportance.Low, DateTime.Now);
            bmea = new BuildMessageEventArgs("Subcategory", "Code", "File", 1, 2, 3, 4, "{0}", "HelpKeyword", "Sender", MessageImportance.Low, DateTime.Now, "Message");
            bmea = new BuildMessageEventArgs(null, null, null, MessageImportance.Low);
            bmea = new BuildMessageEventArgs(null, null, null, MessageImportance.Low, DateTime.Now);
            bmea = new BuildMessageEventArgs(null, null, null, MessageImportance.Low, DateTime.Now, null);
            bmea = new BuildMessageEventArgs(null, null, null, 0, 0, 0, 0, null, null, null, MessageImportance.Low);
            bmea = new BuildMessageEventArgs(null, null, null, 0, 0, 0, 0, null, null, null, MessageImportance.Low, DateTime.Now);
            bmea = new BuildMessageEventArgs(null, null, null, 0, 0, 0, 0, null, null, null, MessageImportance.Low, DateTime.Now, null);
        }

        [Fact]
        public void FormatMessageWithoutMutationPreservesArguments()
        {
            object[] args = ["argument"];
            var eventArgs = new BuildMessageEventArgs("Message {0}", null, null, MessageImportance.High, DateTime.Now, args);

            eventArgs.FormatMessageWithoutMutation().ShouldBe("Message argument");
            eventArgs.ArgCount.ShouldBe(1);
            eventArgs[0].ShouldBeSameAs(args[0]);

            eventArgs.Message.ShouldBe("Message argument");
            eventArgs.ArgCount.ShouldBe(0);
        }

        [Fact]
        public void MessageArgsFormatWithoutMaterializingArray()
        {
            var eventArgs = new BuildMessageEventArgs("Message {0} {1}", null, null, MessageImportance.High);
            eventArgs.SetArgs(MessageArgs.Create("argument", 42));

            eventArgs.ArgCount.ShouldBe(2);
            eventArgs[0].ShouldBe("argument");
            eventArgs[1].ShouldBe(42);

            eventArgs.FormatMessageWithoutMutation().ShouldBe("Message argument 42");
            eventArgs.ArgCount.ShouldBe(2);

            eventArgs.Message.ShouldBe("Message argument 42");
            eventArgs.ArgCount.ShouldBe(0);
        }

        [Fact]
        public void LazyFormattedBuildEventArgsHandlesMessageArgsFormatException()
        {
            var eventArgs = new BuildMessageEventArgs("Value {1}", null, null, MessageImportance.High);
            eventArgs.SetArgs(MessageArgs.Create(42));

            eventArgs.Message.ShouldStartWith("\"Value {1}\"\nSystem.FormatException");
        }

#if NETFRAMEWORK
        [Fact]
        public void MessageArgsMaterializeForFormatterSerialization()
        {
            var eventArgs = new BuildMessageEventArgs("Message {0} {1}", null, null, MessageImportance.High);
            eventArgs.SetArgs(MessageArgs.Create("argument", 42));

            using var stream = new MemoryStream();
            var formatter = new BinaryFormatter();
            formatter.Serialize(stream, eventArgs);
            eventArgs.ArgCount.ShouldBe(2);

            stream.Position = 0;
            var deserializedEventArgs = (BuildMessageEventArgs)formatter.Deserialize(stream);

            deserializedEventArgs.ArgCount.ShouldBe(2);
            deserializedEventArgs.Message.ShouldBe("Message argument 42");
        }
#endif

        /// <summary>
        /// Create a derived class so that we can test the default constructor in order to increase code coverage and
        /// verify this code path does not cause any exceptions.
        /// </summary>
        private sealed class BuildMessageEventArgs2 : BuildMessageEventArgs
        {
            /// <summary>
            /// Default constructor
            /// </summary>
            public BuildMessageEventArgs2()
                : base()
            {
            }
        }
    }
}
