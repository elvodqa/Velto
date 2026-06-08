using System.Runtime.Versioning;
using SharpMetal.Metal;

namespace Velto.Graphics.Metal;

[SupportedOSPlatform("macos")]
public class MetalFramebuffer : IFramebuffer
{
    public int Width { get; private set; }
    public int Height{ get; private set; }
    public TextureFilteringMode FilteringMode { get; }

    public MTLTexture Handle;
    public MTLDevice device;

    public MetalFramebuffer(MetalGraphicsDevice device, int w, int h, TextureFilteringMode mode = TextureFilteringMode.Linear)
    {
        this.device = device.Device;
        Width = w;
        Height = h;
        FilteringMode = mode;
        Handle = this.device.NewTexture(new MTLTextureDescriptor()
        {
            Width = (ulong)Width,
            Height = (ulong)Height,
            PixelFormat = MTLPixelFormat.RGBA8Unorm,
            TextureType = MTLTextureType.Type2D,
            Usage = MTLTextureUsage.ShaderRead | MTLTextureUsage.RenderTarget,
            StorageMode = MTLStorageMode.Shared
        });
    }
    
    public void Resize(int w, int h)
    {
        Width = w;
        Height = h;
        Handle.Dispose();
        Handle = device.NewTexture(new MTLTextureDescriptor()
        {
            Width = (ulong)Width,
            Height = (ulong)Height,
            PixelFormat = MTLPixelFormat.RGBA8Unorm,
            TextureType = MTLTextureType.Type2D,
            Usage = MTLTextureUsage.ShaderRead | MTLTextureUsage.RenderTarget,
            StorageMode = MTLStorageMode.Shared
        });
    }
    
    public void Dispose()
    {
        Handle.Dispose();
    }
}