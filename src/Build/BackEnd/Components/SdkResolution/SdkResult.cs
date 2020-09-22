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

        public SdkResult(SdkReference sdkReference, string path, string version, IEnumerable<string> warnings,
            IDictionary<string, string> propertiesToAdd = null, IDictionary<string, SdkResultItem> itemsToAdd = null)
        {
            Success = true;
            SdkReference = sdkReference;
            Path = path;
            Version = version;
            Warnings = warnings;
            PropertiesToAdd = propertiesToAdd;
            ItemsToAdd = itemsToAdd;
        }

        public SdkResult()
        {
        }

        public SdkResult(SdkReference sdkReference, IEnumerable<string> paths, string version, IDictionary<string, string> propertiesToAdd,
                         IDictionary<string, SdkResultItem> itemsToAdd, IEnumerable<string> warnings)
        {
            Success = true;
            SdkReference = sdkReference;
            if (paths != null)
            {
                var firstPath = paths.FirstOrDefault();
                if (firstPath != null)
                {
                    Path = firstPath;
                }
                if (paths.Count() > 1)
                {
                    AdditionalPaths = paths.Skip(1).ToList();
                }
            }

            Version = version;

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

            translator.Translate(ref _additionalPaths, (ITranslator t, ref string s) => t.Translate(ref s), count => new List<string>(count));
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

        public override bool Equals(object obj)
        {
            if (obj is SdkResult result &&
                  _success == result._success &&
                  StringComparer.OrdinalIgnoreCase.Equals(_path, result._path) &&
                  StringComparer.OrdinalIgnoreCase.Equals(_version, result._version) &&
                  _additionalPaths?.Count == result._additionalPaths?.Count &&
                  _propertiesToAdd?.Count == result._propertiesToAdd?.Count &&
                  _itemsToAdd?.Count == result._propertiesToAdd?.Count &&
                  EqualityComparer<SdkReference>.Default.Equals(_sdkReference, result._sdkReference))
            {
                if (_additionalPaths != null)
                {
                    for (int i = 0; i < _additionalPaths.Count; i++)
                    {
                        if (!_additionalPaths[i].Equals(result._additionalPaths[i], StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }
                    }
                }

                if (_propertiesToAdd != null)
                {
                    foreach (var propertyToAdd in _propertiesToAdd)
                    {
                        if (result._propertiesToAdd[propertyToAdd.Key] != propertyToAdd.Value)
                        {
                            return false;
                        }
                    }
                }

                if (_itemsToAdd != null)
                {
                    foreach (var itemToAdd in _itemsToAdd)
                    {
                        if (!result._itemsToAdd[itemToAdd.Key].Equals(itemToAdd.Value))
                        {
                            return false;
                        }
                    }
                }

                return true;
            }


            return false;
        }

        public override int GetHashCode()
        {
            int hashCode = -1043047289;
            hashCode = (hashCode * -1521134295) + _success.GetHashCode();
            hashCode = (hashCode * -1521134295) + StringComparer.OrdinalIgnoreCase.GetHashCode(_path);
            hashCode = (hashCode * -1521134295) + StringComparer.OrdinalIgnoreCase.GetHashCode(_version);
            hashCode = (hashCode * -1521134295) + EqualityComparer<SdkReference>.Default.GetHashCode(_sdkReference);

            if (_additionalPaths != null)
            {
                foreach (var additionalPath in _additionalPaths)
                {
                    hashCode = (hashCode * -1521134295) + StringComparer.OrdinalIgnoreCase.GetHashCode(additionalPath);
                }
            }
            if (_propertiesToAdd != null)
            {
                foreach (var propertyToAdd in _propertiesToAdd)
                {
                    hashCode = (hashCode * -1521134295) + propertyToAdd.Key.GetHashCode();
                    hashCode = (hashCode * -1521134295) + propertyToAdd.Value.GetHashCode();
                }
            }
            if (_itemsToAdd != null)
            {
                foreach (var itemToAdd in _itemsToAdd)
                {
                    hashCode = (hashCode * -1521134295) + itemToAdd.Key.GetHashCode();
                    hashCode = (hashCode * -1521134295) + itemToAdd.Value.GetHashCode();
                }
            }

            return hashCode;
        }
    }
}
