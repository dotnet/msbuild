// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Tasks.AssemblyDependency
{
    internal abstract class RarTaskItemBase : ITaskItem2, ITranslatable
    {
        protected Dictionary<string, string> _metadata;

        private Dictionary<int, int> _internedMetadata;

        private bool _enableMetadataInterning = false;

        public RarTaskItemBase()
        {
            _metadata = [];
            _internedMetadata = [];
        }

        public virtual string EvaluatedIncludeEscaped { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

        public virtual string ItemSpec { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

        public virtual ICollection MetadataNames => throw new System.NotImplementedException();

        public virtual int MetadataCount => throw new System.NotImplementedException();

        public IDictionary CloneCustomMetadata() => throw new System.NotImplementedException();

        public IDictionary CloneCustomMetadataEscaped() => throw new System.NotImplementedException();

        public virtual void CopyMetadataTo(ITaskItem destinationItem) => throw new System.NotImplementedException();

        public virtual string GetMetadata(string metadataName) => throw new System.NotImplementedException();

        public virtual string GetMetadataValueEscaped(string metadataName) => throw new System.NotImplementedException();

        public virtual void RemoveMetadata(string metadataName) => throw new System.NotImplementedException();

        public virtual void SetMetadata(string metadataName, string metadataValue) => throw new System.NotImplementedException();

        public virtual void SetMetadataValueLiteral(string metadataName, string metadataValue) => throw new System.NotImplementedException();

        public virtual void Translate(ITranslator translator)
        {
            // TODO: String interning needs further design and should only apply to metadata known to contain duplicates.
            // TODO: For now it is always disabled.
            if (_enableMetadataInterning)
            {
                IDictionary<int, int> internedMetadataRef = _internedMetadata;

                translator.TranslateDictionary(
                    ref internedMetadataRef,
                    (ITranslator aTranslator, ref int keyId) => aTranslator.Translate(ref keyId),
                    (ITranslator aTranslator, ref int valueId) => aTranslator.Translate(ref valueId),
                    capacity => new Dictionary<int, int>(capacity));
            }
            else
            {
                translator.TranslateDictionary(ref _metadata, MSBuildNameIgnoreCaseComparer.Default);
            }
        }

        public void InternMetadata(RarMetadataInternCache internCache)
        {
            _internedMetadata = new(_metadata.Count);

            foreach (KeyValuePair<string, string> kvp in _metadata)
            {
                int keyId = internCache.Intern(kvp.Key);
                int valueId = internCache.Intern(kvp.Value);
                _internedMetadata[keyId] = valueId;
            }
        }

        public void PopulateMetadata(RarMetadataInternCache internCache)
        {
            _metadata = new(_internedMetadata.Count, MSBuildNameIgnoreCaseComparer.Default);

            foreach (KeyValuePair<int, int> kvp in _internedMetadata)
            {
                string key = internCache.GetString(kvp.Key);
                string value = internCache.GetString(kvp.Value);
                _metadata[key] = value;
            }
        }
    }
}