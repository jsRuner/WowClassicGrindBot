using SixLabors.ImageSharp;
using SharedLib.Extensions;
using SharedLib.NpcFinder;

namespace Core.Goals;

public sealed class NpcNameTargetingLocations
{
    public Point[] Targeting { get; init; }
    public Point[] FindBy { get; init; }

    public NpcNameTargetingLocations(NpcNameFinder npcNameFinder)
    {
        float w = npcNameFinder.ScaleToRefWidth;
        float h = npcNameFinder.ScaleToRefHeight;

        Targeting = new Point[]
        {
            new Point(0, -2),
            new Point(-13, 8).Scale(w, h),
            new Point(13, 8).Scale(w, h),
        };

        FindBy = new Point[]
        {
            new Point(0, 0),
            new Point(0, 15).Scale(w, h),

            new Point(0, 50).Scale(w, h),
            new Point(-15, 50).Scale(w, h),
            new Point(15, 50).Scale(w, h),

            new Point(0, 100).Scale(w, h),
            new Point(-15, 100).Scale(w, h),
            new Point(15, 100).Scale(w, h),

            new Point(0, 150).Scale(w, h),
            new Point(-15, 150).Scale(w, h),
            new Point(15, 150).Scale(w, h),

            new Point(0,   200).Scale(w, h),
            new Point(-15, 200).Scale(w, h),
            new Point(-15, 200).Scale(w, h),
        };
    }
}
