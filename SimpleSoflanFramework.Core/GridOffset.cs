using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Core.Base
{
    public struct GridOffset
    {
        public float Unit;
        public int Grid;

        public GridOffset(float unit, int grid)
        {
            Unit = unit;
            Grid = grid;
        }

        public static GridOffset Zero { get; } = new GridOffset(0, 0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int TotalGrid(uint gridRadix) => (int)(Unit * gridRadix + Grid);
    }
}
