using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    public class ItemMetadataValue<T>
    {
        public string MetadataName { get; }

        private string _metadataValue;
        private Func<T, string> _metadataValueFunc;

        public ItemMetadataValue(string metadataName, string metadataValue)
        {
            MetadataName = metadataName;
            _metadataValue = metadataValue;
        }

        public ItemMetadataValue(string metadataName, Func<T, string> metadataValueFunc)
        {
            MetadataName = metadataName;
            _metadataValueFunc = metadataValueFunc;
        }

        public string GetMetadataValue(T source)
        {
            return _metadataValue ?? _metadataValueFunc(source);
        }
    }
}
