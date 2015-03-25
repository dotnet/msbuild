// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

using Microsoft.Build.Framework;
using NUnit.Framework;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class LoggerExceptionTests
    {
        /// <summary>
        /// Verify I implemented ISerializable correctly
        /// </summary>
        [Test]
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

                Assert.AreEqual(e.ErrorCode, e2.ErrorCode);
                Assert.AreEqual(e.HelpKeyword, e2.HelpKeyword);
                Assert.AreEqual(e.Message, e2.Message);
                Assert.AreEqual(e.InnerException.Message, e2.InnerException.Message);
            }
        }

        /// <summary>
        /// Verify I implemented ISerializable correctly, using other ctor
        /// </summary>
        [Test]
        public void SerializeDeserialize2()
        {
            LoggerException e = new LoggerException("message");

            using (MemoryStream memstr = new MemoryStream())
            {
                BinaryFormatter frm = new BinaryFormatter();

                frm.Serialize(memstr, e);
                memstr.Position = 0;

                LoggerException e2 = (LoggerException)frm.Deserialize(memstr);

                Assert.AreEqual(null, e2.ErrorCode);
                Assert.AreEqual(null, e2.HelpKeyword);
                Assert.AreEqual(e.Message, e2.Message);
                Assert.AreEqual(null, e2.InnerException);
            }
        }
    }
}





