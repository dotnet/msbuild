// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{
    using Microsoft.Build.Construction;
    using Microsoft.Build.Evaluation;
    using Microsoft.Build.Framework;


    internal static class DirectlyRemotedClasses
    {
        internal static RemotedResolvedImport Export(this ResolvedImport resolvedImport, ProjectCollectionLinker exporter)
        {
            return new RemotedResolvedImport(resolvedImport, exporter);
        }

        internal static ResolvedImport Import(this RemotedResolvedImport remoted, ProjectCollectionLinker importer)
        {
            return remoted.Import(importer);
        }
    }

    internal class RemotedResolvedImport
    {
        public RemotedResolvedImport(ResolvedImport resolvedImport, ProjectCollectionLinker exporter)
        {
            this.ImportingElement = exporter.Export<ProjectElement, MockProjectImportElementLinkRemoter>(resolvedImport.ImportingElement);
            this.ImportedProject = exporter.Export<ProjectElement, MockProjectRootElementLinkRemoter>(resolvedImport.ImportedProject);
            this.IsImported = resolvedImport.IsImported;
            this.SdkResult = resolvedImport.SdkResult;
        }

        public MockProjectImportElementLinkRemoter ImportingElement { get; }
        public MockProjectRootElementLinkRemoter ImportedProject { get; }

        // this is remotable enough.
        public SdkResult SdkResult { get; }

        public bool IsImported { get; }

        ResolvedImport Import(ProjectCollectionLinker importer)
        {
            var importElement = (ProjectImportElement)importer.Import<ProjectElement, MockProjectImportElementLinkRemoter>(this.ImportingElement);
            var projectElement = (ProjectRootElement)importer.Import<ProjectElement, MockProjectRootElementLinkRemoter>(this.ImportedProject);
            return importer.LinkFactory.Create(importElement, projectElement, 0, this.SdkResult, this.IsImported);
        }
    }
}
