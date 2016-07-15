using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Cli.Build
{
    public class SetBlobPropertiesBasedOnFileType : Task
    {
        private AzurePublisher _azurePublisher;

        [Required]
        public string AccountName { get; set; }

        [Required]
        public string AccountKey { get; set; }

        [Required]
        public ITaskItem[] Items { get; set; }

        private AzurePublisher AzurePublisherTool
        {
            get
            {
                if(_azurePublisher == null)
                {
                    _azurePublisher = new AzurePublisher(AccountName, AccountKey);
                }

                return _azurePublisher;
            }
        }

        public override bool Execute()
        {
            if (Items.Length == 0)
            {
                Log.LogError("No items were provided for upload.");
                return false;
            }

            foreach(var item in Items)
            {
                string relativeBlobPath = item.GetMetadata("RelativeBlobPath");
                if (string.IsNullOrEmpty(relativeBlobPath))
                {
                    throw new Exception(string.Format(
                      "Metadata 'RelativeBlobPath' is missing for item '{0}'.",
                      item.ItemSpec));
                }

                AzurePublisherTool.SetBlobPropertiesBasedOnFileType(relativeBlobPath);
            }            

            return true;
        }
    }
}