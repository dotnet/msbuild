// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.DotNet.Cli.Build.UploadToLinuxPackageRepository
{
    internal class LinuxPackageRepositoryDestiny
    {
        private readonly string _password;
        private readonly string _server;
        private readonly string _username;

        public LinuxPackageRepositoryDestiny(string username,
            string password,
            string server,
            string repositoryId)
        {
            _username = username ?? throw new ArgumentNullException(nameof(username));
            _password = password ?? throw new ArgumentNullException(nameof(password));
            _server = server ?? throw new ArgumentNullException(nameof(server));
            RepositoryId = repositoryId ?? throw new ArgumentNullException(nameof(repositoryId));
        }

        public string RepositoryId { get; }

        public Uri GetBaseAddress()
        {
            return new Uri($"https://{_server}");
        }

        public string GetSimpleAuth()
        {
            return $"{_username}:{_password}";
        }
    }
}
