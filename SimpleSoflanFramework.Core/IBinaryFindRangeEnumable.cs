using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Core.Base.Collections.Base
{
    public struct IndexRange
    {
        public IndexRange(int minIndex, int maxIndex)
        {
            MinIndex = minIndex;
            MaxIndex = maxIndex;
        }

        public int MinIndex { get; }
        public int MaxIndex { get; }
    }

    public interface IBinaryFindRangeEnumable<T, X> : IReadOnlyCollection<T> where X : IComparable<X>
    {
        IndexRange BinaryFindRangeIndex(X min, X max);
        IEnumerable<T> BinaryFindRange(X min, X max);
    }
}
