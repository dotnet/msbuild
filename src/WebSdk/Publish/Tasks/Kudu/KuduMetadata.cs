namespace Microsoft.NET.Sdk.Publish.Tasks.Kudu
{
    public class KuduConnectionInfo
    {
        public string UserName { get; set; }
        
        public string Password { get; set; }

        public string SiteName { get; set; }

        public string DestinationUrl { get; set; }
    }
}
