using OngekiFumenEditor.Core.Base;
using OngekiFumenEditor.Core.Base.Collections;
using OngekiFumenEditor.Core.Base.Collections.Base.RangeTree;
using OngekiFumenEditor.Core.Base.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Core.Utils
{
    public static partial class MathUtils
    {
        public static float MapValue(
        float srcCurrent,
        float srcFrom, float srcTo,
        float distFrom, float distTo, bool limitDistInRange = true)
        {
            if (srcFrom == srcTo)
                return distFrom;

            float t = (srcCurrent - srcFrom) / (srcTo - srcFrom);

            if (limitDistInRange)
            {
                if (t < 0f) t = 0f;
                else if (t > 1f) t = 1f;
            }

            return distFrom + t * (distTo - distFrom);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Random() => RandomHepler.RandomDouble();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Random(int min, int max) => RandomHepler.Random(min, max);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Random(int max) => RandomHepler.Random(max);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Normalize(double from, double to, double cur)
        {
            var duration = to - from;
            return (cur - from) / duration;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LCM(int a, int b) => a / GCD(a, b) * b;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GCD(int a, int b) => b == 0 ? a : GCD(b, a % b);

        public static double CalculateLength(TGrid from, TGrid to, BpmList bpmList)
        {
            var fromBpm = bpmList.GetBpm(from);
            var toBpm = bpmList.GetBpm(to);

            if (fromBpm == toBpm)
                return CalculateBPMLength(fromBpm, to);

            var nextBpm = bpmList.GetNextBpm(fromBpm);
            var pre = CalculateBPMLength(from, nextBpm.TGrid, fromBpm.BPM);
            var aft = CalculateBPMLength(toBpm.TGrid, to, toBpm.BPM);

            var mid = 0d;
            var cur = nextBpm;
            while (cur != toBpm)
            {
                nextBpm = bpmList.GetNextBpm(cur);
                mid += CalculateBPMLength(cur.TGrid, nextBpm.TGrid, cur.BPM);
                cur = nextBpm;
            }

            return pre + mid + aft;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float RadianToAngle(float radian) => (float)(radian * 180 / Math.PI);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float AngleToRadian(float angle) => (float)(angle * Math.PI / 180);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double CalculateBPMLength(BPMChange from, BPMChange to) => CalculateBPMLength(from, to.TGrid);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double CalculateBPMLength(BPMChange from, TGrid to) => CalculateBPMLength(from.TGrid, to, from.BPM);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double CalculateBPMLength(TGrid from, TGrid to, double bpm)
        {
            if (to is null)
                return double.PositiveInfinity;

            return CalculateBPMLength(from.TotalUnit, to.TotalUnit, bpm);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double CalculateBPMLength(double fromTGridUnit, double toTGridUnit, double bpm,
            uint resT = TGrid.DEFAULT_RES_T, uint timeT = 240_000)
        {
            var diffGridUnit = toTGridUnit - fromTGridUnit;
            var totalGrid = diffGridUnit * resT;
            return timeT * totalGrid / (resT * bpm);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static double Limit(double val, double min, double max)
        {
            if (min > max)
            {
                var t = max;
                max = min;
                min = t;
            }

            return Math.Max(Math.Min(val, max), min);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Func<double, double> BuildTwoPointFormFormula(double x1, double y1, double x2, double y2)
        {
            var by = y2 - y1;
            var bx = x2 - x1;

            if (by == 0)
                return _ => x1;

            return y => (y - y1) / by * bx + x1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double CalculateXFromTwoPointFormFormula(double y, double x1, double y1, double x2, double y2)
        {
            var by = y2 - y1;
            var bx = x2 - x1;

            if (by == 0)
                return x1;

            return (y - y1) / by * bx + x1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double CalculateYFromTwoPointFormFormula(double x, double x1, double y1, double x2, double y2)
        {
            var by = y2 - y1;
            var bx = x2 - x1;

            if (by == 0)
                return y1;

            return (x - x1) / bx * by + y1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInRange(double start1, double end1, double start2, double end2)
            => (start1 <= end2 && start2 <= end1) || (start2 <= end1 && start1 <= end2);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInRange(TimeSpan start1, TimeSpan end1, TimeSpan start2, TimeSpan end2)
            => (start1 <= end2 && start2 <= end1) || (start2 <= end1 && start1 <= end2);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float calcGradient(float x1, float y1, float x2, float y2)
        {
            if (y1 == y2)
                return float.MaxValue;

            return (y1 - y2) / (x1 - x2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Max<T>(T a, T b) where T : GridBase => a > b ? a : b;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Min<T>(T a, T b) where T : GridBase => a > b ? b : a;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TimeSpan Min(TimeSpan a, TimeSpan b) => a > b ? b : a;

        public static IEnumerable<int> GetIntegersBetweenTwoValues(double from, double to)
        {
            var sign = Math.Sign(to - from);
            var begin = 0;
            var end = 0;

            if (sign > 0)
            {
                begin = (int)Math.Ceiling(from);
                end = (int)Math.Floor(to);
            }

            if (sign < 0)
            {
                begin = (int)Math.Floor(from);
                end = (int)Math.Ceiling(to);
            }

            for (var i = begin; sign > 0 ? i <= end : i >= end; i += sign)
                yield return i;
        }

        public record CombinableRange<T>(T Min, T Max) where T : IComparable<T>
        {
            public static IEnumerable<CombinableRange<T>> CombineRanges(IEnumerable<CombinableRange<T>> sortedList)
            {
                using var itor = sortedList.OrderBy(x => x.Min).GetEnumerator();
                if (!itor.MoveNext())
                    yield break;

                var cur = itor.Current;
                while (itor.MoveNext())
                {
                    var next = itor.Current;
                    if (next.Min.CompareTo(cur.Max) <= 0)
                    {
                        var newMin = cur.Min.CompareTo(next.Min) < 0 ? cur.Min : next.Min;
                        var newMax = cur.Max.CompareTo(next.Max) > 0 ? cur.Max : next.Max;
                        cur = new CombinableRange<T>(newMin, newMax);
                    }
                    else
                    {
                        yield return cur;
                        cur = next;
                    }
                }

                if (cur is not null)
                    yield return cur;
            }

            public static IIntervalTree<T, CombinableRange<T>> ToIntervalTree(IEnumerable<CombinableRange<T>> sortedList)
            {
                var comparer = new ComparerWrapper<T>((a, b) => a.CompareTo(b));
                var tree = new IntervalTree<T, CombinableRange<T>>(comparer);

                foreach (var range in sortedList)
                    tree.Add(range.Min, range.Max, range);

                return tree;
            }
        }
    }
}