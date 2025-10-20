using Dalamud.Utility;
using Microsoft.Extensions.Caching.Memory;
using System;

namespace BisBuddy.Services
{
    public class AttributeService(
        IMemoryCache memoryCache
        ) : IAttributeService
    {
        private static readonly MemoryCacheEntryOptions CacheOptions = new()
        {
            SlidingExpiration = TimeSpan.FromSeconds(1),
        };

        private readonly IMemoryCache memoryCache = memoryCache;

        public T? GetEnumAttribute<T>(Enum enumValue) where T : Attribute
        {
            if (enumValue is null)
                throw new ArgumentException("enumValue cannot be null");

            var cacheKey = (enumValue, typeof(T));

            if (memoryCache.TryGetValue(cacheKey, out var cacheValue))
                return (T?) cacheValue;

            var newCacheValue = EnumExtensions.GetAttribute<T>(enumValue)
                ?? throw new ArgumentException($"Enum value {Enum.GetName(enumValue.GetType(), enumValue)} has no {typeof(T).Name}");

            memoryCache.Set(cacheKey, newCacheValue, CacheOptions);

            return newCacheValue;
        }

    }

    public interface IAttributeService
    {
        T? GetEnumAttribute<T>(Enum value) where T : Attribute;
    }
}
