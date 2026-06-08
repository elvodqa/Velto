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
    public readonly Queue<Track> SampleTracks = new();
    private const int MaxSamples = 50;
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
        lock (_lock)
        {
            while (SampleTracks.Count >= MaxSamples)
            {
                var oldTrack = SampleTracks.Dequeue();

                try
                {
                    oldTrack.Stop(); // if Track supports stopping
                }
                catch
                {
                    // ignore
                }

                oldTrack.Dispose();
            }

            Track track = CreateTrack();
            track.Audio = audioChannel;
            track.Volume = SampleVolume;
            track.Play();

            SampleTracks.Enqueue(track);
        }
    }

    public void Update(double delta)
    {
        _counter += delta;

        if (_counter < 100)
            return;

        _counter = 0;

        lock (_lock)
        {
            int count = SampleTracks.Count;

            for (int i = 0; i < count; i++)
            {
                var track = SampleTracks.Dequeue();

                if (track.Playing)
                {
                    SampleTracks.Enqueue(track);
                }
                else
                {
                    track.Dispose();
                }
            }
        }
    }
    
    public void Dispose()
    {
        MIX_DestroyMixer(_mixer);
    }
}