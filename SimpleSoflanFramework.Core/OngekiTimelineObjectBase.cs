using OngekiFumenEditor.Core.Utils;
using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Core.Base
{
    public abstract class OngekiTimelineObjectBase : OngekiObjectBase, ITimelineObject, IDisposable
    {
        private TGrid tGrid = new TGrid();

        public virtual TGrid TGrid
        {
            get => tGrid;
            set
            {
                this.RegisterOrUnregisterPropertyChangeEvent(tGrid, value);
                tGrid = value;
                NotifyOfPropertyChange(nameof(TGrid));
            }
        }

        private bool isSelecting = false;

        public virtual bool IsSelected
        {
            get => isSelecting;
            set => Set(ref isSelecting, value);
        }

        public virtual bool CheckVisiable(TGrid minVisibleTGrid, TGrid maxVisibleTGrid)
        {
            return minVisibleTGrid <= TGrid && TGrid <= maxVisibleTGrid;
        }

        public int CompareTo(ITimelineObject obj)
        {
            return TGrid.CompareTo(obj?.TGrid);
        }

        public override void Copy(OngekiObjectBase fromObj)
        {
            if (fromObj is not OngekiTimelineObjectBase timelineObject)
                return;

            TGrid = timelineObject.TGrid;
        }

        public override string ToString() => $"{base.ToString()} {TGrid}";

        public virtual void Dispose()
        {
            TGrid = default;
        }
    }
}

