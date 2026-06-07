namespace Velto.Graphics;

public interface IFramebuffer : IDisposable
{
    int Width { get; }
    int Height { get; }
    TextureFilteringMode FilteringMode { get; }
    public void Resize(int w, int h);
}