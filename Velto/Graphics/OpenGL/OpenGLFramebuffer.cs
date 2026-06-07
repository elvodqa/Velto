namespace Velto.Graphics.OpenGL;

public class OpenGLFramebuffer : IFramebuffer
{
    public int Width { get; private set; }
    public int Height { get; private set; }
    public TextureFilteringMode FilteringMode { get; }
    
    
    
}