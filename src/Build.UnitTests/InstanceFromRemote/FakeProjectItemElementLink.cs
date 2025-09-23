// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Construction;
using Microsoft.Build.ObjectModelRemoting;

namespace Microsoft.Build.Engine.UnitTests.InstanceFromRemote
{
    /// <summary>
    /// This is a fake implementation of ProjectItemElementLink to be used to test ProjectInstance created from cache state does not access most state unless needed.
    /// Majority of the methods throw NotImplementedException by deliberate design.
    /// </summary>
    internal sealed class FakeProjectItemElementLink : ProjectItemElementLink
    {
        private readonly string _filePath;

        public FakeProjectItemElementLink(string elementName, string filePath)
        {
            ElementName = elementName ?? throw new ArgumentNullException(nameof(elementName));
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        }

        public override int Count => throw new NotImplementedException();

        public override ProjectElement FirstChild => throw new NotImplementedException();

        public override ProjectElement LastChild => throw new NotImplementedException();

        public override ProjectElementContainer Parent => throw new NotImplementedException();

        public override ProjectRootElement ContainingProject => new ProjectRootElement(new FakeProjectRootElementLink(_filePath));

        public override string ElementName { get; }

        public override string OuterElement => throw new NotImplementedException();

        public override bool ExpressedAsAttribute { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override ProjectElement PreviousSibling => throw new NotImplementedException();

        public override ProjectElement NextSibling => throw new NotImplementedException();

        public override ElementLocation Location => throw new NotImplementedException();

        public override IReadOnlyCollection<XmlAttributeLink> Attributes => throw new NotImplementedException();

        public override string PureText => throw new NotImplementedException();

        public override void AddInitialChild(ProjectElement child) => throw new NotImplementedException();

        public override void ChangeItemType(string newType) => throw new NotImplementedException();

        public override void CopyFrom(ProjectElement element) => throw new NotImplementedException();

        public override ProjectElement CreateNewInstance(ProjectRootElement owner) => throw new NotImplementedException();

        public override ProjectElementContainer DeepClone(ProjectRootElement factory, ProjectElementContainer parent) => throw new NotImplementedException();

        public override ElementLocation GetAttributeLocation(string attributeName) => throw new NotImplementedException();

        public override string GetAttributeValue(string attributeName, bool nullIfNotExists) => throw new NotImplementedException();

        public override void InsertAfterChild(ProjectElement child, ProjectElement reference) => throw new NotImplementedException();

        public override void InsertBeforeChild(ProjectElement child, ProjectElement reference) => throw new NotImplementedException();

        public override void RemoveChild(ProjectElement child) => throw new NotImplementedException();

        public override void SetOrRemoveAttribute(string name, string value, bool clearAttributeCache, string reason, string param) => throw new NotImplementedException();
    }
}
