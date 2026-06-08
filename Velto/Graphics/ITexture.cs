namespace Velto.Graphics;

public enum TextureFilteringMode 
{
    Linear,
    Nearest,
}

public enum TextureWrapMode
{
    None = 0,
    ClampToEdge = 1,
    ClampToBorder = 2,
    Repeat = 3,
}

public interface ITexture : IDisposable
{
    int Width { get; }
    int Height { get; }
    TextureFilteringMode FilteringMode { get; }
    TextureWrapMode WrapMode { get; }
}