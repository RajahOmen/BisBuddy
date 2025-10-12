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
            SlidingExpiration = TimeSpan.FromSeconds(5),
        };

        private readonly IMemoryCache memoryCache = memoryCache;

        public T? GetEnumAttribute<T>(Enum enumValue) where T : Attribute =>
            memoryCache.GetOrCreate(
                (enumValue, typeof(T)),
                cacheEntry => enumValue.GetAttribute<T>(),
                CacheOptions
                );
    }

    public interface IAttributeService
    {
        T? GetEnumAttribute<T>(Enum value) where T : Attribute;
    }
}
