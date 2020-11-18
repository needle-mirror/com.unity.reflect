using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace UnityEngine.Reflect
{
    /// <summary>
    ///     Generic service to manage shared objects and track the number of references to an object.
    /// </summary>
    public class MemoryTracker
    {
#pragma warning disable CS0649
        public struct Handle<TKey, TValue> // IMPORTANT Same layout as HandleInternal
        {
            int m_Id;
        }

        struct HandleInternal<TKey, TValue> // IMPORTANT Same layout as Handle
        {
            public int Id;
        }

        struct GenericHandleInternal // IMPORTANT Same layout as HandleInternal
        {
            public int Id;
        }

        class Cache<TKey, TValue>
        {
            public Action<TValue> Destructor;
            public Dictionary<TKey, (int Count, TValue Item)> ActiveItems;
            public Dictionary<TKey, (TimeSpan Time, TValue Item)> InactiveItems;
        }

        class GenericCache
        {
            public Action<object> Destructor;
            public Dictionary<object, (int Count, object Item)> ActiveItems;
            public Dictionary<object, object> InactiveItems;
        }
#pragma warning restore CS0649

        Clock.Proxy m_Clock;

        readonly Dictionary<GenericHandleInternal, GenericCache> m_Caches = new Dictionary<GenericHandleInternal, GenericCache>();
        int m_NextId;

        public MemoryTracker(Clock.Proxy clock)
        {
            m_Clock = clock;
        }

        /// <summary>
        ///     Creates a key/value cache.
        /// </summary>
        /// <typeparam name="TKey">The key type.</typeparam>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <param name="destructorFunc">The function called when the item is removed from the cache.</param>
        /// <returns>A handle to access the cache.</returns>
        public Handle<TKey, TValue> CreateCache<TKey, TValue>(Action<TValue> destructorFunc)
            where TValue : class
        {
            var handle = new HandleInternal<TKey, TValue>{ Id = ++m_NextId };
            var cache = new Cache<TKey, TValue>
            {
                Destructor = destructorFunc,
                ActiveItems = new Dictionary<TKey, (int, TValue)>(),
                InactiveItems = new Dictionary<TKey, (TimeSpan, TValue)>(),
            };

            m_Caches.Add(ToGenericHandle(handle), ToGenericCache(cache));

            return ToPublicHandle(handle);
        }

        /// <summary>
        ///     Destroy a key/value cache. Destroying a cache will call the destructor
        ///     function received when the cache was created for each object in the cache,
        ///     no matter if there are still active references on them.
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="handle"></param>
        public void DestroyCache<TKey, TValue>(Handle<TKey, TValue> handle)
        {
            var cache = GetCache(handle);

            foreach (var referenced in cache.ActiveItems)
            {
                cache.Destructor(referenced.Value.Item);
            }

            foreach (var unreferenced in cache.InactiveItems)
            {
                cache.Destructor(unreferenced.Value.Item);
            }

            m_Caches.Remove(ToGenericHandle(handle));
        }

        /// <summary>
        ///     Add an item to the cache and increment the reference counter by 1.
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="handle"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void AddAndAcquire<TKey, TValue>(Handle<TKey, TValue> handle, TKey key, TValue value)
        {
            var cache = GetCache(handle);

            if (cache.ActiveItems.ContainsKey(key) || cache.InactiveItems.ContainsKey(key))
            {
                throw new ArgumentException("An item with the same key already exists in the cache.");
            }

            cache.ActiveItems.Add(key, (1, value));
        }

        public void Set<TKey, TValue>(Handle<TKey, TValue> handle, TKey key, TValue value)
        {
            var cache = GetCache(handle);

            if (cache.ActiveItems.TryGetValue(key, out var entry))
            {
                cache.ActiveItems[key] = (entry.Count, value);
                return;
            }

            if (cache.InactiveItems.ContainsKey(key))
            {
                cache.InactiveItems[key] = (m_Clock.frameTime, value);
                return;
            }

            cache.InactiveItems.Add(key, (m_Clock.frameTime, value));
        }

        public bool ContainsKey<TKey, TValue>(Handle<TKey, TValue> handle, TKey key)
        {
            var cache = GetCache(handle);
            return cache.ActiveItems.ContainsKey(key) || cache.InactiveItems.ContainsKey(key);
        }

        public void SetAndAcquire<TKey, TValue>(Handle<TKey, TValue> handle, TKey key, TValue value)
        {
            var cache = GetCache(handle);

            cache.InactiveItems.Remove(key);

            cache.ActiveItems.TryGetValue(key, out var item);
            ++item.Count;
            item.Item = value;
            cache.ActiveItems[key] = item;
        }

        /// <summary>
        ///     Tries to get a value from a key. This does not affect reference counting.
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="handle"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryGetValue<TKey, TValue>(Handle<TKey, TValue> handle, TKey key, out TValue value)
        {
            var cache = GetCache(handle);

            if (cache.ActiveItems.TryGetValue(key, out var entry))
            {
                value = entry.Item;
                return true;
            }

            if (cache.InactiveItems.TryGetValue(key, out var inactiveValue))
            {
                value = inactiveValue.Item;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        ///     Try to acquire a value in the cache from the key. WIll throw if the <see cref="handle"/> is invalid.
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="handle"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryAcquire<TKey, TValue>(Handle<TKey, TValue> handle, TKey key, out TValue value)
        {
            var cache = GetCache(handle);

            if (cache.ActiveItems.TryGetValue(key, out var entry))
            {
                cache.ActiveItems[key] = (++entry.Count, entry.Item);
                value = entry.Item;
                return true;
            }

            if (cache.InactiveItems.TryGetValue(key, out var inactiveValue))
            {
                cache.InactiveItems.Remove(key);
                cache.ActiveItems[key] = (1, inactiveValue.Item);
                value = inactiveValue.Item;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        ///     Increment the reference count on the specific resource without returning the
        ///     actual resource. This is useful in some scenario where the tracking count
        ///     does not directly depend on the caller having the resource directly in its hands
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="handle"></param>
        /// <param name="key"></param>
        public void Acquire<TKey, TValue>(Handle<TKey, TValue> handle, TKey key)
        {
            if (!TryAcquire(handle, key, out _))
            {
                throw new KeyNotFoundException();
            }
        }

        /// <summary>
        ///     Release a reference to the object associated with the key. If the reference
        ///     count drop to 0, the object is moved to a special buffer that can be cleared explicitly.
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="handle"></param>
        /// <param name="key"></param>
        public void Release<TKey, TValue>(Handle<TKey, TValue> handle, TKey key)
        {
            if (!TryRelease(handle, key))
            {
                throw new Exception($"Key {key} does not exist in the cache with handle {ToGenericHandle(handle).Id}");
            }
        }

        public bool TryRelease<TKey, TValue>(Handle<TKey, TValue> handle, TKey key)
        {
            var cache = GetCache(handle);

            if (!cache.ActiveItems.TryGetValue(key, out var value))
            {
                return false;
            }

            if (value.Count == 1)
            {
                cache.ActiveItems.Remove(key);
                cache.InactiveItems.Add(key, (m_Clock.frameTime, value.Item));
            }
            else
            {
                cache.ActiveItems[key] = (value.Count - 1, value.Item);
            }

            return true;
        }

        public void ReleaseAndDestroy<TKey, TValue>(Handle<TKey, TValue> handle, TKey key)
        {
            var cache = GetCache(handle);

            if (!cache.ActiveItems.TryGetValue(key, out var value))
            {
                throw new Exception($"Key {key} does not exist in the cache with handle {ToGenericHandle(handle).Id}");
            }

            if (value.Count == 1)
            {
                cache.ActiveItems.Remove(key);
                cache.Destructor(value.Item);
            }
            else
            {
                cache.ActiveItems[key] = (value.Count - 1, value.Item);
            }
        }

        public List<TValue> GetActiveItems<TKey, TValue>(Handle<TKey, TValue> handle)
        {
            var cache = GetCache(handle);
            return cache.ActiveItems.Select(x => x.Value.Item).ToList();
        }

        public List<TValue> GetInactiveItems<TKey, TValue>(Handle<TKey, TValue> handle)
        {
            var cache = GetCache(handle);
            return cache.InactiveItems.Select(x => x.Value.Item).ToList();
        }

        public void ClearInactiveItems<TKey, TValue>(Handle<TKey, TValue> handle)
        {
            ClearInactiveItems(GetCache(handle));
        }

        public void ClearInactiveItemsOlderThan<TKey, TValue>(Handle<TKey, TValue> handle, TimeSpan expirationTime)
        {
            var cache = GetCache(handle);
            
            var toRemove = new List<TKey>(cache.InactiveItems.Count);

            foreach (var kv in cache.InactiveItems)
            {
                if (m_Clock.frameTime - kv.Value.Time < expirationTime)
                    continue;

                cache.Destructor(kv.Value.Item);
                toRemove.Add(kv.Key);
            }

            foreach (var key in toRemove)
            {
                cache.InactiveItems.Remove(key);
            }
        }

        Cache<TKey, TValue> GetCache<TKey, TValue>(Handle<TKey, TValue> handle)
        {
            var gh = ToGenericHandle(handle);
            if (!m_Caches.TryGetValue(gh, out var cache))
            {
                throw new Exception($"Cache for handle {gh.Id} does not exist.");
            }

            return Unsafe.As<Cache<TKey, TValue>>(cache);
        }

        static void ClearInactiveItems<TKey, TValue>(Cache<TKey, TValue> cache)
        {
            foreach (var kv in cache.InactiveItems)
            {
                cache.Destructor(kv.Value.Item);
            }

            cache.InactiveItems.Clear();
        }

        static GenericHandleInternal ToGenericHandle<TKey, TValue>(Handle<TKey, TValue> handle)
        {
            return Unsafe.As<Handle<TKey, TValue>, GenericHandleInternal>(ref handle);
        }

        static GenericHandleInternal ToGenericHandle<TKey, TValue>(HandleInternal<TKey, TValue> handle)
        {
            return Unsafe.As<HandleInternal<TKey, TValue>, GenericHandleInternal>(ref handle);
        }

        static GenericCache ToGenericCache<TKey, TValue>(Cache<TKey, TValue> cache)
        {
            return Unsafe.As<GenericCache>(cache);
        }

        static Handle<TKey, TValue> ToPublicHandle<TKey, TValue>(HandleInternal<TKey, TValue> handle)
        {
            return Unsafe.As<HandleInternal<TKey, TValue>, Handle<TKey, TValue>>(ref handle);
        }
    }
}