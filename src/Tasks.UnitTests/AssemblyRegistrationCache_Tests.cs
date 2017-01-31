// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Tasks;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    sealed public class AssemblyRegistrationCache_Tests
    {
        [Fact]
        public void ExerciseCache()
        {
            AssemblyRegistrationCache arc = new AssemblyRegistrationCache();

            Assert.Equal(0, arc.Count);

            arc.AddEntry("foo", "bar");

            Assert.Equal(1, arc.Count);

            string assembly;
            string tlb;
            arc.GetEntry(0, out assembly, out tlb);

            Assert.Equal("foo", assembly);
            Assert.Equal("bar", tlb);
        }
    }
}
