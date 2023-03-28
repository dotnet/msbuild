// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;

#nullable disable

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
    internal static class XmlNamespaces
    {
        public const string asmv1 = "urn:schemas-microsoft-com:asm.v1";
        public const string asmv2 = "urn:schemas-microsoft-com:asm.v2";
        public const string asmv3 = "urn:schemas-microsoft-com:asm.v3";
        public const string dsig = "http://www.w3.org/2000/09/xmldsig#";
        public const string xrml = "urn:mpeg:mpeg21:2003:01-REL-R-NS";
        public const string xsi = "http://www.w3.org/2001/XMLSchema-instance";

        public static XmlNamespaceManager GetNamespaceManager(XmlNameTable nameTable)
        {
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(nameTable);
            nsmgr.AddNamespace("asmv1", asmv1);
            nsmgr.AddNamespace("asmv2", asmv2);
            nsmgr.AddNamespace("asmv3", asmv3);
            nsmgr.AddNamespace("dsig", dsig);
            nsmgr.AddNamespace("xrml", xrml);
            nsmgr.AddNamespace("xsi", xsi);
            return nsmgr;
        }
    }
}
