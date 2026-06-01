using SDL;
using static SDL.SDL3;
using static SDL.SDL3_mixer;

namespace Velto.Audio;

public unsafe class AudioChannel : IDisposable
{
    public MIX_Audio* Handle;
    public string Path;
    
    public double Length
    {
        get
        {
            var frames = MIX_GetAudioDuration(Handle);
            
            SDL_AudioSpec devSpec;
            int sampleFrames;
            SDL_GetAudioDeviceFormat(SDL_AUDIO_DEVICE_DEFAULT_PLAYBACK, &devSpec, &sampleFrames);

            double ms = (frames * 1000.0) / devSpec.freq;
            return ms;
        }
    }
    
    public void Dispose()
    {
        AudioManager.Instance.Audios.Remove(this);
        MIX_DestroyAudio(Handle);
    }
}