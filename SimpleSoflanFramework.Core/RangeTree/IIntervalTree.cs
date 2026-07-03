//copy&modify from repo : https://github.com/mbuchetics/RangeTree , LICENSE.txt : https://github.com/mbuchetics/RangeTree/blob/master/LICENSE.txt
using System.Collections.Generic;

namespace OngekiFumenEditor.Core.Base.Collections.Base.RangeTree
{
	/// <summary>
	/// The standard interval tree implementation. Keeps a root node and forwards all queries to it.
	/// Whenever new items are added or items are removed, the tree goes temporarily "out of sync", which means that the
	/// internal index is not updated immediately, but upon the next query operation.    
	/// </summary>
	/// <typeparam name="TKey">The type of the range.</typeparam>
	/// <typeparam name="TValue">The type of the data items.</typeparam>
	public interface IIntervalTree<TKey, TValue> : IReadOnlyCollection<RangeValuePair<TKey, TValue>>
	{
		/// <summary>
		/// Returns all items contained in the tree.
		/// </summary>
		IEnumerable<TValue> Values { get; }

		/// <summary>
		/// Performs a point query with a single value. All items with overlapping ranges are returned.
		/// </summary>
		IEnumerable<TValue> Query(TKey value);

		/// <summary>
		/// Performs a range query. All items with overlapping ranges are returned.
		/// </summary>
		IEnumerable<TValue> Query(TKey from, TKey to);

		/// <summary>
		/// Performs a range query and appends all overlapping items to the supplied list.
		/// </summary>
		void FillQuery(TKey from, TKey to, List<TValue> output);

		/// <summary>
		/// Adds the specified item.
		/// </summary>
		void Add(TKey from, TKey to, TValue value);

		/// <summary>
		/// Removes the specified item.
		/// </summary>
		void Remove(TValue item);

		/// <summary>
		/// Removes the specified items.
		/// </summary>
		void Remove(IEnumerable<TValue> items);

		/// <summary>
		/// Removes all elements from the range tree.
		/// </summary>
		void Clear();

		/// <summary>
		/// Notify implement that their items is dirty and need to rebuild again.
		/// </summary>
		void NotifyDirty();
	}
}
