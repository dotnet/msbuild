namespace Microsoft.NET.Sdk.Publish.Tasks.MsDeploy
{
    public class FileSkipData
    {
        public string sourceProvider { get; set; }
        public string sourceFilePath { get; set; }
        public string destinationProvider { get; set; }
        public string destinationFilePath { get; set; }

    }
}
