using OpenTK.Graphics.OpenGL;
using StbImageSharp;

namespace Velto.Graphics;

public enum WrapMode
{
    Repeat,
    MirroredRepeat,
    ClampToEdge,
    ClampToBorder
}

public enum FilterMode
{
    Nearest,
    Linear
}

public unsafe class Texture : IDisposable
{
    private readonly FilterMode _filterMode;
    private readonly ImageResult _result;
    private readonly WrapMode _wrapMode;

    public string Path;
    public int Handle;

    public Texture(string path, FilterMode filterMode = FilterMode.Linear, WrapMode wrapMode = WrapMode.Repeat,
        bool generateMipmaps = true)
    {
        Path = path;
        _filterMode = filterMode;
        _wrapMode = wrapMode;
        StbImage.stbi_set_flip_vertically_on_load(1);
        _result = ImageResult.FromMemory(File.ReadAllBytes(path), ColorComponents.RedGreenBlueAlpha);

        Width = _result.Width;
        Height = _result.Height;

        Handle = GL.GenTexture();
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, Handle);

        fixed (byte* ptr = _result.Data)
        {
            var format = _result.Comp == ColorComponents.RedGreenBlueAlpha
                ? PixelFormat.Rgba
                : PixelFormat.Rgb;

            var internalFormat = _result.Comp == ColorComponents.RedGreenBlueAlpha
                ? InternalFormat.Rgba
                : InternalFormat.Rgb;

            GL.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba,
                _result.Width, _result.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
        }

        GL.BindTexture(TextureTarget.Texture2D, Handle);

        var wrap = ToGL(_wrapMode);

        GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)wrap);
        GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)wrap);

        // Filter
        if (generateMipmaps)
        {
            var mipmapFilter = _filterMode == FilterMode.Linear
                ? TextureMinFilter.LinearMipmapLinear
                : TextureMinFilter.NearestMipmapNearest;
            GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)mipmapFilter);
        }
        else
        {
            var filter = _filterMode == FilterMode.Linear
                ? TextureMinFilter.Linear
                : TextureMinFilter.Nearest;
            GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)filter);
        }

        var magFilter = _filterMode == FilterMode.Linear
            ? TextureMagFilter.Linear
            : TextureMagFilter.Nearest;
        GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)magFilter);

        if (generateMipmaps)
            GL.GenerateMipmap(TextureTarget.Texture2D);

        GL.BindTexture(TextureTarget.Texture2D, 0);
    }

    public int Width { get; }

    public int Height { get; }

    public void Dispose()
    {
        GL.DeleteTexture(Handle);
    }

    private static OpenTK.Graphics.OpenGL.TextureWrapMode ToGL(WrapMode mode)
    {
        return mode switch
        {
            WrapMode.Repeat => OpenTK.Graphics.OpenGL.TextureWrapMode.Repeat,
            WrapMode.MirroredRepeat => OpenTK.Graphics.OpenGL.TextureWrapMode.MirroredRepeat,
            WrapMode.ClampToEdge => OpenTK.Graphics.OpenGL.TextureWrapMode.ClampToEdge,
            WrapMode.ClampToBorder => OpenTK.Graphics.OpenGL.TextureWrapMode.ClampToBorder,
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