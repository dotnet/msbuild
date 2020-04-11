// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using SdkReference = Microsoft.Build.Framework.SdkReference;
using SdkResultBase = Microsoft.Build.Framework.SdkResult;

namespace Microsoft.Build.BackEnd.SdkResolution
{
    /// <summary>
    /// An internal implementation of <see cref="Microsoft.Build.Framework.SdkResult"/>.
    /// </summary>
    internal sealed class SdkResult : SdkResultBase, INodePacket
    {
        public SdkResult(ITranslator translator)
        {
            Translate(translator);
        }

        public SdkResult(SdkReference sdkReference, IEnumerable<string> errors, IEnumerable<string> warnings)
        {
            Success = false;
            SdkReference = sdkReference;
            Errors = errors;
            Warnings = warnings;
        }

        public SdkResult(SdkReference sdkReference, string path, string version, IEnumerable<string> warnings)
        {
            Success = true;
            SdkReference = sdkReference;
            Path = path;
            Version = version;
            Warnings = warnings;
        }

        public SdkResult()
        {

        }

        public SdkResult(SdkReference sdkReference, IEnumerable<SdkResultPathAndVersion> paths, IDictionary<string, string> propertiesToAdd,
                         IDictionary<string, SdkResultItem> itemsToAdd, IEnumerable<string> warnings)
        {
            Success = true;
            SdkReference = sdkReference;
            if (paths != null)
            {
                var firstPathAndVersion = paths.FirstOrDefault();
                if (firstPathAndVersion != null)
                {
                    Path = firstPathAndVersion.Path;
                    Version = firstPathAndVersion.Version;
                }
                if (paths.Count() > 1)
                {
                    AdditionalPaths = paths.Skip(1).ToList();
                }
            }

            //  Note: these dictionaries should use StringComparison.OrdinalIgnoreCase
            PropertiesToAdd = propertiesToAdd;
            ItemsToAdd = itemsToAdd;

            Warnings = warnings;
        }

        public Construction.ElementLocation ElementLocation { get; set; }

        public IEnumerable<string> Errors { get; }

        public IEnumerable<string> Warnings { get; }
        public void Translate(ITranslator translator)
        {
            translator.Translate(ref _success);
            translator.Translate(ref _path);
            translator.Translate(ref _version);

            translator.Translate(ref _additionalPaths, SdkResultTranslationHelpers.Translate, count => new List<SdkResultPathAndVersion>(count));
            translator.TranslateDictionary(ref _propertiesToAdd, count => new Dictionary<string, string>(count, StringComparer.OrdinalIgnoreCase));
            translator.TranslateDictionary(ref _itemsToAdd,
                                           keyTranslator: (ITranslator t, ref string s) => t.Translate(ref s),
                                           valueTranslator: SdkResultTranslationHelpers.Translate,
                                           dictionaryCreator: count => new Dictionary<string, SdkResultItem>(count, StringComparer.OrdinalIgnoreCase));

            translator.Translate(ref _sdkReference);
        }

        public NodePacketType Type => NodePacketType.ResolveSdkResponse;

        public static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            return new SdkResult(translator);
        }
    }
}
