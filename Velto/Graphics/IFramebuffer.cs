namespace Velto.Graphics;

public interface IFramebuffer
{
    int Width { get; }
    int Height { get; }
    TextureFilteringMode FilteringMode { get; }
}