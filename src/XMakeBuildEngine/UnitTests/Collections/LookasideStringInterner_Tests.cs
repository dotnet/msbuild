//-----------------------------------------------------------------------
// <copyright file="LookasideStringInterner_Tests.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Tests for the lookaside string interner used for serialization.</summary>
//-----------------------------------------------------------------------

using System.Collections.Generic;
using Microsoft.Build.Collections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Microsoft.Build.Evaluation;
using Microsoft.Build.UnitTests;
using System.Collections;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;
using Microsoft.Build.Construction;
using Microsoft.Build.BackEnd;
using System.IO;

namespace Microsoft.Build.UnitTests.OM.Collections
{
    [TestClass]
    public class LookasideStringInterner_Tests
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Empty()
        {
            var interner = new LookasideStringInterner(StringComparer.OrdinalIgnoreCase, 1);
            interner.GetString(0);
        }

        [TestMethod]
        public void BasicInterning()
        {
            var interner = new LookasideStringInterner(StringComparer.OrdinalIgnoreCase, 1);
            int nullIndex = interner.Intern(null);
            int emptyIndex = interner.Intern(String.Empty);
            int strIndex = interner.Intern("abc123def456");

            Assert.AreEqual(interner.Intern(null), nullIndex);
            Assert.AreEqual(interner.Intern(String.Empty), emptyIndex);
            Assert.AreEqual(interner.Intern("abc123def456"), strIndex);

            Assert.AreEqual(interner.GetString(nullIndex), null);
            Assert.AreEqual(interner.GetString(emptyIndex), String.Empty);
            Assert.AreEqual(interner.GetString(strIndex), "abc123def456");
        }

        [TestMethod]
        public void Serialization()
        {
            var interner = new LookasideStringInterner(StringComparer.OrdinalIgnoreCase, 1);
            int nullIndex = interner.Intern(null);
            int emptyIndex = interner.Intern(String.Empty);
            int strIndex = interner.Intern("abc123def456");

            MemoryStream stream = new MemoryStream();
            INodePacketTranslator writetranslator = NodePacketTranslator.GetWriteTranslator(stream);

            interner.Translate(writetranslator);

            INodePacketTranslator readtranslator = NodePacketTranslator.GetReadTranslator(stream, null);
            var newInterner = new LookasideStringInterner(readtranslator);
            Assert.AreEqual(newInterner.GetString(nullIndex), null);
            Assert.AreEqual(newInterner.GetString(emptyIndex), String.Empty);
            Assert.AreEqual(newInterner.GetString(strIndex), "abc123def456");
        }

        [TestMethod]        
        public void ReuseOfDeserializedInternerNotAllowed()
        {
            var interner = new LookasideStringInterner(StringComparer.OrdinalIgnoreCase, 1);
            int strIndex = interner.Intern("abc123def456");

            MemoryStream stream = new MemoryStream();
            INodePacketTranslator writetranslator = NodePacketTranslator.GetWriteTranslator(stream);

            interner.Translate(writetranslator);

            INodePacketTranslator readtranslator = NodePacketTranslator.GetReadTranslator(stream, null);
            var newInterner = new LookasideStringInterner(readtranslator);

            bool gotException = false;
            try
            {
                newInterner.Intern("foo");
            }
            catch (Exception)
            {
                gotException = true;
            }

            Assert.IsTrue(gotException);
        }

        [TestMethod]
        public void ComparerIsObeyed()
        {
            var interner = new LookasideStringInterner(StringComparer.OrdinalIgnoreCase, 1);
            int strIndex = interner.Intern("abc123def456");
            Assert.AreEqual(interner.Intern("ABC123DEF456"), strIndex);

            var interner2 = new LookasideStringInterner(StringComparer.Ordinal, 1);
            int strIndex2 = interner2.Intern("abc123def456");
            Assert.AreNotEqual(interner.Intern("ABC123DEF456"), strIndex2);
        }

    }

}
