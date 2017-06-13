// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Microsoft.DotNet.Cli.Build
{
    public class GenerateResxSource : Task
    {

        [Required]
        public string ResxFile { get; set; }

        [Required]
        public string ResourceName { get; set; }

        [Required]
        public string SourceOutputPath { get; set; }

        public override bool Execute()
        {
            var source = new StringBuilder();
            void _(string line) { source.AppendLine(line); }

            string @namespace = Path.GetFileNameWithoutExtension(ResourceName);
            string @class = Path.GetExtension(ResourceName).TrimStart('.');

            _($"using System;");
            _($"using System.Globalization;");
            _($"using System.Reflection;");
            _($"using System.Resources;");
            _($"");
            _($"namespace {@namespace}");
            _($"{{");
            _($"    internal static class {@class}");
            _($"    {{");
            _($"        internal static CultureInfo Culture {{ get; set; }}");
            _($"        internal static ResourceManager ResourceManager {{ get; }} = new ResourceManager(\"{ResourceName}\", typeof({@class}).GetTypeInfo().Assembly);");

            foreach (var key in XDocument.Load(ResxFile)
                                         .Descendants("data")
                                         .Select(n => n.Attribute("name").Value))
            {
            _($"        internal static string {key} => ResourceManager.GetString(\"{key}\", Culture);");
            }
            _($"    }}");
            _($"}}");

            File.WriteAllText(SourceOutputPath, source.ToString());
            return true;
        }
    }
}
