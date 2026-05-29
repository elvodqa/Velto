using OpenTK.Mathematics;

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
    Perfect,
    Good,
    Ok,
    Meh,
    Miss
}

public class HitObject
{
    public int ComboNumber;
    public bool Failed = false;
    public HitResult HitResult = HitResult.None;
    public Vector2 Position { get; set; }
    public int Time { get; set; }
    public bool NewCombo { get; set; } = false;
    public Vector4 Color { get; set; }
}