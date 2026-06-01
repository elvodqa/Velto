using IniParser;
using OpenTK.Mathematics;
using Velto.Graphics;

namespace Velto.Core;

public class Skin : IDisposable
{
    public List<Vector4> HitCircleColors = new();
    public int SliderBallFlip = 0;
    public int SliderBallFrames = 1; // if 1, load sliderb.png
    public Vector3 SliderBorderColor = new(1, 1, 1);
    public Vector3 SliderBallColor = new(2f/255, 170f/255, 1f);
    public bool SliderBallAnimated = false;
    public bool SliderStartCircleExists = false;
    public bool NumbersHd = false;
    
    public string Folder { get; private set; }  
    
    public Texture HitCircle { get; private set; }
    public Texture HitCircleOverlay { get; private set; }
    public Texture ApproachCircle { get; private set; }
    public Texture Cursor { get; private set; }
    public Texture CursorTrail { get; private set; }
    public Texture ReverseArrow { get; private set;  }
    public Texture SliderFollowCircle { get; private set; }
    public Texture SliderStartCircle { get; private set; }
    public Texture ModAutoplay { get; private set; }
    public List<Texture> SliderBalls { get; private set; } = new();
    public Texture[] Numbers { get; private set; } = new Texture[10];
    public Texture InputOverlayBackground { get; private set; } 
    public Texture InputOverlayKey { get; private set; } 
    public Texture Hit300 { get; private set; }
    public Texture Hit100 { get; private set; }
    public Texture Hit50 { get; private set; }
    public Texture Hit0 { get; private set; }
    public Texture ScorebarBg { get; private set; }
    public Texture ScorebarColour { get; private set; }
    
    
    public List<Vector4> Colors { get; private set; }
    
    
    public Skin(string folderPath)
    {
        Folder = folderPath;
        var parser = new IniDataParser();
        parser.Configuration.SkipInvalidLines = true;
        using (StreamReader sr = new StreamReader(Path.Combine(Folder, "skin.ini")))
        {
            IniData data = parser.Parse(sr);
            //string foo = data[""][""];
            
            
        }
        
        HitCircle = GetElementTexture("hitcircle", "hitcircle");
        HitCircleOverlay = GetElementTexture("hitcircleoverlay", "hitcircleoverlay");
        ApproachCircle = GetElementTexture("approachcircle", "hitcircleoverlay");
        Cursor = GetElementTexture("cursor", "cursor");
        CursorTrail = GetElementTexture("cursortrail", "cursor");
        ReverseArrow = GetElementTexture("reversearrow", "reversearrow");
        SliderFollowCircle = GetElementTexture("sliderfollowcircle", "sliderfollowcircle");
        SliderStartCircle = GetElementTexture("sliderstartcircle", "hitcircle");
        ModAutoplay = GetElementTexture("selection-mod-autoplay", "selection-mod-autoplay");
        InputOverlayBackground = GetElementTexture("inputoverlay-background", "inputoverlay-background");
        InputOverlayKey = GetElementTexture("inputoverlay-key", "inputoverlay-key");
        Hit300 = GetElementTexture("hit300", "hit300");
        Hit100 = GetElementTexture("hit100", "hit100");
        Hit50 = GetElementTexture("hit50", "hit50");
        Hit0 = GetElementTexture("Hit0", "Hit0");
        ScorebarBg = GetElementTexture("scorebar-bg", "scorebar-bg");
        ScorebarColour = GetElementTexture("scorebar-colour", "scorebar-colour");
        
        
        
        for (var i = 0; i < 10; i++)
            Numbers[i] = GetElementTexture($"default-{i}", $"default-{i}");

        if (Path.GetFileName(Numbers[0].Path).Contains("@2x")) NumbersHd = true; 
        if (Path.GetFileName(SliderStartCircle.Path).Contains("sliderfollowcircle")) SliderStartCircleExists = true; 
        
        // Animated elements, ref: https://osu.ppy.sh/wiki/en/Skinning/osu
        if (ElementExists("sliderb0"))
        {
            SliderBallAnimated = true;

            for (int i = 0; ; i++)
            {
                if (!ElementExists($"sliderb{i}"))
                    break;
                
                SliderBalls.Add(GetElementTexture($"sliderb{i}", "sliderb-nd"));
            }
        }
        else
        {
            SliderBalls.Add(GetElementTexture("sliderb", "sliderb-nd"));
        }
    }

    private Vector4 ParseColor(string str)
    {
        var split = str.Trim().Split(",");
        return new(
            int.Parse(split[0]) / 255f,
            int.Parse(split[1]) / 255f,
            int.Parse(split[2]) / 255f,
            1
        );
    }

    private bool ElementExists(string element)
    {
        return File.Exists($"{Folder}/{element}.png") || File.Exists($"{Folder}/{element}@2x.png");
    }

    private Texture GetElementTexture(string name, string fallback)
    {
        string element;
        // check @2x
        if (File.Exists($"{Folder}/{name}@2x.png"))
        {
            element = $"{name}@2x.png";
        } 
        else if (File.Exists($"{Folder}/{name}.png"))
        {
            element = $"{name}.png";
        }
        else if (File.Exists($"{Folder}/{fallback}.png"))
        {
            element = fallback + ".png";
        }
        else
        {
            return GetDefaultTexture(name, fallback);
        }

        return new Texture($"{Folder}/{element}");
    }

    private Texture GetDefaultTexture(string name, string fallback)
    {
        string element;
        // check @2x
        if (File.Exists(Path.Combine(Resources.DefaultSkinPath, $"{name}@2x.png")))
        {
            element = $"{name}@2x.png";
        } 
        else if (File.Exists(Path.Combine(Resources.DefaultSkinPath, $"{name}.png")))
        {
            element = $"{name}.png";
        }
        else
        {
            element = fallback + ".png";
        }

        return new Texture(Path.Combine(Resources.DefaultSkinPath, element));
    }

    public void Dispose()
    {
        
    }
}