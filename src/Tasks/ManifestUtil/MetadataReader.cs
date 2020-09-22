// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Collections.Specialized;
#if RUNTIME_TYPE_NETCORE
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
#endif

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
#if RUNTIME_TYPE_NETCORE
    internal class MetadataReader : IDisposable
    {
        private StringDictionary _attributes;
        private List<string> _customAttributes;

        private FileStream _assemblyStream;
        private PEReader _peReader;
        private System.Reflection.Metadata.MetadataReader _reader;

        private MetadataReader(string path)
        {
            try
            {
                _assemblyStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Delete | FileShare.Read);
                if (_assemblyStream != null)
                {
                    _peReader = new PEReader(_assemblyStream, PEStreamOptions.LeaveOpen);
                    if (_peReader != null)
                    {
                        if (_peReader.HasMetadata)
                        {
                            _reader = _peReader.GetMetadataReader();
                        }
                    }
                }
            }
            catch(Exception)
            {
                Close();
            }
        }

        public static MetadataReader Create(string path)
        {
            var r = new MetadataReader(path);
            return r._reader != null ? r : null;
        }

        public bool HasAssemblyAttribute(string name)
        {
            if (_customAttributes == null)
            {
                lock (this)
                {
                    if (_customAttributes == null)
                    {
                        ImportCustomAttributesNames();
                    }
                }
            }

            return _customAttributes.Contains(name);
        }

        public string Name => Attributes[nameof(Name)];
        public string Version => Attributes[nameof(Version)];
        public string PublicKeyToken => Attributes[nameof(PublicKeyToken)];
        public string Culture => Attributes[nameof(Culture)];
        public string ProcessorArchitecture => Attributes[nameof(ProcessorArchitecture)];

        private void ImportCustomAttributesNames()
        {
            _customAttributes = new List<string>();

            AssemblyDefinition def = _reader.GetAssemblyDefinition();

            CustomAttributeHandleCollection col = def.GetCustomAttributes();
            foreach (CustomAttributeHandle handle in col)
            {
                EntityHandle ctorHandle = _reader.GetCustomAttribute(handle).Constructor;
                if (ctorHandle.Kind != HandleKind.MemberReference)
                    continue;

                EntityHandle mHandle = _reader.GetMemberReference((MemberReferenceHandle)ctorHandle).Parent;
                if (mHandle.Kind != HandleKind.TypeReference)
                    continue;

                string type = GetTypeName((TypeReferenceHandle)mHandle);

                _customAttributes.Add(type);
            }
        }

        private StringDictionary Attributes
        {
            get
            {
                if (_attributes == null)
                {
                    lock (this)
                    {
                        if (_attributes == null)
                        {
                            ImportAttributes();
                        }
                    }
                }

                return _attributes;
            }
        }

        private string GetTypeName(TypeReferenceHandle handle)
        {
            TypeReference reference = _reader.GetTypeReference(handle);

            // We don't need the type reference scope.

            return reference.Namespace.IsNil
                ? _reader.GetString(reference.Name)
                : _reader.GetString(reference.Namespace) + "." + _reader.GetString(reference.Name);
        }

        private void ImportAttributes()
        {
            AssemblyDefinition ad = _reader.GetAssemblyDefinition();

            string name = _reader.GetString(ad.Name);
            string version = ad.Version.ToString();
            string publicKeyToken = GetPublicKeyToken();
            string culture = _reader.GetString(ad.Culture);
            if (String.IsNullOrEmpty(culture))
            {
                culture = "neutral";
            }
            string processorArchitecture = GetProcessorArchitecture();

            _attributes = new StringDictionary
            {
                { "Name", name },
                { "Version", version },
                { "PublicKeyToken", publicKeyToken },
                { "Culture", culture },
                { "ProcessorArchitecture", processorArchitecture }
            };
        }

        private string GetPublicKeyToken()
        {
            string publicKeyToken = null;

            AssemblyDefinition ad = _reader.GetAssemblyDefinition();
            BlobReader br = _reader.GetBlobReader(ad.PublicKey);
            byte[] pk = br.ReadBytes(br.Length);
            if (pk.Length != 0)
            {
                AssemblyName an = new AssemblyName();
                an.SetPublicKey(pk);
                byte[] pkt = an.GetPublicKeyToken();

                publicKeyToken = BitConverter.ToString(pkt).Replace("-","");
            }

            if (!String.IsNullOrEmpty(publicKeyToken))
                publicKeyToken = publicKeyToken.ToUpperInvariant();

            return publicKeyToken;
        }

        private string GetProcessorArchitecture()
        {
            string processorArchitecture = "unknown";

            if (_peReader.PEHeaders == null ||
                _peReader.PEHeaders.CoffHeader == null)
            {
                return processorArchitecture;
            }

            Machine machine = _peReader.PEHeaders.CoffHeader.Machine;
            CorHeader corHeader = _peReader.PEHeaders.CorHeader;
            if (corHeader != null)
            {
                CorFlags corFlags = corHeader.Flags;
                if ((corFlags & CorFlags.ILLibrary) != 0)
                {
                    processorArchitecture = "msil";
                }
                else
                {
                    switch (machine)
                    {
                        case Machine.I386:
                            // "x86" only if corflags "requires" but not "prefers" x86
                            if ((corFlags & CorFlags.Requires32Bit) != 0 &&
                                (corFlags & CorFlags.Prefers32Bit) == 0)
                            {
                                processorArchitecture = "x86";
                            }
                            else
                            {
                                processorArchitecture = "msil";
                            }
                            break;
                        case Machine.IA64:
                            processorArchitecture = "ia64";
                            break;
                        case Machine.Amd64:
                            processorArchitecture = "amd64";
                            break;
                        case Machine.Arm:
                            processorArchitecture = "arm";
                            break;
                        case Machine.Arm64:
                            processorArchitecture = "arm64";
                            break;
                        default:
                            break;
                    }
                }
            }

            return processorArchitecture;
        }

        public void Close()
        {
            if (_peReader != null)
            {
                _peReader.Dispose();
            }

            if (_assemblyStream != null)
            {
                _assemblyStream.Close();
            }

            _attributes = null;
            _reader = null;
            _peReader = null;
            _assemblyStream = null;
        }

        void IDisposable.Dispose()
        {
            Close();
        }
    }
#else
    internal class MetadataReader : IDisposable
    {
        private readonly string _path;
        private StringDictionary _attributes;

        private IMetaDataDispenser _metaDispenser;
        private IMetaDataAssemblyImport _assemblyImport;

        private static Guid s_importerGuid = GetGuidOfType(typeof(IMetaDataImport));
        private static Guid s_refidGuid = GetGuidOfType(typeof(IReferenceIdentity));

        private MetadataReader(string path)
        {
            _path = path;
            // Create the metadata dispenser and open scope on the source file.
            _metaDispenser = (IMetaDataDispenser)new CorMetaDataDispenser();
            int hr = _metaDispenser.OpenScope(path, 0, ref s_importerGuid, out object obj);
            if (hr == 0)
            {
                _assemblyImport = (IMetaDataAssemblyImport)obj;
            }
        }

        public static MetadataReader Create(string path)
        {
            var r = new MetadataReader(path);
            return r._assemblyImport != null ? r : null;
        }

        [SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults", MessageId = "Microsoft.Build.Tasks.IMetaDataImport2.GetCustomAttributeByName(System.UInt32,System.String,System.IntPtr@,System.UInt32@)", Justification = "We verify the valueLen, we don't care what the return value is in this case")]
        public bool HasAssemblyAttribute(string name)
        {
            _assemblyImport.GetAssemblyFromScope(out uint assemblyScope);
            IMetaDataImport2 import2 = (IMetaDataImport2)_assemblyImport;
            IntPtr valuePtr;
            import2.GetCustomAttributeByName(assemblyScope, name, out valuePtr, out uint valueLen);
            return valueLen != 0;
        }

        public string Name => Attributes[nameof(Name)];
        public string Version => Attributes[nameof(Version)];
        public string PublicKeyToken => Attributes[nameof(PublicKeyToken)];
        public string Culture => Attributes[nameof(Culture)];
        public string ProcessorArchitecture => Attributes[nameof(ProcessorArchitecture)];

        private StringDictionary Attributes
        {
            get
            {
                if (_attributes == null)
                {
                    lock (this)
                    {
                        if (_attributes == null)
                        {
                            ImportAttributes();
                        }
                    }
                }

                return _attributes;
            }
        }

        public void Close()
        {
            if (_assemblyImport != null)
            {
                Marshal.ReleaseComObject(_assemblyImport);
            }

            if (_metaDispenser != null)
            {
                Marshal.ReleaseComObject(_metaDispenser);
            }
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
            {
                processorArchitecture = processorArchitecture.ToLowerInvariant();
            }

            _attributes = new StringDictionary
            {
                { "Name", name },
                { "Version", version },
                { "PublicKeyToken", publicKeyToken },
                { "Culture", culture },
                { "ProcessorArchitecture", processorArchitecture }
            };
        }

        void IDisposable.Dispose()
        {
            Close();
        }

        private static Guid GetGuidOfType(Type type)
        {
            var guidAttr = (GuidAttribute)Attribute.GetCustomAttribute(type, typeof(GuidAttribute), false);
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
#endif
}
