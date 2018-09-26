using System.Collections.Generic;
using System.Collections.Specialized;

namespace ProxyProfiler.Classes
{
    public class HybridDictionary<TKey, TValue> : HybridDictionary, IDictionary<TKey, TValue>
    {
        public HybridDictionary() : base() { }

        public HybridDictionary(bool caseInsensitive) : base(caseInsensitive) { }

        public TValue this[TKey key]
        {
            get
            {
                return (TValue)base[key];
            }

            set
            {
                base[key] = value;
            }
        }

        ICollection<TKey> IDictionary<TKey, TValue>.Keys
        {
            get
            {
                return base.Keys as ICollection<TKey>;
            }
        }

        ICollection<TValue> IDictionary<TKey, TValue>.Values
        {
            get
            {
                return base.Values as ICollection<TValue>;
            }
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            base.Add(item.Key, item.Value);
        }

        public void Add(TKey key, TValue value)
        {
            base.Add(key, value);
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return base.Contains(item.Key);
        }

        public bool ContainsKey(TKey key)
        {
            return base.Contains(key);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            base.CopyTo(array, arrayIndex);
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return SafeRemove(item.Key);
        }

        public bool Remove(TKey key)
        {
            return SafeRemove(key);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            value = default(TValue);

            try
            {
                value = (TValue)base[key];
            }
            catch
            {
                return false;
            }

            return true;
        }

        public new IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            var enumerator = base.GetEnumerator();

            while (enumerator.MoveNext())
            {
                yield return new KeyValuePair<TKey, TValue>((TKey)enumerator.Key, (TValue)enumerator.Value);
            }

            //return base.GetEnumerator() as IEnumerator<KeyValuePair<TKey, TValue>>;
        }

        private bool SafeRemove(TKey key)
        {
            try
            {
                base.Remove(key);
            }
            catch
            {
                return false;
            }

            return true;
        }
    }
}
