// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
