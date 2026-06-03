using OpenTK.Mathematics;
using Velto.Graphics;

namespace Velto.Gameplay;

public enum CurveType : byte
{
    Catmull = 0,
    Bezier = 1,
    Linear = 2,
    Perfect = 3
}

public class CurvePoint
{
    public Vector2 Position;
    public CurveType Type;
}

public class Slider : HitObject
{
    public List<CurvePoint> CurvePoints = new();
    public int EdgeSets;
    public int EdgeSounds;
    public double Length;
    public double Duration = 0;
    public List<Vector2> Points = new();
    public int SlideRepeatCount;
    public Vector2 BallPosition = new();
    public bool Sliding = false;
    public int BallTarget = 0;
    public bool JudgementDone = false;
    public double TotalFollowTime { get; set; } = 0.0;     // Total time successfully followed
    public double LastFollowUpdate { get; set; } = 0.0;    // Last time we updated follow time
    public bool WasFollowingPreviousFrame { get; set; } = false;
    public double LongestContinuousFollow { get; set; } = 0.0;
    public double CurrentContinuousFollow { get; set; } = 0.0;
    
    public bool WasFollowedAtEnd { get; set; } = false;  
    public bool IsCurrentlyBeingFollowed { get; set; } = false;
    public double LastHeld = 0;

    public static double FORGIVING_TIME = 50f;
    
    
    public Framebuffer SliderFramebuffer;
    public Vector2 CacheOffset;
    public (float[] vbo, uint[] ibo) VboData, IboData;
    public BufferObject<float> Vbo;
    public BufferObject<uint> Ebo;
    public VertexArrayObject<float, uint> Vao;
    public int IndexCount;

    // Duration for a single traversal of the path (i.e., one "span"), in milliseconds.
    public double SpanDuration;

    public Vector2 GetPositionAt(double songCursor)
    {
        if (Points.Count == 0)
            return Position;

        if (Duration <= 0)
            return Points[^1];

        var localTime = songCursor - Time;

        if (localTime <= 0)
            return Points[0];

        if (localTime >= Duration)
            return (Math.Max(1, SlideRepeatCount) % 2 == 0) ? Points[0] : Points[^1];

        var spans = Math.Max(1, SlideRepeatCount);
        var spanDuration = Duration / spans;
        if (spanDuration <= 0)
            return Points[0];

        var spanIndex = (int)Math.Floor(localTime / spanDuration);
        spanIndex = Math.Clamp(spanIndex, 0, spans - 1);

        var t = (localTime - spanIndex * spanDuration) / spanDuration;
        t = Math.Clamp(t, 0.0, 1.0);

        // odd spans traverse backwards (ping-pong)
        if ((spanIndex & 1) == 1)
            t = 1.0 - t;

        var idxF = t * (Points.Count - 1);
        var idx0 = (int)Math.Floor(idxF);
        idx0 = Math.Clamp(idx0, 0, Points.Count - 1);
        var idx1 = Math.Min(idx0 + 1, Points.Count - 1);

        var frac = (float)(idxF - idx0);
        return Vector2.Lerp(Points[idx0], Points[idx1], frac);
    }
}
