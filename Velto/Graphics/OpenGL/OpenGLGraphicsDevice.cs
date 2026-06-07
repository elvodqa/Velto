using SDL;
using static SDL.SDL3;

namespace Velto.Graphics.OpenGL;


public class OpenGLGraphicsDevice : IGraphicsDevice
{
    public GraphicsBackend Backend
    {
        get => GraphicsBackend.OpenGL;
    }

    private Window window;

    public OpenGLGraphicsDevice(Window window)
    {
        this.window = window;
    }

    public ITexture CreateTexture(int width, int height, TextureFilteringMode filteringMode = TextureFilteringMode.Linear,
        TextureWrapMode wrapMode = TextureWrapMode.ClampToEdge)
    {
        throw new NotImplementedException();
    }

    public ITexture CreateTexture(string path, TextureFilteringMode filteringMode = TextureFilteringMode.Linear,
        TextureWrapMode wrapMode = TextureWrapMode.ClampToEdge)
    {
        return new OpenGLTexture(path, filteringMode, wrapMode);
    }

    public IFramebuffer CreateFramebuffer(int width, int height, TextureFilteringMode filteringMode = TextureFilteringMode.Linear)
    {
        throw new NotImplementedException();
    }
    
    public void Dispose()
    {
        
    }

}