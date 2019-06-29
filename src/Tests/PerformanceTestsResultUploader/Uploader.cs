using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Azure.Storage.Blob;

namespace PerformanceTestsResultUploader
{
    public static class Uploader
    {
        // SAS https://docs.microsoft.com/en-us/azure/storage/common/storage-dotnet-shared-access-signature-part-1
        public static void Upload(FileInfo generatedResult, string sas)
        {
            var newNameForUpload = CreateCopyWithNewName(generatedResult);
            Console.WriteLine($"Rename the file to {newNameForUpload.FullName}");
            string uploadUrl = string.Format("{0}{1}{2}", "https://pvscmdupload.blob.core.windows.net/results/", newNameForUpload.Name, sas);

            var cloudBlockBlob = new CloudBlockBlob(new Uri(uploadUrl));
            Console.WriteLine($"Start to upload");
            cloudBlockBlob.UploadFromFile(newNameForUpload.FullName);

            Console.WriteLine($"Done uploading");
        }
        private static FileInfo CreateCopyWithNewName(FileInfo generatedResult)
        {
            var newName = string.Format("{0}-{1}", Path.Join(
                                                    generatedResult.DirectoryName,
                                                    Environment.GetEnvironmentVariable("HELIX_CORRELATION_ID")),
                                                    generatedResult.Name);
            return generatedResult.CopyTo(newName);
        }
    }
}
