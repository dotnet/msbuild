// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Resources;

#nullable disable

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
