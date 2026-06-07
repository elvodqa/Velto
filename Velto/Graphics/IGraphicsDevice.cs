namespace Velto.Graphics;

public enum GraphicsBackend
{
    OpenGL,
    Metal,
}

public interface IGraphicsDevice : IDisposable
{
    GraphicsBackend Backend { get; }
    
    ITexture CreateTexture(int width, int height, TextureFilteringMode filteringMode = TextureFilteringMode.Linear,  TextureWrapMode wrapMode = TextureWrapMode.ClampToEdge);
    ITexture CreateTexture(string path, TextureFilteringMode filteringMode = TextureFilteringMode.Linear,  TextureWrapMode wrapMode = TextureWrapMode.ClampToEdge);
    IFramebuffer CreateFramebuffer(int width, int height,
        TextureFilteringMode filteringMode = TextureFilteringMode.Linear);
}