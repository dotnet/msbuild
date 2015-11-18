using System.Linq;

namespace System.Collections.Generic
{
    internal static class DictionaryExtensions
    {
        public static IEnumerable<V> GetOrEmpty<K, V>(this IDictionary<K, IEnumerable<V>> self, K key)
        {
            IEnumerable<V> val;
            if (!self.TryGetValue(key, out val))
            {
                return Enumerable.Empty<V>();
            }
            return val;
        }
    }
}
