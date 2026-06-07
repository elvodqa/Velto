using OpenTK.Graphics.OpenGL;
using StbImageSharp;

namespace Velto.Graphics.OpenGL;

public unsafe class OpenGLTexture : ITexture
{
    public int Width { get; private set; }
    public int Height { get; private set; }
    
    public TextureFilteringMode FilteringMode { get; }
    public TextureWrapMode WrapMode { get; }
    
    public string Path;
    public int Handle;
    
    private readonly TextureFilteringMode _filterMode;
    private readonly TextureWrapMode _wrapMode;
        
    public OpenGLTexture(string path, TextureFilteringMode filterMode = TextureFilteringMode.Linear, 
        TextureWrapMode wrapMode = TextureWrapMode.Repeat,
        bool generateMipmaps = true)
    {
        Path = path;
        _filterMode = filterMode;
        _wrapMode = wrapMode;
        StbImage.stbi_set_flip_vertically_on_load(1);
        var result = ImageResult.FromMemory(File.ReadAllBytes(path), ColorComponents.RedGreenBlueAlpha);

        Width = result.Width;
        Height = result.Height;

        Handle = GL.GenTexture();
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, Handle);

        fixed (byte* ptr = result.Data)
        {
            var format = result.Comp == ColorComponents.RedGreenBlueAlpha
                ? PixelFormat.Rgba
                : PixelFormat.Rgb;

            var internalFormat = result.Comp == ColorComponents.RedGreenBlueAlpha
                ? InternalFormat.Rgba
                : InternalFormat.Rgb;

            GL.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba,
                result.Width, result.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
        }

        GL.BindTexture(TextureTarget.Texture2D, Handle);

        var wrap = ToGL(_wrapMode);

        GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)wrap);
        GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)wrap);

        // Filter
        if (generateMipmaps)
        {
            var mipmapFilter = _filterMode == TextureFilteringMode.Linear
                ? TextureMinFilter.LinearMipmapLinear
                : TextureMinFilter.NearestMipmapNearest;
            GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)mipmapFilter);
        }
        else
        {
            var filter = _filterMode == TextureFilteringMode.Linear
                ? TextureMinFilter.Linear
                : TextureMinFilter.Nearest;
            GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)filter);
        }

        var magFilter = _filterMode == TextureFilteringMode.Linear
            ? TextureMagFilter.Linear
            : TextureMagFilter.Nearest;
        GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)magFilter);

        if (generateMipmaps)
            GL.GenerateMipmap(TextureTarget.Texture2D);

        GL.BindTexture(TextureTarget.Texture2D, 0);
    }
    
    public void Dispose()
    {
        GL.DeleteTexture(Handle);
    }

    private static OpenTK.Graphics.OpenGL.TextureWrapMode ToGL(TextureWrapMode mode)
    {
        return mode switch
        {
            TextureWrapMode.Repeat => OpenTK.Graphics.OpenGL.TextureWrapMode.Repeat,
            TextureWrapMode.ClampToEdge => OpenTK.Graphics.OpenGL.TextureWrapMode.ClampToEdge,
            TextureWrapMode.ClampToBorder => OpenTK.Graphics.OpenGL.TextureWrapMode.ClampToBorder,
            _ => OpenTK.Graphics.OpenGL.TextureWrapMode.Repeat
        };
    }

    public IntPtr ToIntPtr()
    {
        return Handle;
    }

    public void Bind(TextureUnit unit = TextureUnit.Texture0)
    {
        GL.ActiveTexture(unit);
        GL.BindTexture(TextureTarget.Texture2D, Handle);
    }

    public void Unbind()
    {
        GL.BindTexture(TextureTarget.Texture2D, 0);
    }
}