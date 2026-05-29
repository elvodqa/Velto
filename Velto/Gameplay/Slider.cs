using System.Collections.Generic;
using OpenTK.Mathematics;

namespace Velto.Gameplay;

public enum CurveType : byte
{
    Catmull = 0,
    Bezier = 1,
    Linear = 2,
    Perfect = 3,
}

public class CurvePoint
{
    public Vector2 Position;
    public CurveType Type;
}

public class Slider : HitObject
{
    public List<CurvePoint> CurvePoints = new();
    public int SlideRepeatCount;
    public double Length;
    public int EdgeSounds;
    public int EdgeSets;

    public List<Vector2> Points = new();
}