// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{
    using System;
    using System.Linq;
    using Xunit;

    public class LinkedProjectCollection_Tests : IClassFixture<LinkedProjectCollection_Tests.MyTestCollectionGroup>
    {
        public class MyTestCollectionGroup : TestCollectionGroup
        {
            public MyTestCollectionGroup() : base(2, 4) { }
        }

        public TestCollectionGroup StdGroup { get; }
        public LinkedProjectCollection_Tests(MyTestCollectionGroup group)
        {
            this.StdGroup = group;
            group.Clear();
        }

        [Fact]
        public void EnumerationBasic()
        {
            var pcLocal = this.StdGroup.Local;
            var pcRemote = this.StdGroup.Remote[0];

            var proj1Path = this.StdGroup.StdProjectFiles[0];
            var proj2Path = this.StdGroup.StdProjectFiles[1]; 

            var proj1 = pcLocal.LoadProject(proj1Path);
            var proj2 = pcRemote.LoadProject(proj2Path);

            ViewValidation.VerifyNotLinkedNotNull(proj1);
            ViewValidation.VerifyNotLinkedNotNull(proj2);

            var loadedLocal = pcLocal.Collection.LoadedProjects;
            var loadedRemote = pcRemote.Collection.LoadedProjects;

            Assert.Equal(1, loadedLocal.Count);
            Assert.Equal(1, loadedRemote.Count);
            Assert.Same(proj1, loadedLocal.FirstOrDefault());
            Assert.Same(proj2, loadedRemote.FirstOrDefault());

            pcLocal.Importing = true;

            loadedLocal = pcLocal.Collection.LoadedProjects;
            Assert.Equal(2, loadedLocal.Count);

            // still can find it's own projects and it is the same object
            var localProj = pcLocal.Collection.GetLoadedProjects(proj1Path).FirstOrDefault();
            Assert.Same(localProj, proj1);

            var remoteProj = pcLocal.Collection.GetLoadedProjects(proj2Path).FirstOrDefault();
            Assert.NotSame(remoteProj, proj2);
            ViewValidation.VerifyLinkedNotNull(remoteProj);
        }

        [Fact]
        public void EnumerationMultiple()
        {
            var pcLocal = this.StdGroup.Local;
            var pcRemote0 = this.StdGroup.Remote[0];
            var pcRemote1 = this.StdGroup.Remote[1];

            var proj0Path = this.StdGroup.StdProjectFiles[0];
            var proj1Path = this.StdGroup.StdProjectFiles[1];
            var proj2Path = this.StdGroup.StdProjectFiles[2];
            var proj3Path = this.StdGroup.StdProjectFiles[3];

            var proj0local = pcLocal.LoadProject(proj0Path);
            var proj1local = pcLocal.LoadProject(proj1Path);

            var proj1remote0 = pcRemote0.LoadProject(proj1Path);
            var proj2remote0 = pcRemote0.LoadProject(proj2Path);

            var proj2remote1 = pcRemote1.LoadProject(proj2Path);
            var proj3remote1 = pcRemote1.LoadProject(proj3Path);

            var loadedLocal = pcLocal.Collection.LoadedProjects;
            var loadedRemote0 = pcRemote0.Collection.LoadedProjects;
            var loadedRemote1 = pcRemote1.Collection.LoadedProjects;

            Assert.Equal(2, loadedLocal.Count);
            Assert.Equal(2, loadedRemote0.Count);
            Assert.Equal(2, loadedRemote1.Count);

            pcLocal.Importing = true;

            var loadedWithExternal = pcLocal.Collection.LoadedProjects;
            Assert.Equal(6, loadedWithExternal.Count);

            var prj0Coll = pcLocal.Collection.GetLoadedProjects(proj0Path);
            Assert.Equal(1, prj0Coll.Count);
            Assert.Same(proj0local, prj0Coll.First());

            var prj1Coll = pcLocal.Collection.GetLoadedProjects(proj1Path);
            Assert.Equal(2, prj1Coll.Count);
            Assert.True(prj1Coll.Contains(proj1local));
            Assert.False(prj1Coll.Contains(proj1remote0));

            var prj2Coll = pcLocal.Collection.GetLoadedProjects(proj2Path);
            Assert.Equal(2, prj2Coll.Count);
            Assert.False(prj2Coll.Contains(proj2remote0));
            Assert.False(prj2Coll.Contains(proj2remote1));
            foreach(var p in prj2Coll)
            {
                ViewValidation.VerifyLinkedNotNull(p);
            }

            var prj3Coll = pcLocal.Collection.GetLoadedProjects(proj3Path);
            Assert.Equal(1, prj3Coll.Count);
            Assert.False(prj2Coll.Contains(proj3remote1));
            ViewValidation.VerifyLinkedNotNull(prj3Coll.FirstOrDefault());
        }

        [Fact]
        public void DynamicEnumeration()
        {
            var pcLocal = this.StdGroup.Local;
            var pcRemote = this.StdGroup.Remote[0];
            pcLocal.Importing = true;

            var proj0Path = this.StdGroup.StdProjectFiles[0];
            var proj1Path = this.StdGroup.StdProjectFiles[1];
            var proj2Path = this.StdGroup.StdProjectFiles[2];

            Assert.Equal(0, pcLocal.Collection.LoadedProjects.Count);
            Assert.Equal(0, pcRemote.Collection.LoadedProjects.Count);

            pcLocal.LoadProject(proj0Path);
            Assert.Equal(1, pcLocal.Collection.LoadedProjects.Count);
            Assert.Equal(1, pcLocal.Collection.GetLoadedProjects(proj0Path).Count);

            var proj1 = pcRemote.LoadProject(proj1Path);
            Assert.Equal(2, pcLocal.Collection.LoadedProjects.Count);
            Assert.Equal(1, pcLocal.Collection.GetLoadedProjects(proj1Path).Count);

            pcRemote.LoadProject(proj2Path);
            Assert.Equal(3, pcLocal.Collection.LoadedProjects.Count);
            Assert.Equal(1, pcLocal.Collection.GetLoadedProjects(proj2Path).Count);

            pcRemote.Collection.UnloadProject(proj1);
            Assert.Equal(2, pcLocal.Collection.LoadedProjects.Count);
            Assert.Equal(0, pcLocal.Collection.GetLoadedProjects(proj1Path).Count);
            Assert.Equal(1, pcLocal.Collection.GetLoadedProjects(proj2Path).Count);
        }

        [Fact]
        public void CrossLinked()
        {
            this.StdGroup.Local.Importing = true;
            Array.ForEach(StdGroup.Remote, (r) => r.Importing = true);

            Assert.Equal(0, this.StdGroup.Local.Collection.LoadedProjects.Count);
            Assert.Equal(0, this.StdGroup.Remote[0].Collection.LoadedProjects.Count);
            Assert.Equal(0, this.StdGroup.Remote[1].Collection.LoadedProjects.Count);

            this.StdGroup.Local.LoadProject(this.StdGroup.StdProjectFiles[0]);
            ViewValidation.VerifyProjectCollectionLinks(this.StdGroup.Local.Collection.LoadedProjects, 1, 0);
            ViewValidation.VerifyProjectCollectionLinks(this.StdGroup.Remote[0].Collection.LoadedProjects, 0, 1);
            ViewValidation.VerifyProjectCollectionLinks(this.StdGroup.Remote[1].Collection.LoadedProjects, 0, 1);
            this.StdGroup.Local.VerifyProjectCollectionLinks(this.StdGroup.StdProjectFiles[0], 1, 0);
            this.StdGroup.Remote[0].VerifyProjectCollectionLinks(this.StdGroup.StdProjectFiles[0], 0, 1);
            this.StdGroup.Remote[1].VerifyProjectCollectionLinks(this.StdGroup.StdProjectFiles[0], 0, 1);

            this.StdGroup.Local.LoadProject(this.StdGroup.StdProjectFiles[1]);
            ViewValidation.VerifyProjectCollectionLinks(this.StdGroup.Local.Collection.LoadedProjects, 2, 0);
            ViewValidation.VerifyProjectCollectionLinks(this.StdGroup.Remote[0].Collection.LoadedProjects, 0, 2);
            ViewValidation.VerifyProjectCollectionLinks(this.StdGroup.Remote[1].Collection.LoadedProjects, 0, 2);
            this.StdGroup.Local.VerifyProjectCollectionLinks(this.StdGroup.StdProjectFiles[1], 1, 0);
            this.StdGroup.Remote[0].VerifyProjectCollectionLinks(this.StdGroup.StdProjectFiles[1], 0, 1);
            this.StdGroup.Remote[1].VerifyProjectCollectionLinks(this.StdGroup.StdProjectFiles[1], 0, 1);

            this.StdGroup.Remote[0].LoadProject(this.StdGroup.StdProjectFiles[2]);
            ViewValidation.VerifyProjectCollectionLinks(this.StdGroup.Local.Collection.LoadedProjects, 2, 1);
            ViewValidation.VerifyProjectCollectionLinks(this.StdGroup.Remote[0].Collection.LoadedProjects, 1, 2);
            ViewValidation.VerifyProjectCollectionLinks(this.StdGroup.Remote[1].Collection.LoadedProjects, 0, 3);
            this.StdGroup.Local.VerifyProjectCollectionLinks(this.StdGroup.StdProjectFiles[2], 0, 1);
            this.StdGroup.Remote[0].VerifyProjectCollectionLinks(this.StdGroup.StdProjectFiles[2], 1, 0);
            this.StdGroup.Remote[1].VerifyProjectCollectionLinks(this.StdGroup.StdProjectFiles[2], 0, 1);

            // load proj0 in remote[1] (already loaded in local)
            this.StdGroup.Remote[1].LoadProject(this.StdGroup.StdProjectFiles[0]);
            ViewValidation.VerifyProjectCollectionLinks(this.StdGroup.Local.Collection.LoadedProjects, 2, 2);
            ViewValidation.VerifyProjectCollectionLinks(this.StdGroup.Remote[0].Collection.LoadedProjects, 1, 3);
            ViewValidation.VerifyProjectCollectionLinks(this.StdGroup.Remote[1].Collection.LoadedProjects, 1, 3);
            this.StdGroup.Local.VerifyProjectCollectionLinks(this.StdGroup.StdProjectFiles[0], 1, 1);
            this.StdGroup.Remote[0].VerifyProjectCollectionLinks(this.StdGroup.StdProjectFiles[0], 0, 2);
            this.StdGroup.Remote[1].VerifyProjectCollectionLinks(this.StdGroup.StdProjectFiles[0], 1, 1);

            this.StdGroup.Local.Collection.UnloadAllProjects();

            ViewValidation.VerifyProjectCollectionLinks(this.StdGroup.Local.Collection.LoadedProjects, 0, 2);
            ViewValidation.VerifyProjectCollectionLinks(this.StdGroup.Remote[0].Collection.LoadedProjects, 1, 1);
            ViewValidation.VerifyProjectCollectionLinks(this.StdGroup.Remote[1].Collection.LoadedProjects, 1, 1);
            this.StdGroup.Local.VerifyProjectCollectionLinks(this.StdGroup.StdProjectFiles[0], 0, 1);
            this.StdGroup.Remote[0].VerifyProjectCollectionLinks(this.StdGroup.StdProjectFiles[0], 0, 1);
            this.StdGroup.Remote[1].VerifyProjectCollectionLinks(this.StdGroup.StdProjectFiles[0], 1, 0);

            this.StdGroup.Local.VerifyProjectCollectionLinks(this.StdGroup.StdProjectFiles[1], 0, 0);
            this.StdGroup.Remote[0].VerifyProjectCollectionLinks(this.StdGroup.StdProjectFiles[1], 0, 0);
            this.StdGroup.Remote[1].VerifyProjectCollectionLinks(this.StdGroup.StdProjectFiles[1], 0, 0);

            this.StdGroup.Local.VerifyProjectCollectionLinks(this.StdGroup.StdProjectFiles[2], 0, 1);
            this.StdGroup.Remote[0].VerifyProjectCollectionLinks(this.StdGroup.StdProjectFiles[2], 1, 0);
            this.StdGroup.Remote[1].VerifyProjectCollectionLinks(this.StdGroup.StdProjectFiles[2], 0, 1);
        }
    }
}
