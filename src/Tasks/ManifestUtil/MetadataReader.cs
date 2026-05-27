// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Specialized;
#if RUNTIME_TYPE_NETCORE
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
#else
using System.Runtime.InteropServices;
using Microsoft.Build.Tasks.Metadata;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
#endif

#nullable disable

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
            catch (Exception)
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
            EnsureCustomAttributes();
            return _customAttributes.Contains(name);
        }

        public void HasAssemblyAttributes(string[] names, bool[] results)
        {
            EnsureCustomAttributes();
            for (int i = 0; i < names.Length; i++)
            {
                results[i] = _customAttributes.Contains(names[i]);
            }
        }

        private void EnsureCustomAttributes()
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
                {
                    continue;
                }

                EntityHandle mHandle = _reader.GetMemberReference((MemberReferenceHandle)ctorHandle).Parent;
                if (mHandle.Kind != HandleKind.TypeReference)
                {
                    continue;
                }

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

                publicKeyToken =
#if NET
                    Convert.ToHexString(pkt);
#else
                    BitConverter.ToString(pkt).Replace("-", "");
#endif
            }

            if (!String.IsNullOrEmpty(publicKeyToken))
            {
                publicKeyToken = publicKeyToken.ToUpperInvariant();
            }

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
    internal unsafe class MetadataReader : IDisposable
    {
        private readonly string _path;
        private StringDictionary _attributes;

        // COM pointers stored thread-agile via the GIT. Disposed in Close().
        // The CLR metadata object returned by IMetaDataDispenser::OpenScope implements
        // all three of IMetaDataImport, IMetaDataImport2, and IMetaDataAssemblyImport;
        // we QueryInterface for the two we actually call. The dispenser itself is only
        // needed during construction.
        private AgileComPointer<IMetaDataAssemblyImport> _assemblyImport;
        private AgileComPointer<IMetaDataImport2> _import2;

        private static Guid s_refidGuid = GetGuidOfType(typeof(IReferenceIdentity));

        private MetadataReader(string path)
        {
            _path = path;
            // OpenScope failure is per-file (the path isn't a valid PE / assembly); we leave
            // _assemblyImport null so that Create(...) returns null, matching the .NET Core
            // branch's catch-all.
            //
            // IMPORTANT — do NOT call PInvoke.CoCreateInstance(CLSID_CorMetaDataDispenser, ...)
            // here, and do NOT use Activator.CreateInstance(Type.GetTypeFromCLSID(...)) if
            // we want this code to AOT-compile. See the matching comment in
            // AssemblyDependency/AssemblyInformation.cs for the full mechanism (regression
            // dotnet/msbuild #13853 / VC P2PReferences.08, HRESULT 0x80131700
            // CLR_E_SHIM_RUNTIMELOAD when raw CoCreateInstance hits the mscoree shim in a
            // host where the shim's bound-runtime state is not set up).
            //
            // ComClassFactory.TryCreateFromModule calls clr.dll's exported DllGetClassObject
            // directly, which routes to MetaDataDllGetClassObject and bypasses the shim.
            //
            // OpenScope is asked directly for IMetaDataImport2 — the underlying CLR RegMeta
            // coclass implements every IMetaData* interface, so this saves a QueryInterface
            // round-trip vs. asking for the base IMetaDataImport.
            if (!ComClassFactory.TryCreateFromModule(
                    "clr.dll",
                    CorMetadata.CLSID_CorMetaDataDispenser,
                    ComClassFactory.ClrDllGetClassObjectInternalExportName,
                    out ComClassFactory factory,
                    out HRESULT activationHr))
            {
                activationHr.ThrowOnFailure();
            }
            using (factory)
            {
                using ComScope<IMetaDataDispenser> dispenser = factory.TryCreateInstance<IMetaDataDispenser>(out HRESULT createHr);
                createHr.ThrowOnFailure();

                Guid import2Iid = IMetaDataImport2.IID_IMetaDataImport2;
                using ComScope<IMetaDataImport2> import2 = new();
                HRESULT hr;
                fixed (char* pPath = path)
                {
                    hr = dispenser.Pointer->OpenScope(pPath, CorOpenFlags.ofRead, &import2Iid, import2);
                }
                if (hr.Failed || import2.IsNull)
                {
                    return;
                }
                _import2 = new AgileComPointer<IMetaDataImport2>(import2.Pointer, takeOwnership: false);

                Guid asmIid = IMetaDataAssemblyImport.IID_IMetaDataAssemblyImport;
                using ComScope<IMetaDataAssemblyImport> asmImport = new();
                import2.Pointer->QueryInterface(&asmIid, asmImport).ThrowOnFailure();
                _assemblyImport = new AgileComPointer<IMetaDataAssemblyImport>(asmImport.Pointer, takeOwnership: false);
            }
        }

        public static MetadataReader Create(string path)
        {
            var r = new MetadataReader(path);
            return r._assemblyImport is not null ? r : null;
        }

        public bool HasAssemblyAttribute(string name)
        {
            using ComScope<IMetaDataAssemblyImport> asmImport = _assemblyImport.GetInterface();
            using ComScope<IMetaDataImport2> import2 = _import2.GetInterface();
            MdAssembly assemblyScope;
            // Tolerate failure: a scope with no mdAssembly token returns CLDB_E_RECORD_NOTFOUND.
            // The legacy [PreserveSig] RCW code ignored the HR; preserve that contract by
            // treating any non-S_OK as "attribute not present".
            if (asmImport.Pointer->GetAssemblyFromScope(&assemblyScope).Failed || assemblyScope.IsNil)
            {
                return false;
            }
            return HasAssemblyAttribute(import2.Pointer, assemblyScope, name);
        }

        // Batch variant: callers (e.g. AssemblyAttributeFlags) that probe several attributes on
        // the same reader pay a single GIT round-trip and a single GetAssemblyFromScope call
        // instead of one per attribute.
        public void HasAssemblyAttributes(string[] names, bool[] results)
        {
            using ComScope<IMetaDataAssemblyImport> asmImport = _assemblyImport.GetInterface();
            using ComScope<IMetaDataImport2> import2 = _import2.GetInterface();
            MdAssembly assemblyScope;
            if (asmImport.Pointer->GetAssemblyFromScope(&assemblyScope).Failed || assemblyScope.IsNil)
            {
                for (int i = 0; i < results.Length; i++)
                {
                    results[i] = false;
                }
                return;
            }

            for (int i = 0; i < names.Length; i++)
            {
                results[i] = HasAssemblyAttribute(import2.Pointer, assemblyScope, names[i]);
            }
        }

        // Takes a borrowed IMetaDataImport2* so callers in a hot path can reuse a single GIT
        // round-trip instead of paying one per call.
        //
        // The CLR returns S_OK with pcbData=size when the attribute is present, S_FALSE with
        // pcbData=0 when it is absent, and an error HRESULT otherwise. Treat anything that is
        // not S_OK as "not present" so failure paths cannot leave valueLen indeterminate.
        private static bool HasAssemblyAttribute(IMetaDataImport2* import2, MdAssembly assemblyScope, string name)
        {
            void* valuePtr = null;
            uint valueLen = 0;
            HRESULT hr;
            fixed (char* pName = name)
            {
                hr = import2->GetCustomAttributeByName(assemblyScope, pName, &valuePtr, &valueLen);
            }
            return hr == HRESULT.S_OK && valueLen != 0;
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
            _import2?.Dispose();
            _import2 = null;
            _assemblyImport?.Dispose();
            _assemblyImport = null;
            _attributes = null;
        }

        private void ImportAttributes()
        {
            IReferenceIdentity refid = (IReferenceIdentity)NativeMethods.GetAssemblyIdentityFromFile(_path, ref s_refidGuid);

            string name = refid.GetAttribute(null, "name");
            string version = refid.GetAttribute(null, "version");
            string publicKeyToken = refid.GetAttribute(null, "publicKeyToken");
            if (String.Equals(publicKeyToken, "neutral", StringComparison.OrdinalIgnoreCase))
            {
                publicKeyToken = String.Empty;
            }
            else if (!String.IsNullOrEmpty(publicKeyToken))
            {
                publicKeyToken = publicKeyToken.ToUpperInvariant();
            }

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
    }
#endif
}
