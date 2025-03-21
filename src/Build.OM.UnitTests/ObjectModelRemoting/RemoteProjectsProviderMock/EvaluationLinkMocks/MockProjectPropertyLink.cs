// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{
    using Microsoft.Build.Construction;
    using Microsoft.Build.Evaluation;
    using Microsoft.Build.ObjectModelRemoting;

    internal sealed class MockProjectPropertyLinkRemoter : MockLinkRemoter<ProjectProperty>
    {
        public override ProjectProperty CreateLinkedObject(IImportHolder holder)
        {
            var link = new MockProjectPropertyLink(this, holder);
            return holder.Linker.LinkFactory.Create(link);
        }

        // ProjectPropertyLink remoting
        public MockProjectLinkRemoter Project => OwningCollection.Export<Project, MockProjectLinkRemoter>(Source.Project);
        public MockProjectPropertyElementLinkRemoter Xml => (MockProjectPropertyElementLinkRemoter)ExportElement(Source.Xml);
        public string Name => Source.Name;
        public string EvaluatedIncludeEscaped => ProjectPropertyLink.GetEvaluatedValueEscaped(Source);
        public string UnevaluatedValue { get => Source.UnevaluatedValue; set => Source.UnevaluatedValue = value; }
        public bool IsEnvironmentProperty => Source.IsEnvironmentProperty;
        public bool IsGlobalProperty => Source.IsGlobalProperty;
        public bool IsReservedProperty => Source.IsReservedProperty;
        public MockProjectPropertyLinkRemoter Predecessor => OwningCollection.Export<ProjectProperty, MockProjectPropertyLinkRemoter>(Source.Predecessor);
        public bool IsImported => Source.IsImported;
    }

    internal sealed class MockProjectPropertyLink : ProjectPropertyLink, ILinkMock
    {
        public MockProjectPropertyLink(MockProjectPropertyLinkRemoter proxy, IImportHolder holder)
        {
            Holder = holder;
            Proxy = proxy;
        }

        public IImportHolder Holder { get; }
        public ProjectCollectionLinker Linker => Holder.Linker;
        public MockProjectPropertyLinkRemoter Proxy { get; }
        object ILinkMock.Remoter => Proxy;

        // ProjectPropertyLink
        public override Project Project => Linker.Import<Project, MockProjectLinkRemoter>(Proxy.Project);
        public override ProjectPropertyElement Xml => (ProjectPropertyElement)Proxy.Xml.Import(Linker);
        public override string Name => Proxy.Name;
        public override string EvaluatedIncludeEscaped => Proxy.EvaluatedIncludeEscaped;
        public override string UnevaluatedValue { get => Proxy.UnevaluatedValue; set => Proxy.UnevaluatedValue = value; }
        public override bool IsEnvironmentProperty => Proxy.IsEnvironmentProperty;
        public override bool IsGlobalProperty => Proxy.IsGlobalProperty;
        public override bool IsReservedProperty => Proxy.IsReservedProperty;
        public override ProjectProperty Predecessor => Linker.Import<ProjectProperty, MockProjectPropertyLinkRemoter>(Proxy.Predecessor);
        public override bool IsImported => Proxy.IsImported;
    }
}
