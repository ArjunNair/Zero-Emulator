using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

//A generic MultiMap class that supports many-values-to-single-key mapping.
//Sourced from http://my6solutions.com/post/2009/05/25/C-MultiMap.aspx
// >>>>> Requires .NET Framework 3.5 as it uses HashSet! <<<<<

namespace ZiggyWin
{
    public class MultiMap<K, V>
    {
        Dictionary<K, HashSet<V>> dictionary = new Dictionary<K, HashSet<V>>();

        public void Add(K key, V value)
        {
            HashSet<V> list;
            if (dictionary.TryGetValue(key, out list))
            {
                list.Add(value);
            }
            else
            {
                list = new HashSet<V>{value};
                dictionary[key] = list;
            }
        }

        public void Remove(K key, V value)
        {
            HashSet<V> list;
            if (dictionary.TryGetValue(key, out list))
            {
                list.Remove(value);
                dictionary[key] = list;
            }
        }

        public void Remove(K key)
        {
            dictionary.Remove(key);
        }

        public void Add(K key, HashSet<V> values)
        {
            HashSet<V> list;
            if (dictionary.TryGetValue(key, out list))
            {
                list.UnionWith(values);
            }
            else
            {
                list = new HashSet<V> (values);
                dictionary[key] = list;
            }
        }

        public bool Contains(K key, V values)
        {
            HashSet<V> list;
            if (dictionary.TryGetValue(key, out list))
            {
                return true;
            }

            return false;
        }

        public IEnumerable<K> Keys
        {
            get
            {
                return dictionary.Keys;
            }
        }

        public HashSet<V> this[K key]
        {
            get
            {
                HashSet<V> list;
                if (dictionary.TryGetValue(key, out list))
                {
                    return list;
                }

                return new HashSet<V>();
            }

        }
    }
}
