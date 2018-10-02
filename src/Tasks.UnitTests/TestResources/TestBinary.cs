// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.Tasks.UnitTests.TestResources
{
    public class TestBinary : IXunitSerializable
    {
        public static string LoremFilePath { get; } =
            Path.Combine(AppContext.BaseDirectory, "TestResources", "lorem.bin");

        public static TheoryData<TestBinary> GetLorem()
            => new TheoryData<TestBinary>
            {
                new TestBinary
                {
                    FilePath = LoremFilePath,
                    HashAlgorithm = "SHA256",
                    HashEncoding = "hex",
                    FileHash = "BCFAF334240356E1B97824A866F643B1ADA3C16AA0B5B2BFA8390D8BB54A244C",
                },
                new TestBinary
                {
                    FilePath = LoremFilePath,
                    HashAlgorithm = "SHA384",
                    HashEncoding = "hex",
                    FileHash = "5520B01FDE8A8A7EA38DCADFBF3CFAB2818FA0D5A8A16CB11A2FC7F5C9F1497F7B3C528FDB8CE10AA293A4E5FF32297F",
                },
                new TestBinary
                {
                    FilePath = LoremFilePath,
                    HashAlgorithm = "SHA512",
                    HashEncoding = "hex",
                    FileHash = "7774962C97EAC52B45291E1410F06AC6EFF6AF9ED38A57E2CEB720650282E46CFE512FAAD68AD9C45B74ED1B7E460198E0B00D5C9EF0404FF76B12F8AD2D329F",
                },
                 new TestBinary
                {
                    FilePath = LoremFilePath,
                    HashAlgorithm = "SHA256",
                    HashEncoding = "base64",
                    FileHash = "vPrzNCQDVuG5eCSoZvZDsa2jwWqgtbK/qDkNi7VKJEw="
                },
                new TestBinary
                {
                    FilePath = LoremFilePath,
                    HashAlgorithm = "SHA384",
                    HashEncoding = "base64",
                    FileHash = "VSCwH96Kin6jjcrfvzz6soGPoNWooWyxGi/H9cnxSX97PFKP24zhCqKTpOX/Mil/"
                },
                new TestBinary
                {
                    FilePath = LoremFilePath,
                    HashAlgorithm = "SHA512",
                    HashEncoding = "base64",
                    FileHash = "d3SWLJfqxStFKR4UEPBqxu/2r57TilfizrcgZQKC5Gz+US+q1orZxFt07Rt+RgGY4LANXJ7wQE/3axL4rS0ynw=="
                },
            };

        public string FileHash { get; private set; }
        public string FilePath { get; private set; }
        public string HashAlgorithm { get; private set; }
        public string HashEncoding { get; private set; }

        void IXunitSerializable.Deserialize(IXunitSerializationInfo info)
        {
            FileHash = info.GetValue<string>(nameof(FileHash));
            FilePath = info.GetValue<string>(nameof(FilePath));
            HashAlgorithm = info.GetValue<string>(nameof(HashAlgorithm));
            HashEncoding = info.GetValue<string>(nameof(HashEncoding));
        }

        void IXunitSerializable.Serialize(IXunitSerializationInfo info)
        {
            info.AddValue(nameof(FileHash), FileHash);
            info.AddValue(nameof(FilePath), FilePath);
            info.AddValue(nameof(HashAlgorithm), HashAlgorithm);
            info.AddValue(nameof(HashEncoding), HashEncoding);
        }
    }
}
