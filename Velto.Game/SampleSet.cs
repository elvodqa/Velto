using Velto.Audio;
using Velto.Core;
using Velto.Graphics;

namespace Velto.Game;

public class SampleSet : IDisposable
{
    public enum SampleSetType 
    {
        Normal,
        Soft,
        Drum
    }
    
    public SampleSetType Type { get; private set; }
    public AudioChannel HitClap { get; private set; }
    public AudioChannel HitFinish { get; private set; }
    public AudioChannel HitNormal { get; private set; }
    public AudioChannel HitWhistle { get; private set; }
    public AudioChannel SliderSlide { get; private set; }
    public AudioChannel SliderTick { get; private set; }
    public AudioChannel SliderWhistle { get; private set; }
    public string Folder  { get; private set; }
    

    // directory containing the files
    public SampleSet(string directory, SampleSetType type)
    {
        Folder = directory;
        Type = type;

        HitClap = GetSample("hitclap");
        HitFinish = GetSample("hitfinish");
        HitNormal = GetSample("hitnormal");
        HitWhistle = GetSample("hitwhistle");
        SliderSlide = GetSample("sliderslide");
        SliderTick = GetSample("slidertick");
        SliderWhistle = GetSample("sliderwhistle");
    }
    
    private string TypeString => Type switch
    {
        SampleSetType.Normal => "normal",
        SampleSetType.Soft => "soft",
        SampleSetType.Drum => "drum",
        _ => throw new ArgumentOutOfRangeException()
    };

    private bool CheckFile(string name, out string extension)
    {
        string wavPath = Path.Combine(Folder, $"{TypeString}-{name}.wav");
        string oggPath = Path.Combine(Folder, $"{TypeString}-{name}.ogg");

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
        if (CheckFile(name, out var extension))
        {
            return AudioManager.Instance.LoadAudio(Path.Combine(Folder, $"{TypeString}-{name}{extension}"));
        }
        return AudioManager.Instance.LoadAudio(Path.Combine(Resources.DefaultSkinPath, $"{TypeString}-{name}.wav"));
    }
    
    public void Dispose()
    {
        HitClap.Dispose();
        HitFinish.Dispose();
        HitNormal.Dispose();
        HitWhistle.Dispose();
        SliderSlide.Dispose();
        SliderTick.Dispose();
        SliderWhistle.Dispose();
    }
}