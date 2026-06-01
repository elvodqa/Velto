using SDL;
using Velto.Core;
using static SDL.SDL3;
using static SDL.SDL3_mixer;

namespace Velto.Audio;


public unsafe class AudioManager : IDisposable
{
    private static readonly Lazy<AudioManager> _instance =
        new(() => new AudioManager());

    public static AudioManager Instance => _instance.Value;

    private readonly object _lock = new();
    
    public List<Track> Tracks = new();
    public List<AudioChannel> Audios = new();
    
    private MIX_Mixer* _mixer;
    public List<Track> SampleTracks = new();
    
    public float SampleVolume { get; set; }
    private double _counter = 0;
    
    public AudioManager()
    {
        _mixer = MIX_CreateMixerDevice(SDL_AUDIO_DEVICE_DEFAULT_PLAYBACK, null);
        if (_mixer == null)
        {
            throw new Exception();
        }
    }

    public AudioChannel LoadAudio(string path)
    {
        AudioChannel audioChannel = new AudioChannel();
        audioChannel.Path = path;
        audioChannel.Handle = MIX_LoadAudio(_mixer, audioChannel.Path, false);
        Audios.Add(audioChannel);
        return audioChannel;
    }

    public Track CreateTrack()
    {
        Track track = new Track();
        track.Handle = MIX_CreateTrack(_mixer);
        track.Audio = null;
        Tracks.Add(track);
        return track;
    }

    public void PlaySample(AudioChannel audioChannel)
    {
        Track track = CreateTrack();
        track.Audio = audioChannel;
        track.Volume = SampleVolume;
        track.Play();
        SampleTracks.Add(track);
    }

    public void Update(double delta)
    {
        _counter += delta;

        if (_counter < 1000)
            return;

        _counter = 0;

        foreach (var track in SampleTracks.ToList())
        {
            if (!track.Playing)
            {
                SampleTracks.Remove(track);
                track.Dispose();
            }
        }
    }
    
    public void Dispose()
    {
        MIX_DestroyMixer(_mixer);
    }
}