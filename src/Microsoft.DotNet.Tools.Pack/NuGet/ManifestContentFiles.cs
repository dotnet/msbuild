using System.Xml.Serialization;

namespace NuGet
{
    public class ManifestContentFiles
    {
        public string Include { get; set; }
        
        public string Exclude { get; set; }
        
        public string BuildAction { get; set; }

        public string CopyToOutput { get; set; }

        public string Flatten { get; set; }
    }
}