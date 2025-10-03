// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

#nullable disable

namespace System.Deployment.Internal.CodeSigning
{
    [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "RSAPKCS", Justification = "This casing is to match the existing RSAPKCS1SHA256SignatureDescription type")]
    [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SHA", Justification = "This casing is to match the use of SHA throughout the framework")]
    public sealed class RSAPKCS1SHA384SignatureDescription : SignatureDescription
    {
        public RSAPKCS1SHA384SignatureDescription()
        {
            KeyAlgorithm = typeof(RSACryptoServiceProvider).FullName;
#if RUNTIME_TYPE_NETCORE
            DigestAlgorithm = typeof(SHA384).FullName;
#else
            DigestAlgorithm = typeof(SHA384Cng).FullName;
#endif
            FormatterAlgorithm = typeof(RSAPKCS1SignatureFormatter).FullName;
            DeformatterAlgorithm = typeof(RSAPKCS1SignatureDeformatter).FullName;
        }

        public override AsymmetricSignatureDeformatter CreateDeformatter(AsymmetricAlgorithm key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            RSAPKCS1SignatureDeformatter deformatter = new RSAPKCS1SignatureDeformatter(key);
            deformatter.SetHashAlgorithm("SHA384");
            return deformatter;
        }

        public override AsymmetricSignatureFormatter CreateFormatter(AsymmetricAlgorithm key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            RSAPKCS1SignatureFormatter formatter = new RSAPKCS1SignatureFormatter(key);
            formatter.SetHashAlgorithm("SHA384");
            return formatter;
        }
    }
}
