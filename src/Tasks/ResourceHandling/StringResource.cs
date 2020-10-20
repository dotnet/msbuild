// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Resources;

namespace Microsoft.Build.Tasks.ResourceHandling
{
    internal class StringResource : LiveObjectResource
    {
        public string OriginatingFile { get; }

        public new string TypeFullName => typeof(string).FullName;

        public StringResource(string name, string value, string filename) :
            base(name, value)
        {
            OriginatingFile = filename;
        }

        public new void AddTo(IResourceWriter writer)
        {
            writer.AddResource(Name, (string)Value);
        }

        public override string ToString()
        {
            return $"StringResource(\"{Name}\", \"{Value}\")";
        }
    }
}
