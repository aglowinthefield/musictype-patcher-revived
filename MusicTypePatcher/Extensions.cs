using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicTypePatcher
{
    internal static class Extensions
    {
        public static IEnumerable<T> Intersection<T>(this IEnumerable<T>? source, IEnumerable<T>? other, IEqualityComparer<T>? comparer = null)
        {
            var _comparer = comparer ?? EqualityComparer<T>.Default;

            if (source == null || other == null)
                yield break;

            var set = new HashSet<T>(source, _comparer);
            var lookup = other.ToLookup(u => u, _comparer);

            if (set.Count is 0 || lookup.Count is 0)
            {
                yield break;
            }

            foreach (var it in set)
            {
                var lookupCount = lookup[it].Count();
                var sourceCount = source.Count(u => _comparer.Equals(u, it));

                foreach (var item in Enumerable.Repeat(it, Math.Min(lookupCount, sourceCount)))
                {
                    yield return item;
                }
            }
        }

        public static IEnumerable<T> DisjunctLeft<T>(this IEnumerable<T> left, IEnumerable<T>? right, IEqualityComparer<T>? comparer = null) where T : notnull
        {
            _ = left ?? throw new ArgumentNullException(nameof(left));

            if (right == null || !right.Any())
            {
                foreach (var it in left)
                    yield return it;
                yield break;
            }

            var dict = new Dictionary<T, int>(comparer);

            foreach (var it in right)
            {
                if (!dict.TryAdd(it, 1))
                    dict[it]++;
            }

            foreach (var it in left)
            {
                if (dict.TryGetValue(it, out int count) && count > 0)
                {
                    dict[it] = count - 1;
                    continue;
                }

                yield return it;
            }
        }
    }
}
