using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
