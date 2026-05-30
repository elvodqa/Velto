namespace Velto.Graphics;

using OpenTK.Graphics.OpenGL;

public unsafe class Framebuffer : IDisposable
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int Handle { get; private set; }
    public int Texture { get; private set; }
    
    private Renderer _renderer;
    private int _width;
    private int _height;

    public Framebuffer(Renderer renderer, int width, int height)
    {
        _width = width;
        _height = height;
        _renderer = renderer;
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

    private void Create()
    {
        Width = _width;
        Height = _height;

        Texture = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, Texture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, _width, _height, 0,
            PixelFormat.Rgba, PixelType.UnsignedByte, null);

        // Must use OpenGL enums here (NOT Velto.Graphics.FilterMode). Otherwise the texture stays incomplete and samples black.
        GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
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
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        
        _renderer.FixFramebuffer();
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