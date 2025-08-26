// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// THE ASSEMBLY BUILT FROM THIS SOURCE FILE HAS BEEN DEPRECATED FOR YEARS. IT IS BUILT ONLY TO PROVIDE
// BACKWARD COMPATIBILITY FOR API USERS WHO HAVE NOT YET MOVED TO UPDATED APIS. PLEASE DO NOT SEND PULL
// REQUESTS THAT CHANGE THIS FILE WITHOUT FIRST CHECKING WITH THE MAINTAINERS THAT THE FIX IS REQUIRED.

using System;
using System.Reflection;
using System.Collections;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using NUnit.Framework;
using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;
using System.Text.RegularExpressions;


namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class InternalLoggerExceptionTests
    {
        /// <summary>
        /// Verify I implemented ISerializable correctly
        /// </summary>
        /// <owner>LukaszG</owner>
        [Test]
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

                InternalLoggerException e2 = (InternalLoggerException) frm.Deserialize(memstr);

                Assertion.AssertEquals(e.BuildEventArgs.Message, e2.BuildEventArgs.Message);
                Assertion.AssertEquals(e.BuildEventArgs.HelpKeyword, e2.BuildEventArgs.HelpKeyword);
                Assertion.AssertEquals(e.ErrorCode, e2.ErrorCode);
                Assertion.AssertEquals(e.HelpKeyword, e2.HelpKeyword);
                Assertion.AssertEquals(e.Message, e2.Message);
                Assertion.AssertEquals(e.InnerException.Message, e2.InnerException.Message);
            }
        }
    }
}





