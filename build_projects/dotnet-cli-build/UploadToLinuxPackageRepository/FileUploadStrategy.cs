// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using NuGet.Protocol;

namespace Microsoft.DotNet.Cli.Build.UploadToLinuxPackageRepository
{
    internal class FileUploadStrategy : IAzurelinuxRepositoryServiceHttpStrategy
    {
        private readonly string _pathToPackageToUpload;

        public FileUploadStrategy(string pathToPackageToUpload)
        {
            _pathToPackageToUpload = pathToPackageToUpload
                                     ?? throw new ArgumentNullException(nameof(pathToPackageToUpload));
        }

        public async Task<string> Execute(HttpClient client, Uri baseAddress)
        {
            var fileName = Path.GetFileName(_pathToPackageToUpload);

            using (var content =
                new MultipartFormDataContent())
            {
                var url = new Uri(baseAddress, "/v1/files");
                content.Add(
                    new StreamContent(
                        new MemoryStream(
                            File.ReadAllBytes(_pathToPackageToUpload))),
                    "file",
                    fileName);
                using (var message = await client.PostAsync(url, content))
                {
                    if (!message.IsSuccessStatusCode)
                    {
                        throw new FailedToAddPackageToPackageRepositoryException(
                            $"{message.ToJson()} failed to post file to {url} file name:{fileName} pathToPackageToUpload:{_pathToPackageToUpload}");
                    }
                    return await message.Content.ReadAsStringAsync();
                }
            }
        }
    }
}
