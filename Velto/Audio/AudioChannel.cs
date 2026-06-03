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
            return MIX_AudioFramesToMS(Handle, frames);
        }
    }
    
    public void Dispose()
    {
        AudioManager.Instance.Audios.Remove(this);
        MIX_DestroyAudio(Handle);
    }
}