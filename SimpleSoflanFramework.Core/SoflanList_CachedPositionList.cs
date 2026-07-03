using OngekiFumenEditor.Core.Base.Collections.Base;
using OngekiFumenEditor.Core.Base.Collections.Base.RangeTree;
using OngekiFumenEditor.Core.Base.EditorObjects;
using OngekiFumenEditor.Core.Base.OngekiObjects;
using OngekiFumenEditor.Core.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OngekiFumenEditor.Core.Base.Collections
{
    public partial class SoflanList
    {
        public struct SoflanPoint
        {
            public SoflanPoint(double y, TGrid tGrid, double speed, BPMChange bpm)
            {
                Y = y;
                TGrid = tGrid;
                Speed = speed;
                Bpm = bpm;
            }

            public SoflanPoint(TGrid tGrid, double speed, BPMChange bpm)
            {
                Y = 0;
                TGrid = tGrid;
                Speed = speed;
                Bpm = bpm;
            }

            public double Y { get; set; }
            public TGrid TGrid { get; set; }
            public double Speed { get; set; }
            public BPMChange Bpm { get; set; }

            public override string ToString() => $"TGrid:{TGrid} Y:{Y} SPD:{Speed} BPM:{Bpm.BPM}";
        }

        private int cachedSoflanListCacheHash = RandomHepler.Random(int.MinValue, int.MaxValue);
        private List<SoflanPoint> cachedSoflanPositionList_DesignMode = new();
        private List<SoflanPoint> cachedSoflanPositionList_PreviewMode = new();

        public record VisibleTGridRange(TGrid minTGrid, TGrid maxTGrid)
        {
            public bool TryMerge(VisibleTGridRange another, out VisibleTGridRange mergedResult)
            {
                mergedResult = Merge(another);
                return mergedResult != default;
            }

            public VisibleTGridRange Merge(VisibleTGridRange another)
            {
                if ((minTGrid <= another.minTGrid && another.minTGrid <= maxTGrid) ||
                    another.minTGrid <= minTGrid && minTGrid <= another.maxTGrid)
                    return new(minTGrid <= another.minTGrid ? minTGrid : another.minTGrid, maxTGrid >= another.maxTGrid ? maxTGrid : another.maxTGrid);
                return default;
            }
        }

        public struct VisibleMsecRange
        {
            public VisibleMsecRange(double minMsec, double maxMsec)
            {
                MinMsec = minMsec;
                MaxMsec = maxMsec;
            }

            public double MinMsec { get; }
            public double MaxMsec { get; }

            public bool Contain(double msec)
            {
                return MinMsec <= msec && msec <= MaxMsec;
            }
        }

        internal struct VisibleGridRange
        {
            public VisibleGridRange(int minTotalGrid, int maxTotalGrid)
            {
                MinTotalGrid = minTotalGrid;
                MaxTotalGrid = maxTotalGrid;
            }

            public int MinTotalGrid { get; }
            public int MaxTotalGrid { get; }
        }

        public record SoflanSegment(int curIdx, SoflanPoint cur, SoflanPoint next);

        private IIntervalTree<double, SoflanSegment> cachePostionList_PreviewMode;

        public sealed class VisibleRangeQueryScratch
        {
            internal readonly List<SoflanSegment> QuerySegments = new();
            internal readonly List<VisibleGridRange> GridRanges = new();
            private int[] fullCheckVersions = new int[0];
            private int fullCheckVersion;

            public void Clear()
            {
                QuerySegments.Clear();
                GridRanges.Clear();
            }

            internal void ResetFullChecks(int segmentCount)
            {
                if (fullCheckVersions.Length < segmentCount)
                    fullCheckVersions = new int[segmentCount];

                if (fullCheckVersion == int.MaxValue)
                {
                    Array.Clear(fullCheckVersions, 0, fullCheckVersions.Length);
                    fullCheckVersion = 1;
                }
                else
                {
                    fullCheckVersion++;
                }
            }

            internal bool IsFullChecked(int posIdx)
            {
                return fullCheckVersions[posIdx] == fullCheckVersion;
            }

            internal void MarkFullChecked(int posIdx)
            {
                fullCheckVersions[posIdx] = fullCheckVersion;
            }
        }

        private sealed class SoflanSegmentIndexComparer : IComparer<SoflanSegment>
        {
            public static readonly SoflanSegmentIndexComparer Instance = new();

            public int Compare(SoflanSegment x, SoflanSegment y)
            {
                return x.curIdx.CompareTo(y.curIdx);
            }
        }

        private sealed class VisibleTGridRangeMinComparer : IComparer<VisibleTGridRange>
        {
            public static readonly VisibleTGridRangeMinComparer Instance = new();

            public int Compare(VisibleTGridRange x, VisibleTGridRange y)
            {
                return x.minTGrid.CompareTo(y.minTGrid);
            }
        }

        private sealed class VisibleGridRangeMinComparer : IComparer<VisibleGridRange>
        {
            public static readonly VisibleGridRangeMinComparer Instance = new();

            public int Compare(VisibleGridRange x, VisibleGridRange y)
            {
                return x.MinTotalGrid.CompareTo(y.MinTotalGrid);
            }
        }

        [Flags]
        private enum ChgEvt
        {
            None = 0,
            BpmChanged = 1,
            SoflanBegan = 2,
            SoflanEnded = 4,
            SoflanChanged = SoflanBegan | SoflanEnded
        }

        private struct ChangeEventTimingPoint
        {
            public ChangeEventTimingPoint(ITimelineObject timeline, ChgEvt evt)
            {
                Timeline = timeline;
                Evt = evt;
            }

            public ITimelineObject Timeline { get; }
            public ChgEvt Evt { get; }
        }

        public IEnumerable<SoflanPoint> GetCalculatableEvents(BpmList bpmList, bool isDesignModel)
        {
            var sortList = new List<ChangeEventTimingPoint>();
            foreach (var timelineObject in CollectionHelper.MergeTwoSortedCollections<ITimelineObject, TGrid>(x => x.TGrid, this, bpmList))
            {
                switch (timelineObject)
                {
                    case IDurationSoflan durationEvt:
                        var itor = durationEvt.GenerateKeyframeSoflans().GetEnumerator();
                        if (itor.MoveNext())
                        {
                            var init = itor.Current;
                            if (itor.MoveNext())
                            {
                                sortList.Add(new(init, ChgEvt.SoflanBegan));
                                var prev = itor.Current;
                                while (itor.MoveNext())
                                {
                                    sortList.Add(new(prev, ChgEvt.SoflanChanged));
                                    prev = itor.Current;
                                }
                                sortList.Add(new(prev, ChgEvt.SoflanEnded));
                            }
                            else
                            {
                                sortList.Add(new(init, ChgEvt.SoflanChanged));
                            }
                        }
                        break;
                    case IKeyframeSoflan keyframeEvt:
                        sortList.Add(new(keyframeEvt, ChgEvt.SoflanChanged));
                        break;
                    case BPMChange bpmEvt:
                        sortList.Add(new(bpmEvt, ChgEvt.BpmChanged));
                        break;
                    default:
                        throw new Exception($"Not support object for GetCalculatableEvents(): {timelineObject}");
                }
            }

            IEnumerable<ITimelineObject> Filter(IEnumerable<ChangeEventTimingPoint> source)
            {
                var soflan = default(ITimelineObject);

                foreach (var item in source)
                {
                    switch (item.Timeline)
                    {
                        case BPMChange:
                            yield return item.Timeline;
                            break;
                        case IKeyframeSoflan:
                            if (item.Evt == ChgEvt.SoflanEnded)
                                soflan ??= item.Timeline;
                            else
                                soflan = item.Timeline;
                            break;
                    }
                }

                if (soflan != null)
                    yield return soflan;
            }

            var groupEvents = sortList.GroupBy(x => x.Timeline.TGrid);
            var combineEvents = groupEvents.SelectMany(Filter).OrderBy(x => x.TGrid);

            IEnumerable<SoflanPoint> Visit()
            {
                double GetSpeed(ISoflan soflan) => isDesignModel ? soflan.SpeedInEditor : soflan.Speed;
                var firstSoflan = this.FirstOrDefault();
                if (firstSoflan != null && firstSoflan.TGrid > TGrid.Zero)
                    firstSoflan = default;

                SoflanPoint currentState =
                    new(TGrid.Zero, firstSoflan is null ? 1 : GetSpeed(firstSoflan), bpmList.GetBpm(TGrid.Zero));

                foreach (var item in combineEvents)
                {
                    if (item.TGrid != currentState.TGrid)
                    {
                        yield return currentState;
                        currentState.TGrid = item.TGrid;
                    }

                    switch (item)
                    {
                        case BPMChange curBpmChange:
                            currentState.Bpm = curBpmChange;
                            break;
                        case IKeyframeSoflan soflan:
                            currentState.Speed = GetSpeed(soflan);
                            break;
                    }
                }

                yield return currentState;
            }

            return Visit();
        }

        private void UpdateCachedSoflanPositionList(BpmList bpmList, List<SoflanPoint> list, bool isDesignMode)
        {
            list.Clear();

            using var itor = GetCalculatableEvents(bpmList, isDesignMode).GetEnumerator();
            if (!itor.MoveNext())
                return;

            var currentY = 0d;
            var prevEvent = itor.Current;

            while (itor.MoveNext())
            {
                var curEvent = itor.Current;
                var len = BpmMathUtils.CalculateBPMLength(prevEvent.TGrid, curEvent.TGrid, prevEvent.Bpm.BPM);
                var scaledLen = len * (isDesignMode ? Math.Abs(prevEvent.Speed) : prevEvent.Speed);

                list.Add(new(currentY, prevEvent.TGrid, prevEvent.Speed, prevEvent.Bpm));

                currentY += scaledLen;
                prevEvent = curEvent;
            }

            if (list.Count == 0)
                list.Add(new(0, TGrid.Zero, 1.0d, bpmList.FirstOrDefault()));
            else if (prevEvent.TGrid != list.First().TGrid)
                list.Add(new(currentY, prevEvent.TGrid, prevEvent.Speed, prevEvent.Bpm));
        }

        private IIntervalTree<double, SoflanSegment> RebuildIntervalTreePositionList(List<SoflanPoint> list)
        {
            var tree = new IntervalTree<double, SoflanSegment>();

            for (int i = 0; i < list.Count - 1; i++)
            {
                var prev = list[i];
                var next = list[i + 1];

                tree.Add(Math.Min(prev.Y, next.Y), Math.Max(prev.Y, next.Y), new(i, prev, next));
            }

            return tree;
        }

        private object locker = new object();

        private void CheckAndUpdateSoflanPositionList(BpmList bpmList)
        {
            var hash = bpmList.cachedBpmContentHash;

            if (cachedSoflanListCacheHash != hash)
            {
                lock (locker)
                {
                    if (cachedSoflanListCacheHash != hash)
                    {
                        UpdateCachedSoflanPositionList(bpmList, cachedSoflanPositionList_DesignMode, true);
                        UpdateCachedSoflanPositionList(bpmList, cachedSoflanPositionList_PreviewMode, false);
                        cachePostionList_PreviewMode = RebuildIntervalTreePositionList(cachedSoflanPositionList_PreviewMode);
                        cachedSoflanListCacheHash = hash;
                    }
                }
            }
        }

        public IList<SoflanPoint> GetCachedSoflanPositionList_DesignMode(BpmList bpmList)
        {
            CheckAndUpdateSoflanPositionList(bpmList);
            return cachedSoflanPositionList_DesignMode;
        }

        public IList<SoflanPoint> GetCachedSoflanPositionList_PreviewMode(BpmList bpmList)
        {
            CheckAndUpdateSoflanPositionList(bpmList);
            return cachedSoflanPositionList_PreviewMode;
        }

        public IIntervalTree<double, SoflanSegment> GetCachedSoflanSegment_PreviewMode(BpmList bpmList)
        {
            CheckAndUpdateSoflanPositionList(bpmList);
            return cachePostionList_PreviewMode;
        }

        public IEnumerable<VisibleTGridRange> GetVisibleRanges_PreviewMode(double currentY, double viewHeight, double preOffset, BpmList bpmList, double scale)
        {
            currentY /= scale;
            var actualViewHeight = viewHeight / scale;
            var actualPreOffset = preOffset / scale;
            var actualViewMinY = currentY - actualPreOffset;
            var actualViewMaxY = actualViewMinY + actualViewHeight;

            var list = GetCachedSoflanPositionList_PreviewMode(bpmList);
            var segments = GetCachedSoflanSegment_PreviewMode(bpmList);

            var fullCheckSets = new HashSet<int>();

            return TryMerge(CoreQuery(currentY, actualViewHeight, actualPreOffset, actualViewMinY, actualViewMaxY, list, segments, fullCheckSets));
        }

        public void FillVisibleRangesForGamePreview(double currentY, double viewHeight, BpmList bpmList, List<VisibleTGridRange> output, VisibleRangeQueryScratch scratch)
        {
            if (output is null)
                throw new ArgumentNullException(nameof(output));
            if (scratch is null)
                throw new ArgumentNullException(nameof(scratch));

            output.Clear();
            scratch.Clear();

            var list = GetCachedSoflanPositionList_PreviewMode(bpmList);
            if (list.Count == 0)
                return;

            if (list.Count > 1)
            {
                var actualViewMinY = currentY;
                var actualViewMaxY = currentY + viewHeight;
                var segments = GetCachedSoflanSegment_PreviewMode(bpmList);

                segments.FillQuery(actualViewMinY, actualViewMaxY, scratch.QuerySegments);

                if (scratch.QuerySegments.Count > 1)
                    scratch.QuerySegments.Sort(SoflanSegmentIndexComparer.Instance);

                scratch.ResetFullChecks(list.Count - 1);
                for (int i = scratch.QuerySegments.Count - 1; i >= 0; i--)
                    FillCalcSegment(scratch.QuerySegments[i].curIdx, currentY, 0, viewHeight, viewHeight, list, scratch, output);

                scratch.ResetFullChecks(list.Count - 1);

                for (int i = 0; i < scratch.QuerySegments.Count; i++)
                    FillCalcSegment(scratch.QuerySegments[i].curIdx, currentY, 0, 0, viewHeight, list, scratch, output);

                var last = list[list.Count - 1];
                if (currentY >= last.Y)
                {
                    var absSpeed = Math.Abs(last.Speed);

                    if (last.Speed > 0)
                    {
                        var left = Math.Max(currentY, last.Y);
                        var leftTGrid = last.TGrid + (absSpeed == 0 ? GridOffset.Zero : last.Bpm.LengthConvertToOffset((left - last.Y) / absSpeed));
                        var rightTGrid = last.TGrid + (absSpeed == 0 ? GridOffset.Zero : last.Bpm.LengthConvertToOffset((currentY + viewHeight - last.Y) / absSpeed));
                        output.Add(new(leftTGrid, rightTGrid));
                    }
                    else
                    {
                        var left = last.Y;
                        var leftTGrid = (last.TGrid - (absSpeed == 0 ? GridOffset.Zero : last.Bpm.LengthConvertToOffset(Math.Max(viewHeight, last.Y - left) / absSpeed))) ?? TGrid.Zero;
                        var rightTGrid = last.TGrid + (absSpeed == 0 ? GridOffset.Zero : last.Bpm.LengthConvertToOffset((last.Y - (currentY - viewHeight)) / absSpeed));
                        output.Add(new(leftTGrid, rightTGrid));
                    }
                }
            }
            else
            {
                var last = list[0];
                if (last.Speed > 0)
                {
                    var absSpeed = Math.Abs(last.Speed);
                    var left = Math.Max(0, currentY);
                    var leftTGrid = last.TGrid + (absSpeed == 0 ? GridOffset.Zero : last.Bpm.LengthConvertToOffset(left / absSpeed));
                    var rightTGrid = last.TGrid + (absSpeed == 0 ? GridOffset.Zero : last.Bpm.LengthConvertToOffset((left + viewHeight) / absSpeed));
                    output.Add(new(leftTGrid, rightTGrid));
                }
            }

            MergeVisibleRangesInPlace(output);
        }

        public void FillVisibleMsecRangesForGamePreview(double currentY, double viewHeight, BpmList bpmList, List<VisibleMsecRange> output, VisibleRangeQueryScratch scratch)
        {
            if (output is null)
                throw new ArgumentNullException(nameof(output));
            if (scratch is null)
                throw new ArgumentNullException(nameof(scratch));

            output.Clear();
            scratch.Clear();

            var list = GetCachedSoflanPositionList_PreviewMode(bpmList);
            if (list.Count == 0)
                return;

            if (list.Count > 1)
            {
                var actualViewMinY = currentY;
                var actualViewMaxY = currentY + viewHeight;
                var segments = GetCachedSoflanSegment_PreviewMode(bpmList);

                segments.FillQuery(actualViewMinY, actualViewMaxY, scratch.QuerySegments);

                if (scratch.QuerySegments.Count > 1)
                    scratch.QuerySegments.Sort(SoflanSegmentIndexComparer.Instance);

                scratch.ResetFullChecks(list.Count - 1);
                for (int i = scratch.QuerySegments.Count - 1; i >= 0; i--)
                    FillCalcSegmentGridRange(scratch.QuerySegments[i].curIdx, currentY, 0, viewHeight, viewHeight, list, scratch, scratch.GridRanges);

                scratch.ResetFullChecks(list.Count - 1);

                for (int i = 0; i < scratch.QuerySegments.Count; i++)
                    FillCalcSegmentGridRange(scratch.QuerySegments[i].curIdx, currentY, 0, 0, viewHeight, list, scratch, scratch.GridRanges);

                var last = list[list.Count - 1];
                if (currentY >= last.Y)
                {
                    var absSpeed = Math.Abs(last.Speed);

                    if (last.Speed > 0)
                    {
                        var left = Math.Max(currentY, last.Y);
                        var leftTotalGrid = AddLengthToTotalGrid(last.TGrid.TotalGrid, last.Bpm, (left - last.Y) / absSpeed);
                        var rightTotalGrid = AddLengthToTotalGrid(last.TGrid.TotalGrid, last.Bpm, (currentY + viewHeight - last.Y) / absSpeed);
                        scratch.GridRanges.Add(new VisibleGridRange(leftTotalGrid, rightTotalGrid));
                    }
                    else
                    {
                        var leftTotalGrid = SubtractLengthFromTotalGrid(last.TGrid.TotalGrid, last.Bpm, absSpeed == 0 ? 0 : viewHeight / absSpeed);
                        var rightTotalGrid = AddLengthToTotalGrid(last.TGrid.TotalGrid, last.Bpm, absSpeed == 0 ? 0 : (last.Y - (currentY - viewHeight)) / absSpeed);
                        scratch.GridRanges.Add(new VisibleGridRange(leftTotalGrid, rightTotalGrid));
                    }
                }
            }
            else
            {
                var last = list[0];
                if (last.Speed > 0)
                {
                    var absSpeed = Math.Abs(last.Speed);
                    var left = Math.Max(0, currentY);
                    var leftTotalGrid = AddLengthToTotalGrid(last.TGrid.TotalGrid, last.Bpm, left / absSpeed);
                    var rightTotalGrid = AddLengthToTotalGrid(last.TGrid.TotalGrid, last.Bpm, (left + viewHeight) / absSpeed);
                    scratch.GridRanges.Add(new VisibleGridRange(leftTotalGrid, rightTotalGrid));
                }
            }

            MergeVisibleGridRangesInPlace(scratch.GridRanges);

            var bpmTimingPoints = bpmList.GetCachedAllBpmUniformPositionList();
            for (var i = 0; i < scratch.GridRanges.Count; i++)
            {
                var range = scratch.GridRanges[i];
                output.Add(new VisibleMsecRange(
                    ConvertTotalGridToAudioMsec(range.MinTotalGrid, bpmTimingPoints),
                    ConvertTotalGridToAudioMsec(range.MaxTotalGrid, bpmTimingPoints)));
            }
        }

        private static IEnumerable<VisibleTGridRange> CoreQuery(double currentY, double actualViewHeight, double actualPreOffset, double actualViewMinY, double actualViewMaxY, IList<SoflanPoint> list, IIntervalTree<double, SoflanSegment> segments, HashSet<int> fullCheckSets)
        {
            if (list.Count > 1)
            {
                var querySegments = segments.Query(currentY, currentY)
                    .Concat(segments.Query(actualViewMinY, actualViewMaxY))
                    .Distinct()
                    .OrderBy(x => x.curIdx)
                    .ToList();

                var scanLeftLength = actualViewHeight - actualPreOffset;
                for (int i = querySegments.Count - 1; i >= 0; i--)
                {
                    foreach (var range in CalcSegment(querySegments[i].curIdx, currentY, 0, scanLeftLength, actualViewHeight, list, fullCheckSets))
                        yield return range;
                }

                fullCheckSets.Clear();

                var scanRightLength = actualPreOffset;
                for (int i = 0; i < querySegments.Count; i++)
                {
                    foreach (var range in CalcSegment(querySegments[i].curIdx, currentY, scanRightLength, 0, actualViewHeight, list, fullCheckSets))
                        yield return range;
                }

                var last = list.Last();
                if (currentY >= last.Y)
                {
                    var absSpeed = Math.Abs(last.Speed);
                    var leftRemain = actualPreOffset;
                    var rightRemain = actualViewHeight - actualPreOffset;

                    if (last.Speed > 0)
                    {
                        var left = Math.Max(currentY - leftRemain, last.Y);
                        var leftTGrid = last.TGrid + (absSpeed == 0 ? GridOffset.Zero : last.Bpm.LengthConvertToOffset((left - last.Y) / absSpeed));
                        var rightTGrid = last.TGrid + (absSpeed == 0 ? GridOffset.Zero : last.Bpm.LengthConvertToOffset((currentY + rightRemain - last.Y) / absSpeed));
                        yield return new(leftTGrid, rightTGrid);
                    }
                    else
                    {
                        var left = Math.Min(currentY + leftRemain, last.Y);
                        var leftTGrid = (last.TGrid - (absSpeed == 0 ? GridOffset.Zero : last.Bpm.LengthConvertToOffset(Math.Max(actualViewHeight, last.Y - left) / absSpeed))) ?? TGrid.Zero;
                        var rightTGrid = last.TGrid + (absSpeed == 0 ? GridOffset.Zero : last.Bpm.LengthConvertToOffset((last.Y - (currentY - rightRemain)) / absSpeed));
                        yield return new(leftTGrid, rightTGrid);
                    }
                }
            }
            else
            {
                var last = list[0];
                if (last.Speed > 0)
                {
                    var absSpeed = Math.Abs(last.Speed);
                    var left = Math.Max(0, actualViewMinY);
                    var leftTGrid = last.TGrid + (absSpeed == 0 ? GridOffset.Zero : last.Bpm.LengthConvertToOffset(left / absSpeed));
                    var rightTGrid = last.TGrid + (absSpeed == 0 ? GridOffset.Zero : last.Bpm.LengthConvertToOffset((left + actualViewHeight) / absSpeed));
                    yield return new(leftTGrid, rightTGrid);
                }
            }
        }

        private static IEnumerable<VisibleTGridRange> CalcSegment(int posIdx, double y, double leftRemain, double rightRemain, double actualViewHeight, IList<SoflanPoint> list, HashSet<int> fullCheckSets)
        {
            if (fullCheckSets.Contains(posIdx))
                return Enumerable.Empty<VisibleTGridRange>();

            var cur = list[posIdx];
            var next = list[posIdx + 1];
            var absSpeed = Math.Abs(cur.Speed);

            var leftMergeds = Enumerable.Empty<VisibleTGridRange>();
            var rightMergeds = Enumerable.Empty<VisibleTGridRange>();
            var left = 0d;
            var right = 0d;
            var newLeftRemain = 0d;
            var newRightRemain = 0d;
            var leftTGrid = default(TGrid);
            var rightTGrid = default(TGrid);

            if (cur.Speed > 0)
            {
                var calcLeftY = y - leftRemain;
                left = Math.Max(calcLeftY, cur.Y);
                newLeftRemain = Math.Min(leftRemain, cur.Y - calcLeftY);

                var calcRightY = y + rightRemain;
                right = Math.Min(next.Y, calcRightY);
                newRightRemain = Math.Min(rightRemain, calcRightY - next.Y);
            }
            else if (cur.Speed < 0)
            {
                var calcLeftY = y + leftRemain;
                left = Math.Min(calcLeftY, cur.Y);
                newLeftRemain = Math.Min(-cur.Y + left, leftRemain);

                var calcRightY = y - rightRemain;
                right = Math.Max(next.Y, calcRightY);
                newRightRemain = Math.Min(next.Y - calcRightY, rightRemain);
            }
            else
            {
                newLeftRemain = leftRemain;
                newRightRemain = rightRemain;
            }

            VisibleTGridRange curRange;
            if (cur.Speed > 0)
            {
                leftTGrid = cur.TGrid + (absSpeed == 0 ? GridOffset.Zero : cur.Bpm.LengthConvertToOffset((left - cur.Y) / absSpeed));
                rightTGrid = cur.TGrid + (absSpeed == 0 ? GridOffset.Zero : cur.Bpm.LengthConvertToOffset((right - cur.Y) / absSpeed));
                curRange = new(leftTGrid, rightTGrid);
            }
            else if (cur.Speed < 0)
            {
                leftTGrid = (cur.TGrid - (absSpeed == 0 ? GridOffset.Zero : cur.Bpm.LengthConvertToOffset(Math.Max(actualViewHeight, cur.Y - left) / absSpeed))) ?? TGrid.Zero;
                rightTGrid = cur.TGrid + (absSpeed == 0 ? GridOffset.Zero : cur.Bpm.LengthConvertToOffset((cur.Y - right) / absSpeed));
                curRange = new(leftTGrid, rightTGrid);
            }
            else
            {
                leftTGrid = cur.TGrid;
                rightTGrid = next.TGrid;
                left = cur.Y;
                right = next.Y;
                curRange = new(leftTGrid, rightTGrid);
            }

            if (newRightRemain >= 0 && newLeftRemain >= 0)
                fullCheckSets.Add(posIdx);

            if (newLeftRemain > 0)
            {
                if (posIdx > 0)
                    leftMergeds = CalcSegment(posIdx - 1, left, newLeftRemain, 0, actualViewHeight, list, fullCheckSets);
                else
                {
                    var overLeftTGrid = leftTGrid - (absSpeed == 0 ? GridOffset.Zero : cur.Bpm.LengthConvertToOffset(newLeftRemain / absSpeed));
                    leftMergeds = leftMergeds.Append(new VisibleTGridRange(overLeftTGrid ?? TGrid.Zero, leftTGrid));
                }
            }

            if (newRightRemain > 0)
            {
                if (posIdx < list.Count - 2)
                    rightMergeds = CalcSegment(posIdx + 1, right, 0, newRightRemain, actualViewHeight, list, fullCheckSets);
                else
                {
                    var absNextSpeed = Math.Abs(next.Speed);
                    var overRightTGrid = rightTGrid + (absNextSpeed == 0 ? GridOffset.Zero : next.Bpm.LengthConvertToOffset(newRightRemain / absNextSpeed));
                    rightMergeds = rightMergeds.Append(new VisibleTGridRange(rightTGrid, overRightTGrid));
                }
            }

            return leftMergeds.Append(curRange).Concat(rightMergeds);
        }

        private static void FillCalcSegment(int posIdx, double y, double leftRemain, double rightRemain, double actualViewHeight, IList<SoflanPoint> list, VisibleRangeQueryScratch scratch, List<VisibleTGridRange> output)
        {
            if (scratch.IsFullChecked(posIdx))
                return;

            var cur = list[posIdx];
            var next = list[posIdx + 1];
            var absSpeed = Math.Abs(cur.Speed);

            var left = 0d;
            var right = 0d;
            var newLeftRemain = 0d;
            var newRightRemain = 0d;
            var leftTGrid = default(TGrid);
            var rightTGrid = default(TGrid);

            if (cur.Speed > 0)
            {
                var calcLeftY = y - leftRemain;
                left = Math.Max(calcLeftY, cur.Y);
                newLeftRemain = Math.Min(leftRemain, cur.Y - calcLeftY);

                var calcRightY = y + rightRemain;
                right = Math.Min(next.Y, calcRightY);
                newRightRemain = Math.Min(rightRemain, calcRightY - next.Y);
            }
            else if (cur.Speed < 0)
            {
                var calcLeftY = y + leftRemain;
                left = Math.Min(calcLeftY, cur.Y);
                newLeftRemain = Math.Min(-cur.Y + left, leftRemain);

                var calcRightY = y - rightRemain;
                right = Math.Max(next.Y, calcRightY);
                newRightRemain = Math.Min(next.Y - calcRightY, rightRemain);
            }
            else
            {
                newLeftRemain = leftRemain;
                newRightRemain = rightRemain;
            }

            VisibleTGridRange curRange;
            if (cur.Speed > 0)
            {
                leftTGrid = cur.TGrid + (absSpeed == 0 ? GridOffset.Zero : cur.Bpm.LengthConvertToOffset((left - cur.Y) / absSpeed));
                rightTGrid = cur.TGrid + (absSpeed == 0 ? GridOffset.Zero : cur.Bpm.LengthConvertToOffset((right - cur.Y) / absSpeed));
                curRange = new(leftTGrid, rightTGrid);
            }
            else if (cur.Speed < 0)
            {
                leftTGrid = (cur.TGrid - (absSpeed == 0 ? GridOffset.Zero : cur.Bpm.LengthConvertToOffset(Math.Max(actualViewHeight, cur.Y - left) / absSpeed))) ?? TGrid.Zero;
                rightTGrid = cur.TGrid + (absSpeed == 0 ? GridOffset.Zero : cur.Bpm.LengthConvertToOffset((cur.Y - right) / absSpeed));
                curRange = new(leftTGrid, rightTGrid);
            }
            else
            {
                leftTGrid = cur.TGrid;
                rightTGrid = next.TGrid;
                left = cur.Y;
                right = next.Y;
                curRange = new(leftTGrid, rightTGrid);
            }

            if (newRightRemain >= 0 && newLeftRemain >= 0)
                scratch.MarkFullChecked(posIdx);

            if (newLeftRemain > 0)
            {
                if (posIdx > 0)
                    FillCalcSegment(posIdx - 1, left, newLeftRemain, 0, actualViewHeight, list, scratch, output);
                else
                {
                    var overLeftTGrid = leftTGrid - (absSpeed == 0 ? GridOffset.Zero : cur.Bpm.LengthConvertToOffset(newLeftRemain / absSpeed));
                    output.Add(new VisibleTGridRange(overLeftTGrid ?? TGrid.Zero, leftTGrid));
                }
            }

            output.Add(curRange);

            if (newRightRemain > 0)
            {
                if (posIdx < list.Count - 2)
                    FillCalcSegment(posIdx + 1, right, 0, newRightRemain, actualViewHeight, list, scratch, output);
                else
                {
                    var absNextSpeed = Math.Abs(next.Speed);
                    var overRightTGrid = rightTGrid + (absNextSpeed == 0 ? GridOffset.Zero : next.Bpm.LengthConvertToOffset(newRightRemain / absNextSpeed));
                    output.Add(new VisibleTGridRange(rightTGrid, overRightTGrid));
                }
            }
        }

        private static void FillCalcSegmentGridRange(int posIdx, double y, double leftRemain, double rightRemain, double actualViewHeight, IList<SoflanPoint> list, VisibleRangeQueryScratch scratch, List<VisibleGridRange> output)
        {
            if (scratch.IsFullChecked(posIdx))
                return;

            var cur = list[posIdx];
            var next = list[posIdx + 1];
            var absSpeed = Math.Abs(cur.Speed);

            var left = 0d;
            var right = 0d;
            var newLeftRemain = 0d;
            var newRightRemain = 0d;
            var leftTotalGrid = 0;
            var rightTotalGrid = 0;

            if (cur.Speed > 0)
            {
                var calcLeftY = y - leftRemain;
                left = Math.Max(calcLeftY, cur.Y);
                newLeftRemain = Math.Min(leftRemain, cur.Y - calcLeftY);

                var calcRightY = y + rightRemain;
                right = Math.Min(next.Y, calcRightY);
                newRightRemain = Math.Min(rightRemain, calcRightY - next.Y);
            }
            else if (cur.Speed < 0)
            {
                var calcLeftY = y + leftRemain;
                left = Math.Min(calcLeftY, cur.Y);
                newLeftRemain = Math.Min(-cur.Y + left, leftRemain);

                var calcRightY = y - rightRemain;
                right = Math.Max(next.Y, calcRightY);
                newRightRemain = Math.Min(next.Y - calcRightY, rightRemain);
            }
            else
            {
                newLeftRemain = leftRemain;
                newRightRemain = rightRemain;
            }

            VisibleGridRange curRange;
            if (cur.Speed > 0)
            {
                leftTotalGrid = AddLengthToTotalGrid(cur.TGrid.TotalGrid, cur.Bpm, (left - cur.Y) / absSpeed);
                rightTotalGrid = AddLengthToTotalGrid(cur.TGrid.TotalGrid, cur.Bpm, (right - cur.Y) / absSpeed);
                curRange = new VisibleGridRange(leftTotalGrid, rightTotalGrid);
            }
            else if (cur.Speed < 0)
            {
                leftTotalGrid = SubtractLengthFromTotalGrid(cur.TGrid.TotalGrid, cur.Bpm, Math.Max(actualViewHeight, cur.Y - left) / absSpeed);
                rightTotalGrid = AddLengthToTotalGrid(cur.TGrid.TotalGrid, cur.Bpm, (cur.Y - right) / absSpeed);
                curRange = new VisibleGridRange(leftTotalGrid, rightTotalGrid);
            }
            else
            {
                leftTotalGrid = cur.TGrid.TotalGrid;
                rightTotalGrid = next.TGrid.TotalGrid;
                left = cur.Y;
                right = next.Y;
                curRange = new VisibleGridRange(leftTotalGrid, rightTotalGrid);
            }

            if (newRightRemain >= 0 && newLeftRemain >= 0)
                scratch.MarkFullChecked(posIdx);

            if (newLeftRemain > 0)
            {
                if (posIdx > 0)
                    FillCalcSegmentGridRange(posIdx - 1, left, newLeftRemain, 0, actualViewHeight, list, scratch, output);
                else
                {
                    var overLeftTotalGrid = SubtractLengthFromTotalGrid(leftTotalGrid, cur.Bpm, absSpeed == 0 ? 0 : newLeftRemain / absSpeed);
                    output.Add(new VisibleGridRange(overLeftTotalGrid, leftTotalGrid));
                }
            }

            output.Add(curRange);

            if (newRightRemain > 0)
            {
                if (posIdx < list.Count - 2)
                    FillCalcSegmentGridRange(posIdx + 1, right, 0, newRightRemain, actualViewHeight, list, scratch, output);
                else
                {
                    var absNextSpeed = Math.Abs(next.Speed);
                    var overRightTotalGrid = AddLengthToTotalGrid(rightTotalGrid, next.Bpm, absNextSpeed == 0 ? 0 : newRightRemain / absNextSpeed);
                    output.Add(new VisibleGridRange(rightTotalGrid, overRightTotalGrid));
                }
            }
        }

        private static void MergeVisibleRangesInPlace(List<VisibleTGridRange> ranges)
        {
            if (ranges.Count <= 1)
                return;

            ranges.Sort(VisibleTGridRangeMinComparer.Instance);

            var writeIndex = 0;
            var cur = ranges[0];
            for (var readIndex = 1; readIndex < ranges.Count; readIndex++)
            {
                var next = ranges[readIndex];
                if (next.minTGrid <= cur.maxTGrid)
                {
                    cur = new(cur.minTGrid <= next.minTGrid ? cur.minTGrid : next.minTGrid, cur.maxTGrid >= next.maxTGrid ? cur.maxTGrid : next.maxTGrid);
                }
                else
                {
                    ranges[writeIndex++] = cur;
                    cur = next;
                }
            }

            ranges[writeIndex++] = cur;
            if (writeIndex < ranges.Count)
                ranges.RemoveRange(writeIndex, ranges.Count - writeIndex);
        }

        private static void MergeVisibleGridRangesInPlace(List<VisibleGridRange> ranges)
        {
            if (ranges.Count <= 1)
                return;

            ranges.Sort(VisibleGridRangeMinComparer.Instance);

            var writeIndex = 0;
            var cur = ranges[0];
            for (var readIndex = 1; readIndex < ranges.Count; readIndex++)
            {
                var next = ranges[readIndex];
                if (next.MinTotalGrid <= cur.MaxTotalGrid)
                {
                    cur = new VisibleGridRange(
                        cur.MinTotalGrid <= next.MinTotalGrid ? cur.MinTotalGrid : next.MinTotalGrid,
                        cur.MaxTotalGrid >= next.MaxTotalGrid ? cur.MaxTotalGrid : next.MaxTotalGrid);
                }
                else
                {
                    ranges[writeIndex++] = cur;
                    cur = next;
                }
            }

            ranges[writeIndex++] = cur;
            if (writeIndex < ranges.Count)
                ranges.RemoveRange(writeIndex, ranges.Count - writeIndex);
        }

        private static int AddLengthToTotalGrid(int baseTotalGrid, BPMChange bpm, double lengthMsec)
        {
            return baseTotalGrid + LengthToOffsetTotalGrid(bpm, lengthMsec);
        }

        private static int SubtractLengthFromTotalGrid(int baseTotalGrid, BPMChange bpm, double lengthMsec)
        {
            var totalGrid = baseTotalGrid - LengthToOffsetTotalGrid(bpm, lengthMsec);
            return totalGrid < 0 ? 0 : totalGrid;
        }

        private static int LengthToOffsetTotalGrid(BPMChange bpm, double lengthMsec)
        {
            var totalGrid = lengthMsec * (TGrid.DEFAULT_RES_T * bpm.BPM) / 240000;
            var p = totalGrid / TGrid.DEFAULT_RES_T;
            var unit = (int)p;
            var grid = (int)Math.Round((p - unit) * TGrid.DEFAULT_RES_T);
            return unit * (int)TGrid.DEFAULT_RES_T + grid;
        }

        private static double ConvertTotalGridToAudioMsec(int totalGrid, List<BpmList.BpmTimingPoint> bpmTimingPoints)
        {
            if (bpmTimingPoints.Count == 0)
                return 0;

            var firstBpm = bpmTimingPoints[0].Bpm;
            if (firstBpm is null || totalGrid < firstBpm.TGrid.TotalGrid)
                return 0;

            var lo = 0;
            var hi = bpmTimingPoints.Count - 1;
            while (lo <= hi)
            {
                var i = lo + ((hi - lo) >> 1);
                var bpm = bpmTimingPoints[i].Bpm;
                var bpmTotalGrid = bpm?.TGrid.TotalGrid ?? int.MaxValue;

                if (bpmTotalGrid <= totalGrid)
                    lo = i + 1;
                else
                    hi = i - 1;
            }

            var timingPoint = bpmTimingPoints[Math.Max(0, hi)];
            if (timingPoint.Bpm is null)
                return 0;

            var relativeGridUnit = TotalGridToTotalUnit(totalGrid) - timingPoint.Bpm.TGrid.TotalUnit;
            var relativeMsec = 240000d * (relativeGridUnit * TGrid.DEFAULT_RES_T) / (TGrid.DEFAULT_RES_T * timingPoint.Bpm.BPM);
            return (timingPoint.AudioTime + TimeSpan.FromMilliseconds(relativeMsec)).TotalMilliseconds;
        }

        private static double TotalGridToTotalUnit(int totalGrid)
        {
            const int resT = (int)TGrid.DEFAULT_RES_T;
            var unit = totalGrid / resT;
            var grid = totalGrid % resT;

            if (grid < 0)
            {
                unit--;
                grid += resT;
            }

            return unit + grid * 1.0 / resT;
        }

        private static IEnumerable<VisibleTGridRange> TryMerge(IEnumerable<VisibleTGridRange> sortedList)
        {
            using var itor = sortedList.OrderBy(x => x.minTGrid).GetEnumerator();
            if (!itor.MoveNext())
                yield break;

            var cur = itor.Current;
            while (itor.MoveNext())
            {
                var next = itor.Current;
                if (next.minTGrid <= cur.maxTGrid)
                    cur = new(cur.minTGrid <= next.minTGrid ? cur.minTGrid : next.minTGrid, cur.maxTGrid >= next.maxTGrid ? cur.maxTGrid : next.maxTGrid);
                else
                {
                    yield return cur;
                    cur = next;
                }
            }

            yield return cur;
        }

        public double CalculateSpeed(BpmList bpmList, TGrid t)
        {
            var soflan = LastOrDefaultByBinarySearch(GetCachedSoflanPositionList_PreviewMode(bpmList), t, x => x.TGrid);
            return soflan.Speed;
        }

        public IEnumerable<Soflan> GenerateDurationSoflans(BpmList bpmList, int soflanGroup)
        {
            var list = GetCachedSoflanPositionList_PreviewMode(bpmList)
                .Select(x => new { x.TGrid, x.Speed })
                .OrderBy(x => x.TGrid)
                .ToArray();

            for (var i = 0; i < list.Length - 1; i++)
            {
                yield return new Soflan()
                {
                    TGrid = list[i].TGrid,
                    Speed = (float)list[i].Speed,
                    EndTGrid = list[i + 1].TGrid,
                    SoflanGroup = soflanGroup
                };
            }
        }

        public IEnumerable<KeyframeSoflan> GenerateKeyframeSoflans(BpmList bpmList)
        {
            var list = DistinctContinuousBy(GetCachedSoflanPositionList_PreviewMode(bpmList)
                .Select(x => new { x.TGrid, x.Speed })
                .OrderBy(x => x.TGrid), x => x.Speed);

            foreach (var item in list)
            {
                yield return new KeyframeSoflan()
                {
                    TGrid = item.TGrid,
                    Speed = (float)item.Speed,
                };
            }
        }

        private static T LastOrDefaultByBinarySearch<T, TKey>(IList<T> source, TKey value, Func<T, TKey> keySelect)
            where TKey : IComparable<TKey>
        {
            var lo = 0;
            var hi = source.Count - 1;

            while (lo <= hi)
            {
                var i = lo + ((hi - lo) >> 1);
                var key = keySelect(source[i]);
                var order = key.CompareTo(value);

                if (order <= 0)
                    lo = i + 1;
                else
                    hi = i - 1;
            }

            return source[Math.Max(0, hi)];
        }

        private static IEnumerable<T> DistinctContinuousBy<T, TKey>(IEnumerable<T> collection, Func<T, TKey> keySelect)
        {
            using var itor = collection.GetEnumerator();
            var isFirst = true;
            var prevKey = default(TKey);
            var comparer = EqualityComparer<TKey>.Default;

            while (itor.MoveNext())
            {
                var value = itor.Current;
                var key = keySelect(value);

                if (isFirst || !comparer.Equals(prevKey, key))
                {
                    yield return value;
                    isFirst = false;
                }

                prevKey = key;
            }
        }

    }
}
