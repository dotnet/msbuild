// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Build.Construction;

    internal static class CollectionHelpers
    {
        public static IList<A> ConvertCollection<A, B>(this IEnumerable<B> source, Func<B, A> converter)
        {
            if (source == null)
            {
                return null;
            }
            // Just copy ...
            List<A> result = new List<A>();
            foreach (var b in source)
            {
                result.Add(converter(b));
            }

            return result;
        }

        public static IList<T> ImportCollection<T>(this ProjectCollectionLinker importer, IEnumerable<MockProjectElementLinkRemoter> source)
            where T : ProjectElement
        {
            if (source == null)
            {
                return null;
            }
            // Just copy ...
            List<T> result = new List<T>();
            foreach (var sRemoter in source)
            {
                var s = (T)sRemoter.Import(importer);
                result.Add(s);
            }

            return result;
        }

        public static IList<T> ImportCollection<T, RMock>(this ProjectCollectionLinker importer, IEnumerable<RMock> source)
            where T : class
            where RMock : MockLinkRemoter<T>, new()
        {
            if (source == null)
            {
                return null;
            }
            // Just copy ...
            List<T> result = new List<T>();
            foreach (var sRemoter in source)
            {
                var s = importer.Import<T, RMock>(sRemoter);
                result.Add(s);
            }

            return result;
        }

        public static IList<MockProjectElementLinkRemoter> ExportCollection<T>(this ProjectCollectionLinker exporter, IEnumerable<T> source)
            where T : ProjectElement
        {
            if (source == null)
            {
                return null;
            }
            // Just copy ...
            List<MockProjectElementLinkRemoter> result = new List<MockProjectElementLinkRemoter>();
            foreach (var s in source)
            {
                var sRemoter = exporter.ExportElement(s);
                result.Add(sRemoter);
            }
            return result;
        }

        public static IList<RMock> ExportCollection<T, RMock>(this ProjectCollectionLinker exporter, IEnumerable<T> source)
            where T : class
            where RMock : MockLinkRemoter<T>, new()
        {
            if (source == null)
            {
                return null;
            }
            // Just copy ...
            List<RMock> result = new List<RMock>();
            foreach (var s in source)
            {
                var sRemoter = exporter.Export<T, RMock>(s);
                result.Add(sRemoter);
            }
            return result;
        }


        public static IDictionary<TKey, TValue> ImportDictionary<TKey, TValue, RMock>(this ProjectCollectionLinker importer, IDictionary<TKey, RMock> source)
            where TValue : class
            where RMock : MockLinkRemoter<TValue>, new()
        {
            if (source == null)
            {
                return null;
            }
            // Just copy ...
            Dictionary<TKey, TValue> result = new Dictionary<TKey, TValue>();
            foreach (var sRemoter in source)
            {
                var value = importer.Import<TValue, RMock>(sRemoter.Value);
                result.Add(sRemoter.Key, value);
            }

            return result;
        }

        public static IDictionary<TKey, RMock> ExportDictionary<TKey, TValue, RMock>(this ProjectCollectionLinker exporter, IDictionary<TKey, TValue> source)
            where TValue : class
            where RMock : MockLinkRemoter<TValue>, new()
        {
            if (source == null)
            {
                return null;
            }
            // Just copy ...
            Dictionary<TKey, RMock> result = new Dictionary<TKey, RMock>();
            foreach (var s in source)
            {
                var valueRemoter = exporter.Export<TValue, RMock>(s.Value);
                result.Add(s.Key, valueRemoter);
            }

            return result;
        }
    }
}
