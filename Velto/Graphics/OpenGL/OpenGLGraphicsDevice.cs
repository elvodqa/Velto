using SDL;
using static SDL.SDL3;

namespace Velto.Graphics.OpenGL;


public class OpenGLGraphicsDevice : IGraphicsDevice
{
    public GraphicsBackend Backend
    {
        get => GraphicsBackend.OpenGL;
    }

    public Window Window { get; private set; }

    public OpenGLGraphicsDevice(Window window)
    {
        this.Window = window;
    }

    public ITexture CreateTexture(int width, int height, TextureFilteringMode filteringMode = TextureFilteringMode.Linear,
        TextureWrapMode wrapMode = TextureWrapMode.ClampToEdge)
    {
        throw new NotImplementedException();
    }

    public ITexture CreateTexture(string path, TextureFilteringMode filteringMode = TextureFilteringMode.Linear,
        TextureWrapMode wrapMode = TextureWrapMode.ClampToEdge, bool generateMipmaps = true, int verticallyFlip = 1)
    {
        return new OpenGLTexture(path, filteringMode, wrapMode, generateMipmaps, verticallyFlip);
    }

    public IFramebuffer CreateFramebuffer(int width, int height, TextureFilteringMode filteringMode = TextureFilteringMode.Linear)
    {
        return new OpenGLFramebuffer(width, height, filteringMode);
    }
    
    public void Dispose()
    {
        
    }
    

}