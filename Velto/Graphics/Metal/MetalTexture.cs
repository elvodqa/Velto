using System.Runtime.Versioning;
using SharpMetal.Metal;
using StbImageSharp;

namespace Velto.Graphics.Metal;

[SupportedOSPlatform("macos")]
public unsafe class MetalTexture : ITexture
{
    public int Width { get; }
    public int Height { get; }
    public TextureFilteringMode FilteringMode { get; }
    public TextureWrapMode WrapMode { get; }
    public string Path { get; }

    private MetalGraphicsDevice device;
    public MTLTexture Handle;

    public MetalTexture(MetalGraphicsDevice device, string path, TextureFilteringMode filterMode = TextureFilteringMode.Linear, 
        TextureWrapMode wrapMode = TextureWrapMode.Repeat, bool generateMipmaps = true, int flipVertically = 1)
    {
        this.device = device;
        Path = path;
        FilteringMode = filterMode;
        WrapMode = wrapMode;
        StbImage.stbi_set_flip_vertically_on_load(flipVertically);
        var result = ImageResult.FromMemory(File.ReadAllBytes(path), ColorComponents.RedGreenBlueAlpha);

        Width = result.Width;
        Height = result.Height;

        Handle = this.device.Device.NewTexture(new MTLTextureDescriptor()
        {
            Width = (ulong)Width,
            Height = (ulong)Height,
            PixelFormat = MTLPixelFormat.RGBA8Unorm,
            TextureType = MTLTextureType.Type2D,
            Usage = MTLTextureUsage.ShaderRead,
        });

        fixed (byte* ptr = result.Data)
        {
            Handle.ReplaceRegion(new MTLRegion()
            {
                size = new MTLSize()
                {
                    width = (ulong)Width,
                    height = (ulong)Height,
                    depth = 1,
                },
                origin = new MTLOrigin()
                {
                    x = 0,
                    y = 0,
                }
            }, 0, new IntPtr(ptr), (ulong)(4 * Width));
        }
    }
    
    public void Dispose()
    {
        Handle.Dispose();
    }
}