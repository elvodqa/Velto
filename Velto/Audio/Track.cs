using SDL;
using Velto.Core;
using static SDL.SDL3;
using static SDL.SDL3_mixer;

namespace Velto.Audio;

public unsafe class Track : IDisposable
{
    public MIX_Track* Handle;
    // Audio that is currently assigned to the track
    public AudioChannel? Audio;
    
    // Position of the track in milliseconds
    public double Position
    {
        get => MIX_TrackFramesToMS(Handle, MIX_GetTrackPlaybackPosition(Handle));
        set => MIX_SetTrackPlaybackPosition(Handle, MIX_TrackMSToFrames(Handle, (long)value));
    }

    public double Length => MIX_TrackFramesToMS(Handle, MIX_GetAudioDuration(Audio!.Handle)); // or similar

    public float Volume
    {
        get => MIX_GetTrackGain(Handle);
        set => MIX_SetTrackGain(Handle, value);
    }

    public void Play()
    {
        if (Audio == null || Audio.Handle == null)
        {
            Logger.Instance.Error("Cannot play track: No Audio assigned!");
            return;
        }

        // This is the missing critical call!
        if (!MIX_SetTrackAudio(Handle, Audio.Handle))
        {
            Logger.Instance.Error($"Failed to set audio on track: {SDL_GetError()}");
            return;
        }

        var props = SDL_CreateProperties();
        // Optional: set looping, fade-in, etc.
        // SDL_SetBooleanProperty(props, MIX_PROP_PLAY_LOOP, true); // or number of loops

        if (!MIX_PlayTrack(Handle, props))
        {
            Logger.Instance.Error($"Failed to play track: {SDL_GetError()}");
        }

        SDL_DestroyProperties(props);
    }

    public float Speed
    {
        get
        {
            return MIX_GetTrackFrequencyRatio(Handle);
        }
        set
        {
            MIX_SetTrackFrequencyRatio(Handle, value);
        }
    }
    
    public bool Paused => MIX_TrackPaused(Handle);
    public bool Playing => MIX_TrackPlaying(Handle);

    public void Pause()
    {
        if (!MIX_PauseTrack(Handle))
        {
            Logger.Instance.Error($"Error pausing track: {SDL_GetError()}");
        }
    }
    
    public void Resume()
    {
        if (!MIX_ResumeTrack(Handle))
        {
            Logger.Instance.Error($"Error resuming track: {SDL_GetError()}");
        }
    }

    // fading time in ms
    public void Stop(float fadeOutFrames = 0)
    {
        SDL_AudioSpec spec;
        int sampleFrames = 0;
        SDL_GetAudioDeviceFormat(SDL_AUDIO_DEVICE_DEFAULT_PLAYBACK, &spec, &sampleFrames);
        int sampleRate = spec.freq;
            
        int frames = (int)(fadeOutFrames * sampleRate / 1000.0);
        if (!MIX_StopTrack(Handle, frames))
        {
            Logger.Instance.Error($"Error stopping track: {SDL_GetError()}");
        }
    }
    
    public void Dispose()
    {
        AudioManager.Instance.Tracks.Remove(this);
        MIX_DestroyTrack(Handle);
    }
}