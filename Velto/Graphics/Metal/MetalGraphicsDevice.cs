using System.Runtime.Versioning;
using SharpMetal.Metal;
using SharpMetal.QuartzCore;

namespace Velto.Graphics.Metal;

[SupportedOSPlatform("macos")]
public class MetalGraphicsDevice : IGraphicsDevice
{
    public Window Window { get; private set; }
    public GraphicsBackend Backend { get; } = GraphicsBackend.Metal;

    public MTLDevice Device;
    public CAMetalLayer MetalLayer;
    
    public MetalGraphicsDevice(Window window)
    {
        Window = window;


        Device = MTLDevice.CreateSystemDefaultDevice();
        MetalLayer = new CAMetalLayer(window.MetalLayer);
        MetalLayer.Device = Device;
        MetalLayer.PixelFormat = MTLPixelFormat.RGBA8Unorm;
    }

    public ITexture CreateTexture(int width, int height, TextureFilteringMode filteringMode = TextureFilteringMode.Linear,
        TextureWrapMode wrapMode = TextureWrapMode.ClampToEdge)
    {
        throw new NotImplementedException();
    }

    public ITexture CreateTexture(string path, TextureFilteringMode filteringMode = TextureFilteringMode.Linear,
        TextureWrapMode wrapMode = TextureWrapMode.ClampToEdge, bool generateMipmaps = true, int verticallyFlip = 1)
    {
        return new MetalTexture(this, path, filteringMode, wrapMode, generateMipmaps, verticallyFlip);
    }

    public IFramebuffer CreateFramebuffer(int width, int height, TextureFilteringMode filteringMode = TextureFilteringMode.Linear)
    {
        return new MetalFramebuffer(this, width, height, filteringMode);
    }
    
    public void Dispose()
    {
        Device.Dispose();
    }
}