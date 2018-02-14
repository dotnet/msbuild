// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

using Microsoft.Build.Framework;
using Xunit;
using Shouldly;

namespace Microsoft.Build.UnitTests
{
    public class LoggerExceptionTests
    {
        /// <summary>
        /// Verify I implemented ISerializable correctly
        /// </summary>
        [Fact]
        public void SerializeDeserialize()
        {
            LoggerException e = new LoggerException("message",
                new Exception("innerException"),
                "errorCode",
                "helpKeyword");

            using (MemoryStream memstr = new MemoryStream())
            {
                BinaryFormatter frm = new BinaryFormatter();

                frm.Serialize(memstr, e);
                memstr.Position = 0;

                LoggerException e2 = (LoggerException)frm.Deserialize(memstr);

                e2.ErrorCode.ShouldBe(e.ErrorCode);
                e2.HelpKeyword.ShouldBe(e.HelpKeyword);
                e2.Message.ShouldBe(e.Message);
                e2.InnerException.ShouldNotBeNull();
                e2.InnerException.Message.ShouldBe(e.InnerException?.Message);
            }
        }

        /// <summary>
        /// Verify I implemented ISerializable correctly, using other ctor
        /// </summary>
        [Fact]
        public void SerializeDeserialize2()
        {
            LoggerException e = new LoggerException("message");

            using (MemoryStream memstr = new MemoryStream())
            {
                BinaryFormatter frm = new BinaryFormatter();

                frm.Serialize(memstr, e);
                memstr.Position = 0;

                LoggerException e2 = (LoggerException)frm.Deserialize(memstr);

                e2.ErrorCode.ShouldBeNull();
                e2.HelpKeyword.ShouldBeNull();
                e2.Message.ShouldBe(e.Message);
                e2.InnerException.ShouldBeNull();
            }
        }
    }
}
