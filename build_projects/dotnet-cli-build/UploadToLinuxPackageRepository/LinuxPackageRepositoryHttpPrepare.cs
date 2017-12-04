// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Cli.Build.UploadToLinuxPackageRepository
{
    internal class LinuxPackageRepositoryHttpPrepare
    {
        private readonly IAzurelinuxRepositoryServiceHttpStrategy _httpStrategy;
        private readonly LinuxPackageRepositoryDestiny _linuxPackageRepositoryDestiny;

        public LinuxPackageRepositoryHttpPrepare(
            LinuxPackageRepositoryDestiny linuxPackageRepositoryDestiny,
            IAzurelinuxRepositoryServiceHttpStrategy httpStrategy
        )
        {
            _linuxPackageRepositoryDestiny = linuxPackageRepositoryDestiny
                                             ?? throw new ArgumentNullException(nameof(linuxPackageRepositoryDestiny));
            _httpStrategy = httpStrategy ?? throw new ArgumentNullException(nameof(httpStrategy));
        }

        public async Task<string> RemoteCall()
        {
            using (var handler = new HttpClientHandler())
            {
                using (var client = new HttpClient(handler))
                {
                    var authHeader =
                        Convert.ToBase64String(Encoding.UTF8.GetBytes((string) _linuxPackageRepositoryDestiny.GetSimpleAuth()));
                    client.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Basic", authHeader);
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.Timeout = TimeSpan.FromMinutes(10);

                    return await _httpStrategy.Execute(client, _linuxPackageRepositoryDestiny.GetBaseAddress());
                }
            }
        }
    }
}
