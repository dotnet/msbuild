// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using System.Collections;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Framework;
using Microsoft.Build.Exceptions;
using System.Text.RegularExpressions;

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public class InternalLoggerExceptionTests
    {
        /// <summary>
        /// Verify I implemented ISerializable correctly
        /// </summary>
        [TestMethod]
        public void SerializeDeserialize()
        {
            InternalLoggerException e = new InternalLoggerException("message",
                new Exception("innerException"),
                new BuildStartedEventArgs("evMessage", "evHelpKeyword"),
                "errorCode",
                "helpKeyword",
                false);

            using (MemoryStream memstr = new MemoryStream())
            {
                BinaryFormatter frm = new BinaryFormatter();

                frm.Serialize(memstr, e);
                memstr.Position = 0;

                InternalLoggerException e2 = (InternalLoggerException)frm.Deserialize(memstr);

                Assert.AreEqual(e.BuildEventArgs.Message, e2.BuildEventArgs.Message);
                Assert.AreEqual(e.BuildEventArgs.HelpKeyword, e2.BuildEventArgs.HelpKeyword);
                Assert.AreEqual(e.ErrorCode, e2.ErrorCode);
                Assert.AreEqual(e.HelpKeyword, e2.HelpKeyword);
                Assert.AreEqual(e.Message, e2.Message);
                Assert.AreEqual(e.InnerException.Message, e2.InnerException.Message);
            }
        }
    }
}





