// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Text;
using System.Security.Cryptography;

namespace Microsoft.DotNet.Cli.Utils
{
    public class Uuid
    {
        /// <summary>
        /// Generate a Version 5 (SHA1 Name Based) Guid from a name.
        /// </summary>
        /// <param name="name">The name to use for generating the GUID.</param>
        /// <returns>A generated <see cref="GUID"/>.</returns>
        public static Guid Create(string name)
        {
            // Any fixed GUID will do for a namespace.
            Guid namespaceId = new Guid("28F1468D-672B-489A-8E0C-7C5B3030630C");

            using (SHA1 hasher = SHA1.Create())
            {
                var nameBytes = System.Text.Encoding.UTF8.GetBytes(name ?? string.Empty);
                var namespaceBytes = namespaceId.ToByteArray();

                SwapGuidByteOrder(namespaceBytes);

                var streamToHash = new byte[namespaceBytes.Length + nameBytes.Length];

                Array.Copy(namespaceBytes, streamToHash, namespaceBytes.Length);
                Array.Copy(nameBytes, 0, streamToHash, namespaceBytes.Length, nameBytes.Length);

                var hashResult = hasher.ComputeHash(streamToHash);

                var res = new byte[16];

                Array.Copy(hashResult, res, res.Length);

                unchecked { res[6] = (byte)(0x50 | (res[6] & 0x0F)); }
                unchecked { res[8] = (byte)(0x40 | (res[8] & 0x3F)); }

                SwapGuidByteOrder(res);

                return new Guid(res);
            }
        }

        /// <summary>
        /// Generates a version 3 UUID given a namespace UUID and name. This is based on the algorithm described in
        /// RFC 4122 (https://tools.ietf.org/html/rfc4122), section 4.3.
        /// </summary>
        /// <param name="namespaceUuid">The UUID representing the namespace.</param>
        /// <param name="name">The name for which to generate a UUID within the given namespace.</param>
        /// <returns>A UUID generated using the given namespace UUID and name.</returns>
        public static Guid CreateUuid(Guid namespaceUuid, string name)
        {
            // 1. Convert the name to a canonical sequence of octets (as defined by the standards or conventions of its name space); put the name space ID in network byte order. 
            byte[] namespaceBytes = namespaceUuid.ToByteArray();
            // Octet 0-3
            int timeLow = IPAddress.HostToNetworkOrder(BitConverter.ToInt32(namespaceBytes, 0));
            // Octet 4-5
            short timeMid = IPAddress.HostToNetworkOrder(BitConverter.ToInt16(namespaceBytes, 4));
            // Octet 6-7
            short timeHiVersion = IPAddress.HostToNetworkOrder(BitConverter.ToInt16(namespaceBytes, 6));

            // 2. Compute the hash of the namespace ID concatenated with the name
            byte[] nameBytes = Encoding.Unicode.GetBytes(name);
            byte[] hashBuffer = new byte[namespaceBytes.Length + nameBytes.Length];

            Buffer.BlockCopy(BitConverter.GetBytes(timeLow), 0, hashBuffer, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(timeMid), 0, hashBuffer, 4, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(timeHiVersion), 0, hashBuffer, 6, 2);
            Buffer.BlockCopy(namespaceBytes, 8, hashBuffer, 8, 8);
            Buffer.BlockCopy(nameBytes, 0, hashBuffer, 16, nameBytes.Length);
            byte[] hash;

            using SHA256 sha256 = SHA256.Create();
            hash = sha256.ComputeHash(hashBuffer);            

            Array.Resize(ref hash, 16);

            // 3. Set octets zero through 3 of the time_low field to octets zero through 3 of the hash. 
            timeLow = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(hash, 0));
            Buffer.BlockCopy(BitConverter.GetBytes(timeLow), 0, hash, 0, 4);

            // 4. Set octets zero and one of the time_mid field to octets 4 and 5 of the hash. 
            timeMid = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(hash, 4));
            Buffer.BlockCopy(BitConverter.GetBytes(timeMid), 0, hash, 4, 2);

            // 5. Set octets zero and one of the time_hi_and_version field to octets 6 and 7 of the hash. 
            timeHiVersion = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(hash, 6));

            // 6. Set the four most significant bits (bits 12 through 15) of the time_hi_and_version field to the appropriate 4-bit version number from Section 4.1.3. 
            timeHiVersion = (short)((timeHiVersion & 0x0fff) | 0x3000);
            Buffer.BlockCopy(BitConverter.GetBytes(timeHiVersion), 0, hash, 6, 2);

            // 7. Set the clock_seq_hi_and_reserved field to octet 8 of the hash. 
            // 8. Set the two most significant bits (bits 6 and 7) of the clock_seq_hi_and_reserved to zero and one, respectively.
            hash[8] = (byte)((hash[8] & 0x3f) | 0x80);

            // Steps 9-11 are essentially no-ops, but provided for completion sake
            // 9. Set the clock_seq_low field to octet 9 of the hash.
            // 10. Set octets zero through five of the node field to octets 10 through 15 of the hash.
            // 11. Convert the resulting UUID to local byte order. 

            return new Guid(hash);
        }

        // Do a byte order swap, .NET GUIDs store multi byte components in little
        // endian.
        private static void SwapGuidByteOrder(byte[] b)
        {
            Swap(b, 0, 3);
            Swap(b, 1, 2);
            Swap(b, 5, 6);
            Swap(b, 7, 8);
        }

        private static void Swap(byte[] b, int x, int y)
        {
            byte t = b[x];
            b[x] = b[y];
            b[y] = t;
        }
    }
}
