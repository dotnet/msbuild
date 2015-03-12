// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    sealed public class AssemblyRegistrationCache_Tests
    {
        [TestMethod]
        public void ExerciseCache()
        {
            AssemblyRegistrationCache arc = new AssemblyRegistrationCache();

            Assert.AreEqual(0, arc.Count);

            arc.AddEntry("foo", "bar");

            Assert.AreEqual(1, arc.Count);

            string assembly;
            string tlb;
            arc.GetEntry(0, out assembly, out tlb);

            Assert.AreEqual("foo", assembly);
            Assert.AreEqual("bar", tlb);
        }
    }
}
