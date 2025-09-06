// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.ObjectModelRemoting;

namespace Microsoft.Build.Engine.UnitTests.InstanceFromRemote
{
    /// <summary>
    /// This is a fake implementation of ProjectItemLink to be used to test ProjectInstance created from cache state does not access most state unless needed.
    /// Project Items are actually being accessed by the project system, which is interested in a few key properties. Besiedes those, most methods throw NotImplementedException by deliberate design.
    /// </summary>
    internal sealed class FakeProjectItemLink : ProjectItemLink
    {
        private readonly IDictionary<string, string> _metadataValues;
        private readonly ProjectItemElementLink _xmlLink;

        public FakeProjectItemLink(Project project, string itemType, string evaluatedInclude, string definedFilePath, IDictionary<string, string> metadataValues)
        {
            Project = project ?? throw new ArgumentNullException(nameof(project));
            EvaluatedInclude = evaluatedInclude;
            _metadataValues = metadataValues ?? throw new ArgumentNullException(nameof(metadataValues));
            _xmlLink = new FakeProjectItemElementLink(itemType, definedFilePath);
        }

        public override Project Project { get; }

        // Note: this is still a thrown away overhead in current implementation.
        public override ProjectItemElement Xml => new ProjectItemElement(_xmlLink);

        public override string EvaluatedInclude { get; }

        public override ICollection<ProjectMetadata> MetadataCollection => throw new NotImplementedException();

        public override ICollection<ProjectMetadata> DirectMetadata { get; } = new FakeCachedEntityDictionary<ProjectMetadata>();

        public override void ChangeItemType(string newItemType) => throw new NotImplementedException();

        public override ProjectMetadata GetMetadata(string name) => throw new NotImplementedException();

        public override string GetMetadataValue(string name)
        {
            if (_metadataValues.TryGetValue(name, out string? value))
            {
                return value;
            }

            return string.Empty;
        }

        public override bool HasMetadata(string name) => throw new NotImplementedException();

        public override bool RemoveMetadata(string name) => throw new NotImplementedException();

        public override void Rename(string name) => throw new NotImplementedException();

        public override ProjectMetadata SetMetadataValue(string name, string unevaluatedValue, bool propagateMetadataToSiblingItems) => throw new NotImplementedException();
    }
}
