using OpenTK.Mathematics;
using Velto.Graphics;

namespace Velto.Gameplay;

[Flags]
public enum HitObjectType : byte
{
    None = 0,

    Circle = 1 << 0, // 00000001
    Slider = 1 << 1, // 00000010
    NewCombo = 1 << 2, // 00000100
    Spinner = 1 << 3, // 00001000

    ManiaHold = 1 << 7 // 10000000
}

public enum HitResult
{
    None,
    Good,
    Ok,
    Meh,
    Miss
}

public abstract class HitObject
{
    public int ComboNumber;
    public bool Failed = false;
    public HitResult HitResult = HitResult.None;
    public double HitTime = -1;
    public Vector2 Position { get; set; }
    public double Time { get; set; }
    public bool NewCombo { get; set; } = false;
    public Color4<Rgba> Color { get; set; }
    
    // public abstract void Draw(double dt, Renderer renderer, Vector2 playfieldTopLeft, float scale, double songCursor, 
    //     double startingTimer, float baseCircleSize, Texture approachCircleTexture,
    //     Texture hitcircleTexture, Texture hitcircleOverlayTexture, Dictionary<int, Texture> numberTextures);
}