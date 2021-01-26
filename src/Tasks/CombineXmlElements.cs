using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Microsoft.Build.Tasks
{
    public class CombineXmlElements : TaskExtension
    {
        public string RootElementName { get; set; }

        public ITaskItem [] XmlElements { get; set; }

        [Output]
        public string Result { get; set; }

        public override bool Execute()
        {
            XElement root = new XElement(RootElementName);

            foreach (var item in XmlElements)
            {
                root.Add(XElement.Parse(item.ItemSpec));
            }

            Result = root.ToString();

            return true;
        }
    }
}
