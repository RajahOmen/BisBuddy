using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.Extensions
{
    public static class EnumerableExtensions
    {
        public static IOrderedEnumerable<T> OrderByDirection<T, TKey>(
            this IEnumerable<T> source,
            Func<T, TKey> keySelector,
            bool descending
            )
        {
            return descending
                ? source.OrderByDescending(keySelector)
                : source.OrderBy(keySelector);
        }

        public static IOrderedEnumerable<T> ThenByDirection<T, TKey>(
            this IOrderedEnumerable<T> source,
            Func<T, TKey> keySelector,
            bool descending
            )
        {
            return descending
                ? source.ThenByDescending(keySelector)
                : source.ThenBy(keySelector);
        }

        public static int FindIndex<T>(
            this IReadOnlyList<T> source,
            Predicate<T> predicate
            )
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(predicate);
            var index = 0;
            foreach (var item in source)
            {
                if (predicate(item))
                    return index;
                index++;
            }
            return -1;
        }
    }
}
