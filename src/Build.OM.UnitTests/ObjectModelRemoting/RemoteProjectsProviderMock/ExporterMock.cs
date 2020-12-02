// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Xml;
    using Microsoft.Build.Construction;
    using Microsoft.Build.Evaluation;
    using Microsoft.Build.ObjectModelRemoting;
    using Xunit;
    using ExportedLinksMap = LinkedObjectsMap<object>;
    using ImportedLinksMap = LinkedObjectsMap<System.UInt32>;

    /**************************************************************************************
    For the ExternalProjectsProvider mock infrastructure we'll try to use very similar model as in the actual implementation in VS.

    Typical flow for "linked object" of type "Foo"
    [ ---  Client Collection                                    ]                           [ Server collection (can be different process) ] 
    (Foo) localView <=> (FooLink) link <=> FooLinkRemoter (Proxy) <=~connection mechanism~=> FooLinkRemoter(stub) <=> (Real object)
    
    FooLinkRemoter would be whatever ExternalProviders see useful to provide FooLink implementation and is compatable with connection mechanism
    it might be completely different interface since some link types would be either inefficient or impossible to serialize for example and pass cross process.
    
    Here we can cheat a little bit, since we run both Client and Server collection in the same process so we can ignore connection mechanism (typically some
    form of serialization/deserialization) and just give the "client" link implementation the same Remoter object we create on the "server"

    So to mock the infrastructure, we will use the pattern bellow.
    - XX - An MSBuild OM object we need to remote
    - YY, ZZ another MSBuild OM objects used to represent "complex" case where we have method/property on the link interface with outputs or inputs that are MSBuild object types.

    so let say XX object is like this:
        public XX
        {
            string Simple(string input)
            YY Complex(ZZ input)
        }

    typically we have a XXLink interface in ObjectModel remoting that is similar (usually a minimal subset) to XX
        public abstract XXLink
        {
            public abstract string Simple(string input);
            public abstract YY Complex(ZZ input);
        }

    And the new LinkedObjectsFactory would allow us to create a view of a instance of XX if we have XXLink for that object.

    to support that we write this classes:

    ("remoter")
        class MockXXRemoter : MockLinkRemoter<XX, XXLink>
        {
            ProjectCollectionLinker Server { get; }    // the Server collection linking supporting class.
            XX RealObject { get; }                     // the XX object instance in "Server collection"

            // this creates a "local view" for "RealObject" inside "local" collection.
            public override XX CreateView(ProjectCollectionLinker local)
            {
                return Server.LinkFactory.Create(new MockXXLink(local, this));
            }

            // remoter implementation
            string Simple(sting input) { return RealObject.Simple(input); }
            public MockYYRemoter Complex(MockZZRemoter input) { Server.Export<MockYYRemoter>(RealObject.Complex(Server.Import(input)); }
        }

     ("link")
        class MockXXLink : XXLink, ILinkMock
        {
            ProjectCollectionLinker Client { get; } // the client collection linking support class.
            MockXXRemoter Proxy { get; }            // the Remoter proxy (technically the actual remoter object in our case).

            // XXLink implementation
            public override string Simple(sting input) { return Proxy.Simple(input); }
            public override YY Complex(ZZ input) { return Client.Import(Proxy.Complex(Client.Export<MockZZRemoter>(input)); }
        }

    Object lifetime management:
    We'll have
    "View" (strong ref) -> "Link" -> (strong ref) -> "Remoter" -> (strong ref) -> "real object"

    "exported" table holds "weak ref" to "remoter" (keyed on "real object")
    "imported" table holds "weak ref" to "view"    (keyed on "real object").

    the purpose of these two is to ensure there is only one "view" instance for any "real object" in client collection.
    Functionally that is not needed, but there is some MSBuild and usage patterns that do use "ReferenceEqual" and we want to not break these.

    **************************************************************************************/

    internal interface IRemoterSource
    {
        object RealObject { get; }
    }

    /// <summary>
    /// Base remoter object implementation.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal abstract class MockLinkRemoter<T> : ExportedLinksMap.LinkedObject<T> , IRemoterSource
        where T : class
    {
        object IRemoterSource.RealObject => this.Source;

        public ProjectCollectionLinker OwningCollection { get; private set; }

        public MockProjectElementLinkRemoter ExportElement(ProjectElement obj)
            => this.OwningCollection.ExportElement(obj);

        public UInt32 HostCollectionId => this.OwningCollection.CollectionId;

        public override void Initialize(object key, T source, object context)
        {
            base.Initialize(key, source, context);
            this.OwningCollection = (ProjectCollectionLinker)context;
        }

        public abstract T CreateLinkedObject(IImportHolder holder);
    }

    /// <summary>
    /// used by ProjectCollectionLinker when exporting objects
    /// to prevent "double - remoting"
    /// all MockFOOLink objects will implement it.
    /// </summary>
    internal interface ILinkMock
    {
        ProjectCollectionLinker Linker { get; }

        object Remoter { get; }
    }

    internal interface IImportHolder
    {
        ProjectCollectionLinker Linker { get; }
        UInt32 LocalId { get; }
    }

    /// <summary>
    /// Provide ability to export and import OM objects to another collections.
    /// </summary>
    internal class ProjectCollectionLinker : ExternalProjectsProvider
    {
        internal static int _collecitonId = 0;

        private bool importing = false;
        private ExportedLinksMap exported = ExportedLinksMap.Create();
        private Dictionary<UInt32, ExternalConnection> imported = new Dictionary<UInt32, ExternalConnection>();

        private ProjectCollectionLinker(ConnectedProjectCollections group)
        {
            this.LinkedCollections = group;
            this.CollectionId = (UInt32) Interlocked.Increment(ref _collecitonId);
            this.Collection = new ProjectCollection();
            this.LinkFactory = LinkedObjectsFactory.Get(this.Collection);
        }

        public Project LoadProject(string path) =>  this.Collection.LoadProject(path);
        public Project LoadProjectIgnoreMissingImports(string path) => LoadProjectWithSettings(path, ProjectLoadSettings.IgnoreMissingImports);
        public Project LoadProjectWithSettings(string path, ProjectLoadSettings settings) => new Project(path, null, null, this.Collection, settings);


        public Project LoadInMemoryWithSettings(string content, ProjectLoadSettings settings = ProjectLoadSettings.Default)
        {
            content = ObjectModelHelpers.CleanupFileContents(content);
            ProjectRootElement xml = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            Project project = new Project(xml, null, null, this.Collection, settings);
            return project;
        }

        public ConnectedProjectCollections LinkedCollections { get; }

        public UInt32 CollectionId { get; }

        public ProjectCollection Collection { get; }

        public LinkedObjectsFactory LinkFactory { get; }

        public bool Importing
        {
            get => this.importing;
            set
            {
                if (value != this.importing)
                {
                    ExternalProjectsProvider.SetExternalProjectsProvider(this.Collection, value ? this : null);
                    this.importing = value;
                }
            }
        }

        private void ConnectTo (ProjectCollectionLinker other)
        {
            if (other.CollectionId == this.CollectionId)
            {
                throw new Exception("Can not connect to self");
            }

            lock (imported)
            {
                if (imported.ContainsKey(other.CollectionId))
                {
                    return;
                }

                // clone to be atomic.
                // we don't have to be efficient here on "Connect". There are very few calls.
                // compared to potentially 1000's of accesses (so it is better to copy that to lock access)
                Dictionary<UInt32, ExternalConnection> newMap = new Dictionary<uint, ExternalConnection>(imported);
                newMap.Add(other.CollectionId, new ExternalConnection(other));
                imported = newMap;
            }
        }

        private static bool dbgValidateDuplicateViews = false;


        internal  void ValidateNoDuplicates()
        {
            foreach (var r in imported)
            {
                lock (r.Value.ActiveImports.GetLockForDebug)
                {
                    ValidateNoDuplicates(r.Value.ActiveImports);
                }
            }
        }

        private void ValidateNoDuplicates(ImportedLinksMap map)
        {
            HashSet<object> views = new HashSet<object>();
            HashSet<object> links = new HashSet<object>();
            HashSet<object> remoters = new HashSet<object>();
            foreach (var ai in map.GetActiveLinks())
            {
                var dbg = ai as IActiveImportDBG;
                Assert.NotNull(dbg);
                var view = dbg.Linked;
                Assert.NotNull(view);
                var link = LinkedObjectsFactory.GetLink(view) as ILinkMock;
                Assert.NotNull(link);
                var remoter = link.Remoter;
                Assert.NotNull(remoter);

                if (views.Contains(view))
                {
                    Assert.DoesNotContain(view, views);
                }

                if (links.Contains(link))
                {
                    Assert.DoesNotContain(link, links);
                }

                if (remoters.Contains(remoter))
                {
                    Assert.DoesNotContain(remoter, remoters);
                }
                views.Add(view);
                links.Add(link);
                remoters.Add(remoter);
            }
        }

        public T Import<T, RMock>(RMock remoter)
            where T : class
            where RMock : MockLinkRemoter<T>, new()
        {
            if (remoter == null)
            {
                return null;
            }

            if (remoter.HostCollectionId == this.CollectionId)
            {
                this.exported.GetActive(remoter.LocalId, out T result);
                return result;
            }

            if (!imported.TryGetValue(remoter.HostCollectionId, out var perRemoteCollection))
            {
                throw new Exception("Not connected!");
            }

            ActiveImport<T, RMock> proxy;
            if (!dbgValidateDuplicateViews)
            {
                perRemoteCollection.ActiveImports.GetOrCreate(remoter.LocalId, remoter, this, out proxy, slow: true);
            }
            else
            {
                lock (perRemoteCollection.ActiveImports.GetLockForDebug)
                {
                    ValidateNoDuplicates(perRemoteCollection.ActiveImports);
                    perRemoteCollection.ActiveImports.GetOrCreate(remoter.LocalId, remoter, this, out proxy, slow: true);
                    ValidateNoDuplicates(perRemoteCollection.ActiveImports);
                }
            }


            return proxy.Linked;
        }

        public RMock Export<T, RMock>(T obj)
            where T : class
            where RMock : MockLinkRemoter<T>, new()
        {
            Export(obj, out RMock result);
            return result;
        }

        public void Export<T, RMock>(T obj, out RMock remoter)
            where T : class
            where RMock : MockLinkRemoter<T>, new()
        {
            if (obj == null)
            {
                remoter = null;
                return;
            }

            var external = LinkedObjectsFactory.GetLink(obj);

            if (external != null)
            {
                var proxy = (ILinkMock)external;

                remoter = (RMock) proxy.Remoter;
                return;
            }

            exported.GetOrCreate(obj, obj, this, out remoter);
        }

        // ExternalProjectsProvider
        public override ICollection<Project> GetLoadedProjects(string filePath)
        {
            List<Project> result = new List<Project>();

            foreach (var external in this.imported.Values)
            {
                foreach (var remote in external.Linker.ExportLoadedProjects(filePath))
                {
                    result.Add(this.Import<Project, MockProjectLinkRemoter>(remote));
                }
            }

            return result;
        }

        private IReadOnlyCollection<MockProjectLinkRemoter> ExportLoadedProjects(string filePath)
        {
            List<MockProjectLinkRemoter> remoted = new List<MockProjectLinkRemoter>();
            var toRemote = LinkedObjectsFactory.GetLocalProjects(this.Collection, filePath);

            foreach (var p in toRemote)
            {
                remoted.Add(this.Export<Project, MockProjectLinkRemoter>(p));
            }

            return remoted;
        }


        private interface IActiveImportDBG
        {
            object Linked { get; }
        }

        private class ActiveImport<T, RMock> : ImportedLinksMap.LinkedObject<RMock>, IImportHolder, IActiveImportDBG
            where T : class
            where RMock : MockLinkRemoter<T>
        {
            public override void Initialize(uint key, RMock source, object context)
            {
                base.Initialize(key, source, context);

                this.Remoter = source;
                this.Linker = (ProjectCollectionLinker)context;
                this.Linked = source.CreateLinkedObject(this);
            }

            object IActiveImportDBG.Linked => this.Linked;

            public ProjectCollectionLinker Linker { get; private set; }

            public T Linked { get; protected set; }
            public RMock Remoter { get; protected set; }
        }


        public static ConnectedProjectCollections CreateGroup()
        {
            return new ConnectedProjectCollections();
        }

        internal class ConnectedProjectCollections
        {
            private List<ProjectCollectionLinker> group = new List<ProjectCollectionLinker>();
            public ProjectCollectionLinker AddNew()
            {
                var linker = new ProjectCollectionLinker(this);
                lock (group)
                {
                    foreach (var l in group)
                    {
                        linker.ConnectTo(l);
                        l.ConnectTo(linker);
                    }
                    var updatedGroup = new List<ProjectCollectionLinker>(group);
                    updatedGroup.Add(linker);
                    group = updatedGroup;
                }

                return linker;
            }

            public void ClearAllRemotes()
            {
                lock (group)
                {
                    foreach (var l in group)
                    {
                        l.ClearAllRemotes();
                    }
                }
            }
        }

        private void ClearAllRemotes()
        {
            this.exported = ExportedLinksMap.Create();
            foreach (var i in imported)
            {
                i.Value.Clear();
            }
        }


        private class ExternalConnection
        {
            public ExternalConnection(ProjectCollectionLinker linker)
            {
                this.Linker = linker;
                this.ActiveImports = ImportedLinksMap.Create();
            }
            public ProjectCollectionLinker Linker { get; }
            public ImportedLinksMap ActiveImports { get; private set; }

            public void Clear()
            {
                this.ActiveImports = ImportedLinksMap.Create();
            }
        }
    }
}
