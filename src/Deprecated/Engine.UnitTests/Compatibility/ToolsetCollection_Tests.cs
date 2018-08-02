// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;

using NUnit.Framework;

using Microsoft.Build;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;

namespace Microsoft.Build.UnitTests.OM.OrcasCompatibility
{
    /// <summary>
    /// Fixture Class for the v9 OM Public Interface Compatibility Tests. ToolsetCollection Class.
    /// Also see Toolset tests in the Project test class.
    /// </summary>
    [TestFixture]
    public class ToolsetCollection_Tests
    {
        /// <summary>
        ///  Add Test, simple add
        /// </summary>
        [Test]
        public void Add()
        {
            Engine e = new Engine();

            int defaultToolsetCount = e.Toolsets.Count;
            e.Toolsets.Add(new Toolset("version", @"c:\path"));
            Assertion.AssertEquals(defaultToolsetCount + 1, e.Toolsets.Count);
        }

        /// <summary>
        ///  Add Test, simple add assert overwrite
        /// </summary>
        [Test]
        public void AddTwiceOverWrite()
        {
            Engine e = new Engine();

            int defaultToolsetCount = e.Toolsets.Count;
            e.Toolsets.Add(new Toolset("version", @"c:\path"));
            e.Toolsets.Add(new Toolset("version", @"c:\path"));
            Assertion.AssertEquals(defaultToolsetCount + 1, e.Toolsets.Count);
        }

        /// <summary>
        ///  Add Test, add a null toolset
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void AddNullToolset()
        {
            Engine e = new Engine();
            Toolset toolset = null;
            e.Toolsets.Add(toolset);
        }

        /// <summary>
        /// Clear Test, this is a not supported method
        /// </summary>
        [Test]
        [ExpectedException(typeof(NotSupportedException))]
        public void Clear()
        {
            Engine e = new Engine();
            e.Toolsets.Clear();
        }

        /// <summary>
        /// ToolsetCollection Test, Indexing simple
        /// </summary>
        [Test]
        public void ToolsetIndex()
        {
            Engine e = new Engine();
            e.Toolsets.Add(new Toolset("version", @"c:\path"));
            Assertion.AssertEquals("version", e.Toolsets["version"].ToolsVersion);
        }

        /// <summary>
        /// ToolsetCollection Test, Indexing where index is null
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ToolsetIndex_Null()
        {
            Engine e = new Engine();
            string toolsVersion = e.Toolsets[null].ToolsVersion;
        }

        /// <summary>
        /// ToolsetCollection Test, Indexing where index does not exist
        /// </summary>
        [Test]
        [ExpectedException(typeof(NullReferenceException))]
        public void ToolsetIndex_NotFound()
        {
            Engine e = new Engine();
            string toolsVersion = e.Toolsets["NotFound"].ToolsVersion;
        }

        /// <summary>
        /// ToolsetCollection Test, Indexing Empty string
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void ToolsetIndex_EmptyString()
        {
            Engine e = new Engine();
            string toolsVersion = e.Toolsets[String.Empty].ToolsVersion;
        }

        /// <summary>
        /// ToolsetCollection Test, returned object by index is clone of origional.
        /// </summary>
        [Test]
        public void ToolsetIndex_AssertClonedReturn()
        {
            Engine e = new Engine();
            Toolset toolSet = new Toolset("version", @"c:\path");
            e.Toolsets.Add(toolSet);
            Toolset toolset2 = e.Toolsets["version"];
            Assertion.AssertEquals(false, object.ReferenceEquals(toolSet, toolset2));
        }

        /// <summary>
        /// Count Test. Increments on add
        /// </summary>
        /// <remarks> We cannot test decrements because Clear() and Remove() are not supported</remarks>
        [Test]
        public void CountIncrement()
        {
            Engine e = new Engine();
            Toolset toolSet = new Toolset("version", @"c:\path");
            int defaultCount = e.Toolsets.Count;
            e.Toolsets.Add(toolSet);
            Assertion.AssertEquals(defaultCount + 1, e.Toolsets.Count);
        }

        /// <summary>
        /// Remove Test. Assert method not supported
        /// </summary>
        [Test]
        [ExpectedException(typeof(NotSupportedException))]
        public void Remove()
        {
            Engine e = new Engine();
            Toolset toolSet = new Toolset("version", @"c:\path");
            e.Toolsets.Add(toolSet);
            e.Toolsets.Remove(toolSet);
        }

        /// <summary>
        /// IsReadOnly test. Always returns false.
        /// </summary>
        [Test]
        public void IsReadOnly()
        {
            Engine e = new Engine();
            Toolset toolSet = new Toolset("version", @"c:\path");
            Assertion.AssertEquals(false, e.Toolsets.IsReadOnly);
        }

        /// <summary>
        /// Contains Test, by object that is in collection
        /// </summary>
        [Test]
        public void ContainsObject_found()
        {
            Engine e = new Engine();
            Toolset toolset1 = new Toolset("v1", @"c:\path");
            Toolset toolset2 = new Toolset("v2", @"c:\path");
            e.Toolsets.Add(toolset1);
            e.Toolsets.Add(toolset2);
            
            Assertion.AssertEquals(true, e.Toolsets.Contains(toolset1));
        }

        /// <summary>
        /// Contains Test, by object that is not in collection
        /// </summary>
        [Test]
        public void ContainsObject_notFound()
        {
            Engine e = new Engine();
            Toolset toolSetToAdd = new Toolset("v1", @"c:\path");
            Toolset toolSetNotAdded = new Toolset("v2", @"c:\path");
            e.Toolsets.Add(toolSetToAdd);
            Assertion.AssertEquals(false, e.Toolsets.Contains(toolSetNotAdded));
        }

        /// <summary>
        /// Contains Test, by object. 
        /// </summary>
        [Test]
        public void ContainsToolsVersion_found()
        {
            Engine e = new Engine();
            Toolset toolset1 = new Toolset("v1", @"c:\path");
            Toolset toolset2 = new Toolset("v2", @"c:\path");
            e.Toolsets.Add(toolset1);
            e.Toolsets.Add(toolset2);
            Assertion.AssertEquals(true, e.Toolsets.Contains("v1"));
        }

        /// <summary>
        /// Contains Test, by object. 
        /// </summary>
        [Test]
        public void ContainsToolsVersion_notFound()
        {
            Engine e = new Engine();
            Toolset toolset1 = new Toolset("v1", @"c:\path");
            Toolset toolset2 = new Toolset("v2", @"c:\path");
            e.Toolsets.Add(toolset1);
            e.Toolsets.Add(toolset2);
            Assertion.AssertEquals(false, e.Toolsets.Contains("notthere"));
        }

        /// <summary>
        /// Contains Test, by object. 
        /// </summary>
        [Test]
        public void ContainsToolsVersion_escapedVersions()
        {
            Engine e = new Engine();
            string escaped = @"%25%2a%3f%40%24%28%29%3b\";
            string unescaped = @"%*?@$();\";
            Toolset toolSetEscaped = new Toolset(escaped, @"c:\path");
            Toolset toolSetUnescaped = new Toolset(unescaped, @"c:\path");
            e.Toolsets.Add(toolSetEscaped);
            e.Toolsets.Add(toolSetUnescaped);
            Assertion.AssertEquals(true, e.Toolsets.Contains(escaped));
            Assertion.AssertEquals(true, e.Toolsets.Contains(unescaped));
        }

        /// <summary>
        /// CopyTo Test, copy into array at index zero
        /// </summary>
        [Test]
        public void CopyToTest_IndexZero()
        {
            Engine e = new Engine();
            Toolset toolset1 = new Toolset("v1", @"c:\path");
            Toolset toolset2 = new Toolset("v2", @"c:\path");
            e.Toolsets.Add(toolset1);
            e.Toolsets.Add(toolset2);
            Toolset[] toolsetArray = new Toolset[e.Toolsets.Count]; 
            e.Toolsets.CopyTo(toolsetArray, 0);
            Assertion.AssertEquals(e.Toolsets.Count, toolsetArray.Length);
            Assertion.AssertEquals(true, 0 < Array.IndexOf(toolsetArray, toolset1));
            Assertion.AssertEquals(true, 0 < Array.IndexOf(toolsetArray, toolset2));
            Assertion.AssertEquals(true, object.ReferenceEquals(toolsetArray[Array.IndexOf(toolsetArray, toolset2)], toolset2));
        }

        /// <summary>
        /// CopyTo Test, copy into array at offset Index
        /// </summary>
        [Test]
        public void CopyToTest_OffsetIndex()
        {
            const int OffsetIndex = 2;
            Engine e = new Engine();
            Toolset toolset1 = new Toolset("v1", @"c:\path");
            Toolset toolset2 = new Toolset("v2", @"c:\path");
            e.Toolsets.Add(toolset1);
            Toolset[] toolsetArray = new Toolset[e.Toolsets.Count + OffsetIndex];
            e.Toolsets.CopyTo(toolsetArray, OffsetIndex);
            Assertion.AssertEquals(e.Toolsets.Count, toolsetArray.Length - OffsetIndex);
            Assertion.AssertNull(toolsetArray[OffsetIndex - 1]);
            Assertion.AssertEquals(true, 0 < Array.IndexOf(toolsetArray, toolset1));
        }

        /// <summary>
        /// CopyTo Test, copy into array that is initialized too small to contain all toolsets, 
        /// at index zero
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void CopyToTest_ArrayTooSmall()
        {
            Engine e = new Engine();
            Toolset toolset1 = new Toolset("v1", @"c:\path");
            e.Toolsets.Add(toolset1);
            e.Toolsets.CopyTo(new Toolset[e.Toolsets.Count - 1], 0);  
        }

        /// <summary>
        /// Enumeration Test, manual iteration over ToolsetCollection using GetEnumerator();
        /// </summary>
        [Test]
        public void GetEnumerator()
        {
            Engine e = new Engine();
            Toolset toolset1 = new Toolset("v1", @"c:\path");
            Toolset[] toolsetArray = new Toolset[e.Toolsets.Count];
            e.Toolsets.CopyTo(toolsetArray, 0);
            IEnumerator<Toolset> toolsetEnum = e.Toolsets.GetEnumerator();
            int enumerationCounter = 0;
            while (toolsetEnum.MoveNext())
            {
                Assertion.AssertEquals(true, object.ReferenceEquals(toolsetArray[enumerationCounter], toolsetEnum.Current));
                Assertion.AssertEquals(toolsetArray[enumerationCounter].ToolsVersion, toolsetEnum.Current.ToolsVersion);
                enumerationCounter++;
            }
        }

        /// <summary>
        /// ToolsVersions Test, get all ToolsVersions through ToolsVersions enumerable interface.
        /// </summary>
        [Test]
        public void ToolsVersions()
        {
            Engine e = new Engine();
            Toolset toolset1 = new Toolset("v1", @"c:\path");
            IEnumerable<string> toolsVersions = e.Toolsets.ToolsVersions;
            Toolset[] toolsetArray = new Toolset[e.Toolsets.Count];
            e.Toolsets.CopyTo(toolsetArray, 0);
            int counter = 0;
            foreach (string toolsVersion in toolsVersions)
            {
                Assertion.AssertEquals(toolsetArray[counter].ToolsVersion, toolsVersion);
                counter++;
            }

            Assertion.AssertEquals(toolsetArray.Length, counter);
        }
    }
}
