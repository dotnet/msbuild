// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if FEATURE_BINARY_SERIALIZATION
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

using Microsoft.Build.Framework;
using Xunit;

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

                Assert.Equal(e.ErrorCode, e2.ErrorCode);
                Assert.Equal(e.HelpKeyword, e2.HelpKeyword);
                Assert.Equal(e.Message, e2.Message);
                Assert.Equal(e.InnerException.Message, e2.InnerException.Message);
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

                Assert.Equal(null, e2.ErrorCode);
                Assert.Equal(null, e2.HelpKeyword);
                Assert.Equal(e.Message, e2.Message);
                Assert.Equal(null, e2.InnerException);
            }
        }
    }
}

#endif
