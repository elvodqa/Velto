using OpenTK.Graphics.OpenGL;

namespace Velto.Graphics.OpenGL;

public class OpenGLFramebuffer : IFramebuffer
{
    public TextureFilteringMode FilteringMode { get; private set; }
    public int Width
    {
        get
        {
            return _width;
        }
        private set => _width = value;
    }

    public int Height
    {
        get => _height;
        private set => _height = value;
    }
    public int Handle { get; private set; }
    public int Texture { get; private set; }
    private int _width;
    private int _height;
    
    public OpenGLFramebuffer(int width, int height, TextureFilteringMode filteringMode)
    {
        FilteringMode = filteringMode;
        _width = width;
        _height = height;
        Handle = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, Handle);

        Create();
    }
    
    public void Resize(int width, int height)
    {
        _width = width;
        _height = height;
        GL.DeleteTexture(Texture);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, Handle);
        Create();
    }
    
    private unsafe void Create()
    {
        Width = _width;
        Height = _height;

        Texture = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, Texture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, _width, _height, 0,
            PixelFormat.Rgba, PixelType.UnsignedByte, null);

        var min = FilteringMode == TextureFilteringMode.Linear
            ? TextureMinFilter.Linear
            : TextureMinFilter.Nearest;
        var max = FilteringMode == TextureFilteringMode.Linear
            ? TextureMagFilter.Linear
            : TextureMagFilter.Nearest;
        
        GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)min);
        GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)max);
        GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        GL.BindTexture(TextureTarget.Texture2D, 0);

        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D, Texture, 0);

        GL.DrawBuffer(DrawBufferMode.ColorAttachment0);

        var res = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (res != FramebufferStatus.FramebufferComplete)
        {
            throw new Exception($"Framebuffer error: {res}");
        }
        //_renderer.FixFramebuffer();
        //GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        //if (Renderer.Framebuffer != null) GL.BindFramebuffer(FramebufferTarget.Framebuffer, Renderer.Framebuffer.Handle);
        //else GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }
    
    public void Bind()
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, Handle);
    }


    public void Dispose()
    {
        GL.DeleteTexture(Texture);
        GL.DeleteFramebuffer(Handle);
    }
}