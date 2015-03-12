// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Collections.Specialized;
using Microsoft.Build.Tasks;

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
    internal class MetadataReader : IDisposable
    {
        private readonly string _path = null;
        private StringDictionary _attributes = null;

        private IMetaDataDispenser _metaDispenser = null;
        private IMetaDataAssemblyImport _assemblyImport = null;

        private static Guid s_importerGuid = GetGuidOfType(typeof(IMetaDataImport));
        private static Guid s_refidGuid = GetGuidOfType(typeof(IReferenceIdentity));

        private MetadataReader(string path)
        {
            _path = path;
            // Create the metadata dispenser and open scope on the source file.
            _metaDispenser = (IMetaDataDispenser)new CorMetaDataDispenser();
            object obj;
            int hr = _metaDispenser.OpenScope(path, 0, ref s_importerGuid, out obj);
            if (hr == 0)
                _assemblyImport = (IMetaDataAssemblyImport)obj;
        }

        public static MetadataReader Create(string path)
        {
            MetadataReader r = new MetadataReader(path);
            if (r._assemblyImport != null)
                return r;
            else
                return null;
        }

        [SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults", MessageId = "Microsoft.Build.Tasks.IMetaDataImport2.GetCustomAttributeByName(System.UInt32,System.String,System.IntPtr@,System.UInt32@)", Justification = "We verify the valueLen, we don't care what the return value is in this case")]
        public bool HasAssemblyAttribute(string name)
        {
            UInt32 assemblyScope;
            _assemblyImport.GetAssemblyFromScope(out assemblyScope);
            IMetaDataImport2 import2 = (IMetaDataImport2)_assemblyImport;
            IntPtr valuePtr = IntPtr.Zero;
            UInt32 valueLen = 0;
            import2.GetCustomAttributeByName(assemblyScope, name, out valuePtr, out valueLen);
            return valueLen != 0;
        }

        public string Name { get { return Attributes["Name"]; } }
        public string Version { get { return Attributes["Version"]; } }
        public string PublicKeyToken { get { return Attributes["PublicKeyToken"]; } }
        public string Culture { get { return Attributes["Culture"]; } }
        public string ProcessorArchitecture { get { return Attributes["ProcessorArchitecture"]; } }

        private StringDictionary Attributes
        {
            get
            {
                if (_attributes == null)
                {
                    lock (this)
                    {
                        if (_attributes == null)
                            ImportAttributes();
                    }
                }

                return _attributes;
            }
        }

        public void Close()
        {
            if (_assemblyImport != null)
                Marshal.ReleaseComObject(_assemblyImport);
            if (_metaDispenser != null)
                Marshal.ReleaseComObject(_metaDispenser);
            _attributes = null;
            _metaDispenser = null;
            _assemblyImport = null;
        }

        private void ImportAttributes()
        {
            IReferenceIdentity refid = (IReferenceIdentity)NativeMethods.GetAssemblyIdentityFromFile(_path, ref s_refidGuid);

            string name = refid.GetAttribute(null, "name");
            string version = refid.GetAttribute(null, "version");
            string publicKeyToken = refid.GetAttribute(null, "publicKeyToken");
            if (String.Equals(publicKeyToken, "neutral", StringComparison.OrdinalIgnoreCase))
                publicKeyToken = String.Empty;
            else if (!String.IsNullOrEmpty(publicKeyToken))
                publicKeyToken = publicKeyToken.ToUpperInvariant();
            string culture = refid.GetAttribute(null, "culture");
            string processorArchitecture = refid.GetAttribute(null, "processorArchitecture");
            if (!String.IsNullOrEmpty(processorArchitecture))
                processorArchitecture = processorArchitecture.ToLowerInvariant();

            _attributes = new StringDictionary();
            _attributes.Add("Name", name);
            _attributes.Add("Version", version);
            _attributes.Add("PublicKeyToken", publicKeyToken);
            _attributes.Add("Culture", culture);
            _attributes.Add("ProcessorArchitecture", processorArchitecture);

            refid = null;
        }

        void IDisposable.Dispose()
        {
            Close();
        }

        private static Guid GetGuidOfType(Type type)
        {
            GuidAttribute guidAttr = (GuidAttribute)Attribute.GetCustomAttribute(type, typeof(GuidAttribute), false);
            return new Guid(guidAttr.Value);
        }

        [ComImport]
        [Guid("6eaf5ace-7917-4f3c-b129-e046a9704766")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IReferenceIdentity
        {
            [return: MarshalAs(UnmanagedType.LPWStr)]
            string GetAttribute([In, MarshalAs(UnmanagedType.LPWStr)] string Namespace, [In, MarshalAs(UnmanagedType.LPWStr)] string Name);
            void SetAttribute();
            void EnumAttributes();
            void Clone();
        }

        [ComImport]
        [Guid("809c652e-7396-11d2-9771-00a0c9b4d50c")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [TypeLibType(TypeLibTypeFlags.FRestricted)]
        private interface IMetaDataDispenser
        {
            int DefineScope();
            [PreserveSig]
            int OpenScope([In][MarshalAs(UnmanagedType.LPWStr)]  string szScope, [In] UInt32 dwOpenFlags, [In] ref Guid riid, [Out][MarshalAs(UnmanagedType.Interface)] out object obj);
            int OpenScopeOnMemory();
        }
    }
}
