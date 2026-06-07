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
    
    public ITexture HitCircle { get; private set; }
    public ITexture HitCircleOverlay { get; private set; }
    public ITexture ApproachCircle { get; private set; }
    public ITexture Cursor { get; private set; }
    public ITexture CursorTrail { get; private set; }
    public ITexture CursorMiddle { get; private set; }
    public ITexture ReverseArrow { get; private set;  }
    public ITexture SliderFollowCircle { get; private set; }
    public ITexture SliderStartCircle { get; private set; }
    public ITexture SliderSpec { get; private set; }
    public ITexture ModAutoplay { get; private set; }
    public ITexture ModNightcore { get; private set; }
    public ITexture ModHidden { get; private set; }
    public List<ITexture> SliderBalls { get; private set; } = new();
    public ITexture[] DefaultNumbers { get; private set; } = new ITexture[10];
    public ITexture[] ScoreNumbers { get; private set; } = new ITexture[10];
    public ITexture InputOverlayBackground { get; private set; } 
    public ITexture InputOverlayKey { get; private set; } 
    public ITexture Hit300 { get; private set; }
    public ITexture Hit100 { get; private set; }
    public ITexture Hit50 { get; private set; }
    public ITexture Hit0 { get; private set; }
    public ITexture ScorebarBg { get; private set; }
    public ITexture ScorebarColour { get; private set; }
    public ITexture ScoreX { get; private set; }
    public ITexture ScorePercent { get; private set; }
    public ITexture PlayUnranked { get; private set; }
    public ITexture FollowPoint { get; private set; }
    public List<ITexture> FollowPoints { get; private set; } = new();
    
    public ITexture MenuBackground { get; private set; }
    public ITexture MenuButtonBackground { get; private set; }
    public AudioChannel MenuClick { get; private set;  }
    public AudioChannel MenuBack { get; private set;  }
    public AudioChannel PauseRetryClick { get; private set; }

    public SampleSet Normal { get; private set; }
    public SampleSet Soft { get; private set; }
    public SampleSet Drum { get; private set; }
    public AudioChannel ComboBreak { get; private set; }

    private IGraphicsDevice device;
    
    public Skin(IGraphicsDevice device, string folderPath)
    {
        this.device = device;
        Folder = folderPath;
        var parser = new IniDataParser();
        parser.Configuration.SkipInvalidLines = true;
        using (StreamReader sr = new StreamReader(Path.Combine(Folder, "skin.ini")))
        {
            IniData data = parser.Parse(sr);
            //string foo = data[""][""];
            
            
        }
        
        HitCircle = GetElementITexture("hitcircle", "hitcircle");
        HitCircleOverlay = GetElementITexture("hitcircleoverlay", "hitcircleoverlay");
        ApproachCircle = GetElementITexture("approachcircle", "hitcircleoverlay");
        Cursor = GetElementITexture("cursor", "cursor");
        CursorTrail = GetElementITexture("cursortrail", "cursor");
        ReverseArrow = GetElementITexture("reversearrow", "reversearrow");
        SliderFollowCircle = GetElementITexture("sliderfollowcircle", "sliderfollowcircle");
        SliderStartCircle = GetElementITexture("sliderstartcircle", "hitcircle");
        ModAutoplay = GetElementITexture("selection-mod-autoplay", "selection-mod-autoplay");
        ModNightcore = GetElementITexture("selection-mod-nightcore", "selection-mod-nightcore");
        ModHidden = GetElementITexture("selection-mod-hidden", "selection-mod-hidden");
        
        InputOverlayBackground = GetElementITexture("inputoverlay-background", "inputoverlay-background");
        InputOverlayKey = GetElementITexture("inputoverlay-key", "inputoverlay-key");
        Hit300 = GetElementITexture("hit300", "hit300");
        Hit100 = GetElementITexture("hit100", "hit100");
        Hit50 = GetElementITexture("hit50", "hit50");
        Hit0 = GetElementITexture("Hit0", "Hit0");
        ScorebarBg = GetElementITexture("scorebar-bg", "scorebar-bg");
        ScorebarColour = GetElementITexture("scorebar-colour", "scorebar-colour");
        ScoreX = GetElementITexture("score-x", "score-x");
        ScorePercent = GetElementITexture("score-percent", "score-percent");

        PlayUnranked = GetElementITexture("play-unranked", "play-unranked");

        MenuBackground = GetElementITexture("menu-background", "menu-background");
        MenuButtonBackground = GetElementITexture("menu-button-background", "menu-button-background");
        
        for (var i = 0; i < 10; i++)
            ScoreNumbers[i] = GetElementITexture($"score-{i}", $"score-{i}");
        
        for (var i = 0; i < 10; i++)
            DefaultNumbers[i] = GetElementITexture($"default-{i}", $"default-{i}");

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
                
                SliderBalls.Add(GetElementITexture($"sliderb{i}", "sliderb-nd"));
            }
        }
        else
        {
            SliderBalls.Add(GetElementITexture("sliderb", "sliderb-nd"));
        }

        if (ElementExists("cursormiddle"))
        {
            HasCursorMiddle = true;
            CursorMiddle = GetElementITexture("cursormiddle", "cursormiddle");
        }
        if (ElementExists("sliderb-spec"))
        {
            HasSliderSpec = true;
            SliderSpec = GetElementITexture("sliderb-spec", "sliderb-spec");
        }

        if (ElementExists("followpoint-0"))
        {
            HasAnimatedFollowPoints = true;
            for (int i = 0; ; i++)
            {
                if (!ElementExists($"followpoint-{i}"))
                    break;
                
                SliderBalls.Add(GetElementITexture($"followpoint-{i}", "followpoint"));
            }
            
        }
        else
        {
            FollowPoint = GetElementITexture("followpoint", "followpoint");
        }

        Normal = new SampleSet(Folder, SampleSet.SampleSetType.Normal);
        Soft = new SampleSet(Folder, SampleSet.SampleSetType.Soft);
        Drum = new SampleSet(Folder, SampleSet.SampleSetType.Drum);
        ComboBreak = GetSample("combobreak");
        MenuClick = GetSample("menuclick");
        MenuBack = GetSample("menuback");
        PauseRetryClick = GetSample("pause-retry-click");
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

    private ITexture GetElementITexture(string name, string fallback)
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
            return GetDefaultITexture(name, fallback);
        }

        return device.CreateTexture($"{Folder}/{element}");
    }

    private ITexture GetDefaultITexture(string name, string fallback)
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

        return device.CreateTexture(Path.Combine(Resources.DefaultSkinPath, element));
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
        
        MenuBackground.Dispose();
        MenuButtonBackground.Dispose();
        MenuBack.Dispose();
        MenuClick.Dispose();
        PauseRetryClick.Dispose();

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