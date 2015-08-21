// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Implementation of Some special translation methods that we 
// can't put in INodePacketTranslator.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
#if FEATURE_BINARY_SERIALIZATION
using System.Runtime.Serialization.Formatters.Binary;
#endif
using Microsoft.Build.Collections;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using System.Globalization;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// This class is responsible for serializing and deserializing anything that is not 
    /// officially supported by INodePacketTranslator, but that we still want to do 
    /// custom translation of.  
    /// </summary>
    static internal class NodePacketTranslatorExtensions
    {
        /// <summary>
        /// Translates a PropertyDictionary of ProjectPropertyInstances.
        /// </summary>
        /// <param name="translator">The tranlator doing the translating</param>
        /// <param name="value">The dictionary to translate.</param>
        public static void TranslateProjectPropertyInstanceDictionary(this INodePacketTranslator translator, ref PropertyDictionary<ProjectPropertyInstance> value)
        {
            if (!translator.TranslateNullable(value))
            {
                return;
            }

            if (translator.Mode == TranslationDirection.ReadFromStream)
            {
                int count = 0;
                translator.Translate(ref count);

                value = new PropertyDictionary<ProjectPropertyInstance>(count);
                for (int i = 0; i < count; i++)
                {
                    ProjectPropertyInstance instance = null;
                    translator.Translate(ref instance, ProjectPropertyInstance.FactoryForDeserialization);
                    value[instance.Name] = instance;
                }
            }
            else // TranslationDirection.WriteToStream
            {
                int count = value.Count;
                translator.Translate(ref count);

                foreach (ProjectPropertyInstance instance in value)
                {
                    ProjectPropertyInstance instanceForSerialization = instance;
                    translator.Translate(ref instanceForSerialization, ProjectPropertyInstance.FactoryForDeserialization);
                }
            }
        }
    }
}