using IniParser;
using OpenTK.Mathematics;
using Velto.Audio;
using Velto.Core;
using Velto.Graphics;

namespace Velto.Game;


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
    public List<Vector4> Colors { get; private set; }
    public bool HasCursorMiddle = false;
    public bool HasSliderSpec = false;
    public bool HasAnimatedFollowPoints = false;

    
    public string Folder { get; private set; }  
    
    public Texture HitCircle { get; private set; }
    public Texture HitCircleOverlay { get; private set; }
    public Texture ApproachCircle { get; private set; }
    public Texture Cursor { get; private set; }
    public Texture CursorTrail { get; private set; }
    public Texture CursorMiddle { get; private set; }
    public Texture ReverseArrow { get; private set;  }
    public Texture SliderFollowCircle { get; private set; }
    public Texture SliderStartCircle { get; private set; }
    public Texture SliderSpec { get; private set; }
    public Texture ModAutoplay { get; private set; }
    public Texture ModNightcore { get; private set; }
    public Texture ModHidden { get; private set; }
    public List<Texture> SliderBalls { get; private set; } = new();
    public Texture[] DefaultNumbers { get; private set; } = new Texture[10];
    public Texture[] ScoreNumbers { get; private set; } = new Texture[10];
    public Texture InputOverlayBackground { get; private set; } 
    public Texture InputOverlayKey { get; private set; } 
    public Texture Hit300 { get; private set; }
    public Texture Hit100 { get; private set; }
    public Texture Hit50 { get; private set; }
    public Texture Hit0 { get; private set; }
    public Texture ScorebarBg { get; private set; }
    public Texture ScorebarColour { get; private set; }
    public Texture ScoreX { get; private set; }
    public Texture ScorePercent { get; private set; }
    public Texture PlayUnranked { get; private set; }
    public Texture FollowPoint { get; private set; }
    public List<Texture> FollowPoints { get; private set; } = new();

    public SampleSet Normal { get; private set; }
    public SampleSet Soft { get; private set; }
    public SampleSet Drum { get; private set; }
    public AudioChannel ComboBreak { get; private set; }
    
  
    
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
        ModNightcore = GetElementTexture("selection-mod-nightcore", "selection-mod-nightcore");
        ModHidden = GetElementTexture("selection-mod-hidden", "selection-mod-hidden");
        
        InputOverlayBackground = GetElementTexture("inputoverlay-background", "inputoverlay-background");
        InputOverlayKey = GetElementTexture("inputoverlay-key", "inputoverlay-key");
        Hit300 = GetElementTexture("hit300", "hit300");
        Hit100 = GetElementTexture("hit100", "hit100");
        Hit50 = GetElementTexture("hit50", "hit50");
        Hit0 = GetElementTexture("Hit0", "Hit0");
        ScorebarBg = GetElementTexture("scorebar-bg", "scorebar-bg");
        ScorebarColour = GetElementTexture("scorebar-colour", "scorebar-colour");
        ScoreX = GetElementTexture("score-x", "score-x");
        ScorePercent = GetElementTexture("score-percent", "score-percent");
        
        PlayUnranked = GetElementTexture("play-unranked", "play-unranked");
        
        for (var i = 0; i < 10; i++)
            ScoreNumbers[i] = GetElementTexture($"score-{i}", $"score-{i}");
        
        for (var i = 0; i < 10; i++)
            DefaultNumbers[i] = GetElementTexture($"default-{i}", $"default-{i}");

        if (Path.GetFileName(DefaultNumbers[0].Path).Contains("@2x")) NumbersHd = true; 
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

        if (ElementExists("cursormiddle"))
        {
            HasCursorMiddle = true;
            CursorMiddle = GetElementTexture("cursormiddle", "cursormiddle");
        }
        if (ElementExists("sliderb-spec"))
        {
            HasSliderSpec = true;
            SliderSpec = GetElementTexture("sliderb-spec", "sliderb-spec");
        }

        if (ElementExists("followpoint-0"))
        {
            HasAnimatedFollowPoints = true;
            for (int i = 0; ; i++)
            {
                if (!ElementExists($"followpoint-{i}"))
                    break;
                
                SliderBalls.Add(GetElementTexture($"followpoint-{i}", "followpoint"));
            }
            
        }
        else
        {
            FollowPoint = GetElementTexture("followpoint", "followpoint");
        }

        Normal = new SampleSet(Folder, SampleSet.SampleSetType.Normal);
        Soft = new SampleSet(Folder, SampleSet.SampleSetType.Soft);
        Drum = new SampleSet(Folder, SampleSet.SampleSetType.Drum);
        ComboBreak = GetSample("combobreak");
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
        if (File.Exists(Path.Combine(Folder, $"{name}@2x.png")))
        {
            element = $"{name}@2x.png";
        } 
        else if (File.Exists(Path.Combine(Folder, $"{name}.png")))
        {
            element = $"{name}.png";
        }
        else if (File.Exists(Path.Combine(Folder, $"{fallback}.png")))
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
    
    private bool CheckAudio(string name, out string extension)
    {
        string wavPath = Path.Combine(Folder, $"{name}.wav");
        string oggPath = Path.Combine(Folder, $"{name}.ogg");

        if (File.Exists(wavPath))
        {
            extension = ".wav";
            return true;
        }

        if (File.Exists(oggPath))
        {
            extension = ".ogg";
            return true;
        }

        extension = string.Empty;
        return false;
    }
    
    private AudioChannel GetSample(string name)
    {
        if (CheckAudio(name, out var extension))
        {
            return AudioManager.Instance.LoadAudio(Path.Combine(Folder, $"{name}{extension}"));
        }
        return AudioManager.Instance.LoadAudio(Path.Combine(Resources.DefaultSkinPath, $"{name}.wav"));
    }

    public void Dispose()
    {
        HitCircle?.Dispose();
        HitCircleOverlay?.Dispose();
        ApproachCircle?.Dispose();
        Cursor?.Dispose();
        CursorTrail?.Dispose();
        CursorMiddle?.Dispose();
        ReverseArrow?.Dispose();
        SliderFollowCircle?.Dispose();
        SliderStartCircle?.Dispose();
        SliderSpec?.Dispose();
        ModAutoplay?.Dispose();
        ModNightcore?.Dispose();
        ModHidden?.Dispose();
        PlayUnranked?.Dispose();
        FollowPoint?.Dispose();

        InputOverlayBackground?.Dispose();
        InputOverlayKey?.Dispose();

        Hit300?.Dispose();
        Hit100?.Dispose();
        Hit50?.Dispose();
        Hit0?.Dispose();

        ScorebarBg?.Dispose();
        ScorebarColour?.Dispose();
        ScoreX?.Dispose();
        ScorePercent?.Dispose();
        
        Normal.Dispose();
        Soft.Dispose();
        Drum.Dispose();
        ComboBreak.Dispose();
        
        foreach (var texture in FollowPoints)
            texture?.Dispose();

        foreach (var texture in SliderBalls)
            texture?.Dispose();

        foreach (var texture in DefaultNumbers)
            texture?.Dispose();

        foreach (var texture in ScoreNumbers)
            texture?.Dispose();
    }
}