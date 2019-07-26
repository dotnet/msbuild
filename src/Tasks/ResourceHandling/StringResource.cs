// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Tasks.ResourceHandling
{
    internal class StringResource : IResource
    {
        public string Name { get; }

        public string OriginatingFile { get; }

        public string Value { get; }

        public StringResource(string name, string value, string filename)
        {
            Name = name;
            Value = value;
            OriginatingFile = filename;
        }

        public void AddTo(IResourceWriter writer)
        {
            writer.AddResource(Name, Value);
        }

        public override string ToString()
        {
            return $"StringResource(\"{Name}\", \"{Value}\")";
        }
    }
}
