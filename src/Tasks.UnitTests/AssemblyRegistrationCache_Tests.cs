// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Tasks;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public sealed class AssemblyRegistrationCache_Tests
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

        [Fact]
        public void ExerciseCacheSerialization()
        {
            AssemblyRegistrationCache arc = new();
            arc.AddEntry("foo", "bar");
            AssemblyRegistrationCache arc2 = null;
            using (TestEnvironment env = TestEnvironment.Create())
            {
                TransientTestFile file = env.CreateFile();
                arc.SerializeCache(file.Path, null);
                arc2 = StateFileBase.DeserializeCache<AssemblyRegistrationCache>(file.Path, null);
            }

            arc2._assemblies.Count.ShouldBe(arc._assemblies.Count);
            arc2._assemblies[0].ShouldBe(arc._assemblies[0]);
            arc2._typeLibraries.Count.ShouldBe(arc._typeLibraries.Count);
            arc2._typeLibraries[0].ShouldBe(arc._typeLibraries[0]);
        }
    }
}
