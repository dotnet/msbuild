// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

using NUnit.Framework;

using Microsoft.Build.BuildEngine;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;

namespace Microsoft.Build.UnitTests.OM.OrcasCompatibility
{
    /// <summary>
    /// STUB Fixture Class for the v9 OM Public Interface Compatibility Tests. RemoteErrorException class 
    /// Also see Toolset tests in the Project test class.
    /// </summary>
    [TestFixture]
    public class InvalidToolsetDefinitionException_Tests
    {
        /// <summary>
        /// Ctor Test, ensure message is set.
        /// </summary>
        [Test]
        public void CtorMessage()
        {
            InvalidToolsetDefinitionException toolSetException = new InvalidToolsetDefinitionException("Message");
            Assertion.AssertEquals("Message", toolSetException.Message);
        }

        /// <summary>
        /// Ctor Test, set a null message. We do not guard against this. 
        /// </summary>
        [Test]
        public void CtorMessage_Null()
        {
            InvalidToolsetDefinitionException toolSetException = new InvalidToolsetDefinitionException(null); 
        }

        /// <summary>
        /// Ctor Test, ensure message is set to empty
        /// </summary>
        [Test]
        public void CtorMessage_Empty()
        {
            InvalidToolsetDefinitionException toolSetException = new InvalidToolsetDefinitionException(String.Empty);
            Assertion.AssertEquals(String.Empty, toolSetException.Message);
        }

        /// <summary>
        /// Ctor Test, set message and inner exception
        /// </summary>
        [Test]
        public void CtorArity2InnerException()
        {
            InvalidToolsetDefinitionException toolSetException = new InvalidToolsetDefinitionException("Message", new Exception("Inner"));
            Assertion.AssertEquals("Message", toolSetException.Message);
            Assertion.AssertEquals("Inner", toolSetException.InnerException.Message);
        }

        /// <summary>
        /// Ctor Test,set message and error code
        /// </summary>
        [Test]
        public void CtorArity2ErrorMessage()
        {
            InvalidToolsetDefinitionException toolSetException = new InvalidToolsetDefinitionException("Message", "Error");
            Assertion.AssertEquals("Message", toolSetException.Message);
            Assertion.AssertEquals("Error", toolSetException.ErrorCode);
        }

        /// <summary>
        /// Ctor Test, set message  error code and Inner excetion
        /// </summary>
        [Test]
        public void CtorArity3()
        {
            InvalidToolsetDefinitionException toolSetException = new InvalidToolsetDefinitionException("Message", "Error", new Exception("Inner"));
            Assertion.AssertEquals("Message", toolSetException.Message);
            Assertion.AssertEquals("Error", toolSetException.ErrorCode);
            Assertion.AssertEquals("Inner", toolSetException.InnerException.Message);
        }

        /// <summary>
        /// XML Serialization Test, not supported, throws invalid operation Exception. 
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void SerializationXML()
        {
            InvalidToolsetDefinitionException toolSetException = new InvalidToolsetDefinitionException("Message", "errorCode");
            MemoryStream memoryStream = null;
            try
            {
                memoryStream = new MemoryStream();
                XmlSerializer xs = new XmlSerializer(typeof(InvalidToolsetDefinitionException));
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
            MemoryStream memoryStream = null;
            InvalidToolsetDefinitionException toolSetException = new InvalidToolsetDefinitionException("Message", "errorCode");
            try
            {
                memoryStream = new MemoryStream();
                IFormatter binaryForamtter = new BinaryFormatter();
                binaryForamtter.Serialize(memoryStream, toolSetException);
                memoryStream.Position = 0; // reset pointer into stream for read
                Object returnObj = binaryForamtter.Deserialize(memoryStream);
                Assertion.Assert(returnObj is InvalidToolsetDefinitionException);
                Assertion.AssertEquals("Message", ((InvalidToolsetDefinitionException)returnObj).Message);
            }
            finally
            {
                memoryStream.Close();
            }
        }

        /// <summary>
        /// Binary Serialization Test, serialize a inherited exception out and back in from a stream. 
        /// </summary>
        [Test]
        public void ProtectedConstructorTest()
        {
            ExtendsInvalidToolsetDefinitionException toolSetException = new ExtendsInvalidToolsetDefinitionException("Message", "Error");
            MemoryStream memoryStream = null;
            try
            {
                memoryStream = new MemoryStream();
                IFormatter binaryForamtter = new BinaryFormatter();
                binaryForamtter.Serialize(memoryStream, toolSetException);
                memoryStream.Position = 0; // Reset pointer into stream for read
                Object returnObj = binaryForamtter.Deserialize(memoryStream);
                Assertion.Assert(returnObj is ExtendsInvalidToolsetDefinitionException);
                Assertion.AssertEquals("Message", ((ExtendsInvalidToolsetDefinitionException)returnObj).Message);
                Assertion.AssertEquals("Error", ((ExtendsInvalidToolsetDefinitionException)returnObj).ErrorCode);
            }
            finally
            {
                memoryStream.Close();
            }
        }

        /// <summary>
        /// Helper class to expose the protected constructor through extension
        /// </summary>
        [Serializable]
        internal class ExtendsInvalidToolsetDefinitionException : InvalidToolsetDefinitionException, ISerializable
        {
            /// <summary>
            /// Constructor that takes an MSBuild error code, Override
            /// </summary>
            public ExtendsInvalidToolsetDefinitionException(string message, string errorCode)
                : base(message, errorCode)
            { 
            }

            /// <summary>
            /// Basic Constructor Override
            /// </summary>
            public ExtendsInvalidToolsetDefinitionException(SerializationInfo info, StreamingContext context)
                : base(info, context) 
            { 
            }
        }
    }
}
