// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
    public class InvalidProjectFileExceptionTests
    {
        /// <summary>
        /// Verify I implemented ISerializable correctly
        /// </summary>
        /// <owner>LukaszG</owner>
        [Test]
        public void SerializeDeserialize()
        {
            InvalidProjectFileException e = new InvalidProjectFileException(
                "projectFile", 
                1, 2, 3, 4,
                "message",
                "errorSubcategory",
                "errorCode", 
                "helpKeyword");

            using (MemoryStream memstr = new MemoryStream())
            {
                BinaryFormatter frm = new BinaryFormatter();

                frm.Serialize(memstr, e);
                memstr.Position = 0;

                InvalidProjectFileException e2 = (InvalidProjectFileException)frm.Deserialize(memstr);

                Assertion.AssertEquals(e.ColumnNumber, e2.ColumnNumber);
                Assertion.AssertEquals(e.EndColumnNumber, e2.EndColumnNumber);
                Assertion.AssertEquals(e.EndLineNumber, e2.EndLineNumber);
                Assertion.AssertEquals(e.ErrorCode, e2.ErrorCode);
                Assertion.AssertEquals(e.ErrorSubcategory, e2.ErrorSubcategory);
                Assertion.AssertEquals(e.HasBeenLogged, e2.HasBeenLogged);
                Assertion.AssertEquals(e.HelpKeyword, e2.HelpKeyword);
                Assertion.AssertEquals(e.LineNumber, e2.LineNumber);
                Assertion.AssertEquals(e.Message, e2.Message);
                Assertion.AssertEquals(e.ProjectFile, e2.ProjectFile);
            }
        }
    }
}
