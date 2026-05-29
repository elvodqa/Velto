using System.Runtime.InteropServices;
using StbImageSharp;
using OpenTK.Graphics.OpenGL;

namespace Velto.Graphics;

public enum WrapMode
{
    Repeat,
    MirroredRepeat,
    ClampToEdge,
    ClampToBorder,
}

public enum FilterMode
{
    Nearest,
    Linear,
}

public unsafe class Texture : IDisposable
{
    private ImageResult _result;
    private WrapMode _wrapMode;
    private FilterMode _filterMode;
    private int _width;
    private int _height;
    private static TextureWrapMode ToGL(WrapMode mode) => mode switch
    {
        WrapMode.Repeat => OpenTK.Graphics.OpenGL.TextureWrapMode.Repeat,
        WrapMode.MirroredRepeat => OpenTK.Graphics.OpenGL.TextureWrapMode.MirroredRepeat,
        WrapMode.ClampToEdge => OpenTK.Graphics.OpenGL.TextureWrapMode.ClampToEdge,
        WrapMode.ClampToBorder => OpenTK.Graphics.OpenGL.TextureWrapMode.ClampToBorder,
        _ => OpenTK.Graphics.OpenGL.TextureWrapMode.Repeat
    };
    
    public int Handle;
    public IntPtr ToIntPtr() => Handle;

    public int Width
    {
        get => _width;
    }

    public int Height
    {
        get => _height;
    }
    
    public Texture(string path, FilterMode filterMode = FilterMode.Linear, WrapMode wrapMode = WrapMode.Repeat, bool generateMipmaps = true)
    {
        _filterMode = filterMode;
        _wrapMode = wrapMode;
        StbImage.stbi_set_flip_vertically_on_load(1);
        _result =  ImageResult.FromMemory(File.ReadAllBytes(path), ColorComponents.RedGreenBlueAlpha);
        
        _width = _result.Width;
        _height =_result.Height;

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
    
    public void Bind(TextureUnit unit = TextureUnit.Texture0)
    {
        GL.ActiveTexture(unit);
        GL.BindTexture(TextureTarget.Texture2D, Handle);
    }
    
    public void Unbind()
    {
        GL.BindTexture(TextureTarget.Texture2D, 0);
    }
    
    public void Dispose()
    {
        GL.DeleteTexture(Handle);
    }
}