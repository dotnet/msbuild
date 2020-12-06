// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{
    using Microsoft.Build.Construction;
    using Microsoft.Build.Evaluation;
    using Microsoft.Build.ObjectModelRemoting;

    internal class MockProjectPropertyLinkRemoter : MockLinkRemoter<ProjectProperty>
    {
        public override ProjectProperty CreateLinkedObject(IImportHolder holder)
        {
            var link = new MockProjectPropertyLink(this, holder);
            return holder.Linker.LinkFactory.Create(link);
        }


        ///  ProjectPropertyLink remoting
        public MockProjectLinkRemoter Project => this.OwningCollection.Export<Project, MockProjectLinkRemoter>(this.Source.Project);
        public MockProjectPropertyElementLinkRemoter Xml => (MockProjectPropertyElementLinkRemoter)this.ExportElement(this.Source.Xml);
        public string Name => this.Source.Name;
        public string EvaluatedIncludeEscaped => ProjectPropertyLink.GetEvaluatedValueEscaped(this.Source);
        public string UnevaluatedValue { get => this.Source.UnevaluatedValue; set=> this.Source.UnevaluatedValue = value; }
        public bool IsEnvironmentProperty => this.Source.IsEnvironmentProperty;
        public bool IsGlobalProperty => this.Source.IsGlobalProperty;
        public bool IsReservedProperty => this.Source.IsReservedProperty;
        public MockProjectPropertyLinkRemoter Predecessor => this.OwningCollection.Export<ProjectProperty, MockProjectPropertyLinkRemoter>(this.Source.Predecessor);
        public bool IsImported => this.Source.IsImported;
    }

    internal class MockProjectPropertyLink : ProjectPropertyLink, ILinkMock
    {
        public MockProjectPropertyLink(MockProjectPropertyLinkRemoter proxy, IImportHolder holder)
        {
            this.Holder = holder;
            this.Proxy = proxy;
        }

        public IImportHolder Holder { get; }
        public ProjectCollectionLinker Linker => this.Holder.Linker;
        public MockProjectPropertyLinkRemoter Proxy { get; }
        object ILinkMock.Remoter => this.Proxy;

        // ProjectPropertyLink
        public override Project Project => this.Linker.Import<Project, MockProjectLinkRemoter>(this.Proxy.Project);
        public override ProjectPropertyElement Xml => (ProjectPropertyElement)this.Proxy.Xml.Import(this.Linker);
        public override string Name => this.Proxy.Name;
        public override string EvaluatedIncludeEscaped => this.Proxy.EvaluatedIncludeEscaped;
        public override string UnevaluatedValue { get => this.Proxy.UnevaluatedValue; set => this.Proxy.UnevaluatedValue = value; }
        public override bool IsEnvironmentProperty => this.Proxy.IsEnvironmentProperty;
        public override bool IsGlobalProperty => this.Proxy.IsGlobalProperty;
        public override bool IsReservedProperty => this.Proxy.IsReservedProperty;
        public override ProjectProperty Predecessor => this.Linker.Import<ProjectProperty, MockProjectPropertyLinkRemoter>(this.Proxy.Predecessor);
        public override bool IsImported => this.Proxy.IsImported;
    }
}
