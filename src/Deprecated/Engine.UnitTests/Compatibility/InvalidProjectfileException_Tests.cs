// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

using NUnit.Framework;

using Microsoft.Build;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;

namespace Microsoft.Build.UnitTests.OM.OrcasCompatibility
{
    /// <summary>
    ///   Fixture Class for the v9 OM Public Interface Compatibility Tests. Import Class.
    ///   Also see Toolset tests in the Project test class.
    /// </summary>
    [TestFixture]
    public class InvalidProjectfileException_Tests
    {
        /// <summary>
        ///  Ctor test, default construction is supported.
        /// </summary>
        [Test]
        public void CtorDefault()
        {
            InvalidProjectFileException invalidProjectFileException = new InvalidProjectFileException();
        }

        /// <summary>
        ///  Ctor Test, with message
        /// </summary>
        [Test]
        public void CtorMessageArity1()
        {
            InvalidProjectFileException invalidProjectFileException = new InvalidProjectFileException("Message");
            Assertion.AssertEquals("Message", invalidProjectFileException.Message);
            Assertion.AssertEquals(0, invalidProjectFileException.LineNumber);
            Assertion.AssertEquals(0, invalidProjectFileException.ColumnNumber);
        }

        ///<summary>
        ///  Ctor Test, with null message
        /// </summary>
        [Test]
        public void CtorMessageArity1_null()
        {
            string nullString = null; // typed null as to hit correct ctor overloaded.
            InvalidProjectFileException invalidProjectFileException = new InvalidProjectFileException(nullString);
            InvalidProjectFileException invalidProjectFileException2 = new InvalidProjectFileException(nullString, new Exception("MessageInner"));
        }

        /// <summary>
        ///  Ctor Test, with an empty string message
        /// </summary>
        [Test]
        public void CtorMessageArity1_empty()
        {
            InvalidProjectFileException invalidProjectFileException = new InvalidProjectFileException(String.Empty);
            InvalidProjectFileException invalidProjectFileException2 = new InvalidProjectFileException(String.Empty, new Exception("MessageInner"));
        }

        /// <summary>
        ///  Ctor Test, with message and inner exception
        /// </summary>
        [Test]
        public void Ctor_Arity2InnerException()
        {
            InvalidProjectFileException invalidProjectFileException = new InvalidProjectFileException("Message", new Exception("MessageInner"));
            Assertion.AssertEquals("Message", invalidProjectFileException.Message);
            Assertion.AssertEquals("MessageInner", invalidProjectFileException.InnerException.Message);
            Assertion.AssertEquals(0, invalidProjectFileException.LineNumber);
            Assertion.AssertEquals(0, invalidProjectFileException.ColumnNumber);
        }

        /// <summary>
        ///  Ctor Test, Construct with a mock project element. There is no way to test this on concrete project as the xml tree is internalised.
        /// </summary>
        [Test]
        public void CtorArity4()
        {
            string message = "Message";
            InvalidProjectFileException invalidProjectFileException =
                    new InvalidProjectFileException(new XmlDocument().CreateElement("name"), message, "errorSubCategory", "errorCode", "HelpKeyword");
            Assertion.AssertEquals(String.Empty, invalidProjectFileException.ProjectFile);

            // preserve a bug in Orcas SP1:  if projectFile is empty but non-null, extra spaces get added to the message.
            Assertion.AssertEquals(message + "  ", invalidProjectFileException.Message);
            Assertion.AssertEquals("errorSubCategory", invalidProjectFileException.ErrorSubcategory);
            Assertion.AssertEquals("errorCode", invalidProjectFileException.ErrorCode);
            Assertion.AssertEquals("HelpKeyword", invalidProjectFileException.HelpKeyword);
            Assertion.AssertEquals(0, invalidProjectFileException.LineNumber);
            Assertion.AssertEquals(0, invalidProjectFileException.ColumnNumber);
        }

        /// <summary>
        ///  Ctor Test, Construct with a mock project element and a Null message string
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void CtorArity4_NullMessageString()
        {
            string message = null;
            InvalidProjectFileException invalidProjectFileException =
                    new InvalidProjectFileException(new XmlDocument().CreateElement("name"), message, "subcategory", "ErrorCode", "HelpKeyword");
            Assertion.AssertEquals(String.Empty, invalidProjectFileException.ProjectFile);
            Assertion.AssertEquals(message, invalidProjectFileException.Message);
            Assertion.AssertNull(invalidProjectFileException.ErrorSubcategory);
            Assertion.AssertEquals("ErrorCode", invalidProjectFileException.ErrorCode);
            Assertion.AssertEquals("HelpKeyword", invalidProjectFileException.HelpKeyword);
            Assertion.AssertEquals(0, invalidProjectFileException.LineNumber);
            Assertion.AssertEquals(0, invalidProjectFileException.ColumnNumber);
        }

        /// <summary>
        ///  Ctor Test 
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void CtorArity4_EmptyMessageString()
        {
            string message = String.Empty;
            InvalidProjectFileException invalidProjectFileException =
                    new InvalidProjectFileException(new XmlDocument().CreateElement("Name"), message, String.Empty, String.Empty, String.Empty);
        }

        /// <summary>
        ///  Ctor Testt
        /// </summary>
        [Test]
        public void CtorArity4_EmptyStringOtherParams()
        {
            string message = "Message";
            InvalidProjectFileException invalidProjectFileException =
                    new InvalidProjectFileException(new XmlDocument().CreateElement("Name"), message, String.Empty, String.Empty, String.Empty);
            Assertion.AssertEquals(String.Empty, invalidProjectFileException.ProjectFile);

            // preserve a bug in Orcas SP1:  if projectFile is empty but non-null, extra spaces get added to the message.
            Assertion.AssertEquals(message + "  ", invalidProjectFileException.Message);
            Assertion.AssertEquals(String.Empty, invalidProjectFileException.ErrorSubcategory);
            Assertion.AssertEquals(String.Empty, invalidProjectFileException.ErrorCode);
            Assertion.AssertEquals(String.Empty, invalidProjectFileException.HelpKeyword);
            Assertion.AssertEquals(0, invalidProjectFileException.LineNumber);
            Assertion.AssertEquals(0, invalidProjectFileException.ColumnNumber);
        }

        /// <summary>
        ///  Ctor Test 
        /// </summary>
        [Test]
        public void CtorArity4_NullStringOtherParams()
        {
            string message = "Message";
            InvalidProjectFileException invalidProjectFileException =
                new InvalidProjectFileException(new XmlDocument().CreateElement("Name"), message, null, null, null);
            Assertion.AssertEquals(String.Empty, invalidProjectFileException.ProjectFile);

            // preserve a bug in Orcas SP1:  if projectFile is empty but non-null, extra spaces get added to the message.
            Assertion.AssertEquals(message + "  ", invalidProjectFileException.Message);
            Assertion.AssertEquals(null, invalidProjectFileException.ErrorSubcategory);
            Assertion.AssertEquals(null, invalidProjectFileException.ErrorCode);
            Assertion.AssertEquals(null, invalidProjectFileException.HelpKeyword);
        }

        /// <summary>
        ///  Ctor Test
        /// </summary>
        [Test]
        public void Ctor_Arity9PositveInts()
        {
            string message = "Message";
            string projectFile = @"c:\ProjectFile";
            InvalidProjectFileException invalidProjectFileException =
                new InvalidProjectFileException(projectFile, 1, 10, 11, 12, message, "errorSubCategory", "errorCode", "HelpKeyword");
            Assertion.AssertEquals(message + "  " + projectFile, invalidProjectFileException.Message);
            Assertion.AssertEquals("errorSubCategory", invalidProjectFileException.ErrorSubcategory);
            Assertion.AssertEquals("errorCode", invalidProjectFileException.ErrorCode);
            Assertion.AssertEquals("HelpKeyword", invalidProjectFileException.HelpKeyword);
            Assertion.AssertEquals(1, invalidProjectFileException.LineNumber);
            Assertion.AssertEquals(10, invalidProjectFileException.ColumnNumber);
            Assertion.AssertEquals(11, invalidProjectFileException.EndLineNumber);
            Assertion.AssertEquals(12, invalidProjectFileException.EndColumnNumber);
        }

        /// <summary>
        ///  Ctor Test, this enforces lack of bounds checking and the lack of range checking (end can come before start) 
        ///  on the line and column number params. 
        /// </summary>
        [Test]
        public void CtorArity9NegativeInts()
        {
            string message = "Message";
            string projectFile = @"c:\ProjectFile";
            InvalidProjectFileException invalidProjectFileException =
                new InvalidProjectFileException(projectFile, -1, -10, -11, -12, message, "errorSubCategory", "errorCode", "HelpKeyword");
            Assertion.AssertEquals(message + "  " + projectFile, invalidProjectFileException.Message);
            Assertion.AssertEquals("errorSubCategory", invalidProjectFileException.ErrorSubcategory);
            Assertion.AssertEquals("errorCode", invalidProjectFileException.ErrorCode);
            Assertion.AssertEquals("HelpKeyword", invalidProjectFileException.HelpKeyword);
            Assertion.AssertEquals(-1, invalidProjectFileException.LineNumber);
            Assertion.AssertEquals(-10, invalidProjectFileException.ColumnNumber);
            Assertion.AssertEquals(-11, invalidProjectFileException.EndLineNumber);
            Assertion.AssertEquals(-12, invalidProjectFileException.EndColumnNumber);
            Assertion.AssertEquals(projectFile, invalidProjectFileException.ProjectFile);
        }

        /// <summary>
        ///  BaseMessage, get and set
        /// </summary>
        [Test]
        public void BaseMessage()
        {
            string message = "Message";
            InvalidProjectFileException invalidProjectFileException =
                new InvalidProjectFileException("ProjectFile", 0, 0, 0, 0, message, "errorSubCategory", "errorCode", "HelpKeyword");
            Assertion.AssertEquals(message, invalidProjectFileException.BaseMessage);
        }

        /// <summary>
        /// XML Serialization Test, not supported, throws invalid operation Exception. 
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void SerializationXML()
        {
            InvalidProjectFileException toolSetException = new InvalidProjectFileException("Message", new Exception("innerException"));
            MemoryStream memoryStream = null;
            try
            {
                memoryStream = new MemoryStream();
                XmlSerializer xs = new XmlSerializer(typeof(InvalidProjectFileException));
                XmlTextWriter xmlTextWriter = new XmlTextWriter(memoryStream, Encoding.UTF8);
                xs.Serialize(xmlTextWriter, toolSetException);
                memoryStream = (MemoryStream)xmlTextWriter.BaseStream;
            }
            finally
            {
                memoryStream.Close();
            }
        }

        /// <summary>
        /// Binary Serialization Test, serialize the exception out and back in from a stream. This uses the protected constructor
        /// </summary>
        [Test]
        public void SerializationBinary()
        {
            string message = "Message";
            string projectFile = @"c:\ProjectFile";
            MemoryStream memoryStream = null;
            InvalidProjectFileException invalidProjectFileException =
                new InvalidProjectFileException(projectFile, 1, 2, 3, 4, message, "errorSubCategory", "errorCode", "HelpKeyword");
            
            try
            {
                memoryStream = new MemoryStream();
                IFormatter binaryForamtter = new BinaryFormatter();
                binaryForamtter.Serialize(memoryStream, invalidProjectFileException);
                memoryStream.Position = 0; // reset pointer into stream for read
                Object returnObj = binaryForamtter.Deserialize(memoryStream);
                Assertion.Assert(returnObj is InvalidProjectFileException);
                InvalidProjectFileException outException = ((InvalidProjectFileException)returnObj);
                Assertion.AssertEquals(message + "  " + projectFile, outException.Message);
                Assertion.AssertEquals("errorSubCategory", outException.ErrorSubcategory);
                Assertion.AssertEquals("errorCode", outException.ErrorCode);
                Assertion.AssertEquals("HelpKeyword", outException.HelpKeyword);
                Assertion.AssertEquals(1, outException.LineNumber);
                Assertion.AssertEquals(2, outException.ColumnNumber);
                Assertion.AssertEquals(3, outException.EndLineNumber);
                Assertion.AssertEquals(4, outException.EndColumnNumber);
                Assertion.AssertEquals(projectFile, outException.ProjectFile);
            }
            finally
            {
                memoryStream.Close();
            }
        }

        /// <summary>
        /// Binary Serialization Test, serialize the exception out and back in from a stream with an InnerException. This uses the protected constructor
        /// </summary>
        [Test]
        public void SerializationBinaryInnerException()
        {
            MemoryStream memoryStream = null;
            InvalidProjectFileException invalidProjectFileException =
                new InvalidProjectFileException("Message", new Exception("innerException"));
            try
            {
                memoryStream = new MemoryStream();
                IFormatter binaryForamtter = new BinaryFormatter();
                binaryForamtter.Serialize(memoryStream, invalidProjectFileException);
                memoryStream.Position = 0; // reset pointer into stream for read
                Object returnObj = binaryForamtter.Deserialize(memoryStream);
                Assertion.Assert(returnObj is InvalidProjectFileException);
                InvalidProjectFileException outException = ((InvalidProjectFileException)returnObj);
                Assertion.AssertEquals("Message", outException.Message);
                Assertion.AssertEquals("innerException", outException.InnerException.Message);
            }
            finally
            {
                memoryStream.Close();
            }
        }
    }
}
