// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// These functions are now guarded with a linkDemand and requires the caller to be signed with a 
// ms pkt.  The test harness does not appear to be signed.
#if never
using System;
using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Microsoft.Build.Tasks.Deployment.ManifestUtilities;

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    sealed public class AssemblyIdentity_Tests
    {

        /// <summary>
        /// Convert an AssemblyIdentity to a string.
        /// </summary>
        [TestMethod]
        public void ConvertToString()
        {
            AssemblyIdentity a = new AssemblyIdentity("MyAssembly", "1.0.0.0");

            Assert.AreEqual("MyAssembly, Version=1.0.0.0", a.ToString("D"));
            Assert.AreEqual("MyAssembly_1.0.0.0", a.ToString("N"));
            Assert.AreEqual("MyAssembly, Version=1.0.0.0, Culture=, PublicKeyToken=, ProcessorArchitecture=", a.ToString("P"));
        }

        /// <summary>
        /// Attempt to resolve an assembly.
        /// </summary>
        [TestMethod]
        public void AttemptResolveButFailed()
        {
            AssemblyIdentity a = new AssemblyIdentity("MyAssembly", "1.0.0.0");

            string path = a.Resolve(new string [] {Path.GetTempPath()});
        }
    }
}
#endif


