namespace Velto.Graphics;

public enum GraphicsBackend
{
    OpenGL,
    Metal,
}

public interface IGraphicsDevice : IDisposable
{
    Window Window { get; }
    GraphicsBackend Backend { get; }
    
    ITexture CreateTexture(int width, int height, TextureFilteringMode filteringMode = TextureFilteringMode.Linear,  
        TextureWrapMode wrapMode = TextureWrapMode.ClampToEdge);
    ITexture CreateTexture(string path, TextureFilteringMode filteringMode = TextureFilteringMode.Linear,  
        TextureWrapMode wrapMode = TextureWrapMode.ClampToEdge, bool generateMipmaps = true, int verticallyFlip = 1);
    IFramebuffer CreateFramebuffer(int width, int height,
        TextureFilteringMode filteringMode = TextureFilteringMode.Linear);
}