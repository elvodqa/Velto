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
    
    
    
    
    public Framebuffer SliderFramebuffer;
    public Vector2 CacheOffset;
    public (float[] vbo, uint[] ibo) VboData, IboData;
    public BufferObject<float> Vbo;
    public BufferObject<uint> Ebo;
    public VertexArrayObject<float, uint> Vao;
    public int IndexCount;
}