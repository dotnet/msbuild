using System.Diagnostics;
using System.Xml.Serialization;

namespace Microsoft.DotNet.ToolPackageObtainer.ToolConfigurationDeserialization
{
    [DebuggerStepThrough]
    [XmlRoot(Namespace = "", IsNullable = false)]
    public class DotNetCliTool
    {
        [XmlArrayItem("Command", IsNullable = false)]
        public DotNetCliToolCommand[] Commands { get; set; }
    }
}
