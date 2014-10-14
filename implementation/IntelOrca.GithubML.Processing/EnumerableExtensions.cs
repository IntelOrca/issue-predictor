using System;
using System.Collections.Generic;

namespace IntelOrca
{
	internal static class EnumerableExtensions
	{
		public static IEnumerable<T[]> SplitInto<T>(this ICollection<T> items, int numGroups)
		{
			if (numGroups <= 0)
				throw new ArgumentException("Number of groups must be greater than 0.", "numGroups");

			return items.SplitPer((items.Count + (numGroups - 1)) / numGroups);
		}

		public static IEnumerable<T[]> SplitPer<T>(this IEnumerable<T> items, int count)
		{
			if (count <= 0)
				throw new ArgumentException("Count must be greater than 0.", "count");

			List<T> group = new List<T>(count);
			foreach (T item in items) {
				group.Add(item);
				if (group.Count == count) {
					yield return group.ToArray();
					group.Clear();
				}
			}
			if (group.Count > 0)
				yield return group.ToArray();
		}
	}
}
