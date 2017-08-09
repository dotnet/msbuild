// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using NuGet.Protocol;

namespace Microsoft.DotNet.Cli.Build.UploadToLinuxPackageRepository
{
    internal class AddPackageStrategy : IAzurelinuxRepositoryServiceHttpStrategy
    {
        private readonly IdInRepositoryService _idInRepositoryService;
        private readonly string _packageName;
        private readonly string _packageVersion;
        private readonly string _repositoryId;

        public AddPackageStrategy(
            IdInRepositoryService idInRepositoryService,
            string packageName,
            string packageVersion,
            string repositoryId)
        {
            _idInRepositoryService = idInRepositoryService
                                     ?? throw new ArgumentNullException(nameof(idInRepositoryService));
            _packageName = packageName;
            _packageVersion = packageVersion;
            _repositoryId = repositoryId;
        }

        public async Task<string> Execute(HttpClient client, Uri baseAddress)
        {
            var debianUploadJsonContent = new Dictionary<string, string>
            {
                ["name"] = _packageName,
                ["version"] = AppendDebianRevisionNumber(_packageVersion),
                ["fileId"] = _idInRepositoryService.Id,
                ["repositoryId"] = _repositoryId
            }.ToJson();
            var content = new StringContent(debianUploadJsonContent,
                Encoding.UTF8,
                "application/json");

            using (var response = await client.PostAsync(new Uri(baseAddress, "/v1/packages"), content))
            {
                if (!response.IsSuccessStatusCode)
                    throw new FailedToAddPackageToPackageRepositoryException(
                        $"request:{debianUploadJsonContent} response:{response.ToJson()}");
                return response.Headers.GetValues("Location").Single();
            }
        }

        private static string AppendDebianRevisionNumber(string packageVersion)
        {
            return packageVersion + "-1";
        }
    }
}
