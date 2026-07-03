using OngekiFumenEditor.Core.Base;
using OngekiFumenEditor.Core.Base.Collections;
using OngekiFumenEditor.Core.Base.OngekiObjects;
using OngekiFumenEditor.Core.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Core.Modules.FumenVisualEditor
{
    public static class TGridCalculator
    {
        #region Frame -> AudioTime

        public const float FRAME_DURATION = 16.666666f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TimeSpan ConvertFrameToAudioTime(float frame)
           => TimeSpan.FromMilliseconds(FRAME_DURATION * frame);

        #endregion

        #region AudioTime -> TGrid

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TGrid ConvertAudioTimeToTGrid(TimeSpan audioTime, BpmList bpmList)
        {
            var positionBpmList = bpmList.GetCachedAllBpmUniformPositionList();

            var bpmTimingPoint = positionBpmList.LastOrDefault(x => x.AudioTime <= audioTime);
            var pickBpm = bpmTimingPoint.Bpm;
            var pickStartY = bpmTimingPoint.AudioTime;

            if (pickBpm is null)
                return default;
            var relativeBpmLenOffset = pickBpm.LengthConvertToOffset((audioTime - pickStartY).TotalMilliseconds);

            var pickTGrid = pickBpm.TGrid + relativeBpmLenOffset;
            return pickTGrid;
        }

        #endregion

        #region TGrid -> AudioTime

        public static TimeSpan ConvertTGridToAudioTime(TGrid tGrid, BpmList bpmList)
        {
            var positionBpmList = bpmList.GetCachedAllBpmUniformPositionList();

            var bpmTimingPoint = positionBpmList.LastOrDefault(x => x.Bpm.TGrid <= tGrid);
            var audioTimeMsecBase = bpmTimingPoint.AudioTime;
            var pickBpm = bpmTimingPoint.Bpm;

            if (pickBpm is null)
                if (positionBpmList.FirstOrDefault().Bpm?.TGrid is TGrid first && tGrid < first)
                    return TimeSpan.FromMilliseconds(0);
                else
                    return default;
            var relativeBpmLenOffset = TimeSpan.FromMilliseconds(MathUtils.CalculateBPMLength(pickBpm, tGrid));

            var audioTimeMsec = audioTimeMsecBase + relativeBpmLenOffset;
            return audioTimeMsec;
        }

        #endregion

        #region [PreviewMode] Y -> TGrid[]

        public static IEnumerable<TGrid> ConvertYToTGrid_PreviewMode(double pickY, SoflanList soflanList, BpmList bpmList, double scale)
        {
            var r = soflanList.GetVisibleRanges_PreviewMode(pickY, 0, 0, bpmList, scale);
            var result = r.OrderBy(x => x.minTGrid).Select(x => x.minTGrid);
            return result;
        }

        #endregion

        #region [PreviewMode] TGrid -> Y

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ConvertTGridToY_PreviewMode(TGrid tGrid, SoflanList soflanList, BpmList bpmList, double scale)
            => ConvertTGridUnitToY_PreviewMode(tGrid.TotalUnit, soflanList, bpmList, scale);

        public static double ConvertTGridUnitToY_PreviewMode(double tGridUnit, SoflanList soflanList, BpmList bpmList, double scale)
        {
            var positionBpmList = soflanList.GetCachedSoflanPositionList_PreviewMode(bpmList);

            var pos = positionBpmList.LastOrDefaultByBinarySearch(tGridUnit, x => x.TGrid.TotalUnit);
            if (pos.Bpm is null)
                return default;

            var relativeBpmLenOffset = MathUtils.CalculateBPMLength(pos.TGrid.TotalUnit, tGridUnit, pos.Bpm.BPM);
            var speed = pos.Speed;

            var y = (pos.Y + relativeBpmLenOffset * speed) * scale;
            return y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ConvertAudioTimeToY_PreviewMode(TimeSpan audioTime, SoflanList soflanList, BpmList bpmList, double scale)
            => ConvertTGridToY_PreviewMode(ConvertAudioTimeToTGrid(audioTime, bpmList), soflanList, bpmList, scale);

        #endregion
    }
}

