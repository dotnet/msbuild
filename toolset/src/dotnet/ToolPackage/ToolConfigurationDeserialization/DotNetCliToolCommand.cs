using System;
using System.Diagnostics;
using System.Xml.Serialization;

namespace Microsoft.DotNet.ToolPackage.ToolConfigurationDeserialization
{
    [Serializable]
    [DebuggerStepThrough]
    [XmlType(AnonymousType = true)]
    public class DotNetCliToolCommand
    {
        [XmlAttribute]
        public string Name { get; set; }

        [XmlAttribute]
        public string EntryPoint { get; set; }

        [XmlAttribute]
        public string Runner { get; set; }
    }
}
