// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Framework;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public class InternalLoggerExceptionTests
    {
        /// <summary>
        /// Verify I implemented ISerializable correctly
        /// </summary>
        [Fact]
        public void SerializeDeserialize()
        {
            InternalLoggerException e = new InternalLoggerException(
                "message",
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

                Assert.Equal(e.BuildEventArgs.Message, e2.BuildEventArgs.Message);
                Assert.Equal(e.BuildEventArgs.HelpKeyword, e2.BuildEventArgs.HelpKeyword);
                Assert.Equal(e.ErrorCode, e2.ErrorCode);
                Assert.Equal(e.HelpKeyword, e2.HelpKeyword);
                Assert.Equal(e.Message, e2.Message);
                Assert.Equal(e.InnerException.Message, e2.InnerException.Message);
            }
        }
    }
}
