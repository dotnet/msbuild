// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Configurer;

namespace Microsoft.DotNet.Cli
{
    public class AspNetCoreCertificateGenerator : IAspNetCoreCertificateGenerator
    {
        public void GenerateAspNetCoreDevelopmentCertificate()
        {
#if !EXCLUDE_ASPNETCORE
            Microsoft.AspNetCore.DeveloperCertificates.XPlat.CertificateGenerator.GenerateAspNetHttpsCertificate();
#endif
        }
    }
}
