// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace Microsoft.NETCore.Build.Tasks.UnitTests
{
    public class MockTaskItem : ITaskItem
    {
        private Dictionary<string, string> _metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public MockTaskItem()
        {
        }

        public MockTaskItem(string itemSpec, Dictionary<string, string> metadata)
        {
            ItemSpec = itemSpec;
            foreach(var m in metadata)
            {
                _metadata.Add(m.Key, m.Value);
            }
        }

        public string ItemSpec { get; set; }

        public int MetadataCount
        {
            get
            {
                return _metadata.Count;
            }
        }

        public ICollection MetadataNames
        {
            get
            {
                return _metadata.Keys;
            }
        }

        public IDictionary CloneCustomMetadata()
        {
            throw new NotImplementedException();
        }

        public void CopyMetadataTo(ITaskItem destinationItem)
        {
            throw new NotImplementedException();
        }

        public string GetMetadata(string metadataName)
        {
            string metadatValue = null;
            if (_metadata.TryGetValue(metadataName, out metadatValue))
            {
                return metadatValue;
            }

            return null;
        }

        public void RemoveMetadata(string metadataName)
        {
            string metadatValue = null;
            if (_metadata.TryGetValue(metadataName, out metadatValue))
            {
                _metadata.Remove(metadataName);
            }
        }

        public void SetMetadata(string metadataName, string metadataValue)
        {
            _metadata[metadataName] = metadataValue;
        }
    }
}
