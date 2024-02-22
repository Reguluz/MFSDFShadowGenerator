namespace Moonflow
{
    public struct MFSDFPoint
    {
        public int x;
        public int y;
        public int dist => x * x + y * y;

        public MFSDFPoint(int t)
        {
            x = t;
            y = t;
        }
    }
}