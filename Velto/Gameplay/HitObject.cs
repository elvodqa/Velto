using OpenTK.Mathematics;

namespace Velto.Gameplay;

[Flags]
public enum HitObjectType : byte
{
    None        = 0,

    Circle      = 1 << 0, // 00000001
    Slider      = 1 << 1, // 00000010
    NewCombo    = 1 << 2, // 00000100
    Spinner     = 1 << 3, // 00001000

    ManiaHold   = 1 << 7   // 10000000
}


public class HitObject
{
    public Vector2 Position { get; set; }
    public int Time { get; set; }
    public bool NewCombo { get; set; } = false;
    
    internal bool Failed = false;
    
    // prepass info
    public Vector4 Color { get; set; }
    internal int ComboNumber;
    
    
    
}


public static class HitObjectParser
{
    const byte ComboSkipMask = 0b0111_0000;
    
    
    public static void Parse(byte value)
    {
        var flags = (HitObjectType)(value & 0b1000_1111); 
        // keep bits 0–3 and 7 as enum flags

        int comboSkip = (value & ComboSkipMask) >> 4;

        bool isCircle = flags.HasFlag(HitObjectType.Circle);
        bool isSlider = flags.HasFlag(HitObjectType.Slider);
        bool isNewCombo = flags.HasFlag(HitObjectType.NewCombo);
        bool isSpinner = flags.HasFlag(HitObjectType.Spinner);
        bool isHold = flags.HasFlag(HitObjectType.ManiaHold);

        //Console.WriteLine($"Flags: {flags}");
        //Console.WriteLine($"Combo skip: {comboSkip}");
    }
}