// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Reflection.Metadata;
using System.Text;

namespace Microsoft.NET.TestFramework
{
    public static class AssemblyInfo
    {
        public static IDictionary<string, string> Get(string assemblyPath)
        {
            var dictionary = new SortedDictionary<string, string>();

            using (var stream = File.OpenRead(assemblyPath))
            using (var peReader = new PEReader(stream))
            {
                var metadataReader = peReader.GetMetadataReader();
                var assemblyDefinition = metadataReader.GetAssemblyDefinition();

                // AssemblyVersion is not actually a custom attribute
                if (assemblyDefinition.Version != new Version(0, 0, 0, 0))
                {
                    dictionary.Add("AssemblyVersionAttribute", assemblyDefinition.Version.ToString());
                }

                foreach (var handle in assemblyDefinition.GetCustomAttributes())
                {
                    var attribute = metadataReader.GetCustomAttribute(handle);
                    var constructor = metadataReader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
                    var type = metadataReader.GetTypeReference((TypeReferenceHandle)constructor.Parent);
                    var name = metadataReader.GetString(type.Name);

                    var signature = metadataReader.GetBlobReader(constructor.Signature);
                    var value = metadataReader.GetBlobReader(attribute.Value);
                    var header = signature.ReadSignatureHeader();

                    const ushort prolog = 1; // two-byte "prolog" defined by ECMA-335 (II.23.3) to be at the beginning of attribute value blobs
                    if (value.ReadUInt16() != prolog || header.Kind != SignatureKind.Method || header.IsGeneric)
                    {
                        throw new BadImageFormatException();
                    }

                    var paramCount = signature.ReadCompressedInteger();
                    if (paramCount <= 0 || // must have at least 1 parameter
                        signature.ReadSignatureTypeCode() != SignatureTypeCode.Void) // return type must be void
                    {
                        continue;
                    }

                    var sb = new StringBuilder();
                    while (paramCount > 0 && sb != null)
                    {
                        switch (signature.ReadSignatureTypeCode())
                        {
                            case SignatureTypeCode.String:
                                sb.Append(value.ReadSerializedString());
                                break;
                            default:
                                sb = null;
                                break;
                        }

                        paramCount--;
                        if (paramCount != 0)
                        {
                            sb?.Append(':');
                        }
                    }

                    if (sb != null)
                    {
                        dictionary.Add(name, sb.ToString());
                    }
                }
            }

            return dictionary;
        }
    }
}
