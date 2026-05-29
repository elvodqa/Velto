using System.Runtime.InteropServices;
using BlurgText;

namespace Velto.Graphics;

using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using Velto.Gameplay;
using SDL;
using static SDL.SDL3;

public unsafe class Renderer : IDisposable
{
    private float[] _vertices =
    [
        // position         // uv
        0f, 0f, 0f, 0f, 1f, // top-left
        1f, 0f, 0f, 1f, 1f, // top-right
        0f, 1f, 0f, 0f, 0f, // bottom-left
        1f, 1f, 0f, 1f, 0f // bottom-right
    ];

    private uint[] _indices =
    [
        0, 2, 1, // first triangle
        1, 2, 3 // second triangle
    ];
    
    private SDL_Window* _window;
    private BufferObject<float> _quadVertexBuffer;
    private BufferObject<uint> _quadIndexBuffer;
    private VertexArrayObject<float, uint> _spriteVao;

    private BufferObject<Vertex> _fontVertexBuffer;
    private VertexArrayObject<Vertex> _fontVao;
    
    private Shader _spriteShader;
    private Shader _textShader;

    private Texture _whiteTexture;

    private bool _framebufferBound = false;
    
    private Vertex[] vertices = new Vertex[16384];
    private int vCount = 0;
    private IntPtr currentTexture = IntPtr.Zero;
    private List<int> textures = new List<int>();
    private Blurg _blurg;

    public Vector2 WindowSizeInPixels
    {
        get
        {
            int w, h;
            SDL_GetWindowSizeInPixels(_window, &w, &h);
            return new Vector2(w, h);
        }
    }
    
    [StructLayout(LayoutKind.Sequential)]
    struct Vertex
    {
        public Vector2 Position;
        public Vector2 Texture;
        public Vector4 Color;
        public Vertex(Vector2 pos, Vector2 tex, Vector4 color)
        {
            Position = pos;
            Texture = tex;
            Color = color;
        }
    }


    public Renderer(SDL_Window *window)
    {
        _window = window;
        _quadVertexBuffer = new(_vertices, BufferTarget.ArrayBuffer, BufferUsage.StaticDraw);
        _quadIndexBuffer = new(_indices, BufferTarget.ElementArrayBuffer, BufferUsage.StaticDraw);
        _spriteVao = new VertexArrayObject<float, uint>(_quadVertexBuffer, _quadIndexBuffer);
        _spriteVao.VertexAttributePointer(0, 3, VertexAttribPointerType.Float, 5 * sizeof(float), 0);
        _spriteVao.VertexAttributePointer(1, 2, VertexAttribPointerType.Float, 5 * sizeof(float), 3 * sizeof(float));
        _spriteShader = new("sprite");


        _fontVertexBuffer = new BufferObject<Vertex>(vertices.Length, BufferTarget.ArrayBuffer, BufferUsage.DynamicDraw);
        _fontVao = new VertexArrayObject<Vertex>(_fontVertexBuffer);
        
        uint stride = (uint)Marshal.SizeOf<Vertex>(); // should be 32
        _fontVao.VertexAttributePointer(0, 2, VertexAttribPointerType.Float, stride, 0);
        _fontVao.VertexAttributePointer(1, 2, VertexAttribPointerType.Float, stride, Vector2.SizeInBytes);
        _fontVao.VertexAttributePointer(2, 4, VertexAttribPointerType.Float, stride, Vector2.SizeInBytes * 2);
        
        _textShader = new("blurg");
        _textShader.Use();
        _textShader.SetInt("oTexture", 0);

        _blurg = new Blurg(CreateTexture, UpdateTexture);
        _blurg.EnableSystemFonts();
        _blurg.AddFontFile(Resources.GetFontPath("Roboto-Regular.ttf"));
        _blurg.AddFontFile(Resources.GetFontPath("Roboto-Bold.ttf"));
        _blurg.AddFontFile(Resources.GetFontPath("Roboto-Italic.ttf"));
        _blurg.AddFontFile(Resources.GetFontPath("Roboto-BoldItalic.ttf"));
        _blurg.AddFontFile(Resources.GetFontPath("Roboto-ThinItalic.ttf"));
        _blurg.AddFontFile(Resources.GetFontPath("Roboto-Black.ttf"));
        
        _whiteTexture = new(Resources.GetPath("Resources/Textures/white.png"));

    }

    public void Clear(Vector4 color)
    {
        if (!_framebufferBound)
        {
            int width, height;
            SDL_GetWindowSizeInPixels(_window, &width, &height);
            GL.Viewport(0, 0, width, height);
        }
        else
        {
            // TODO: implement framebuffers
        }
        
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.ClearColor(color.X, color.Y, color.Z, color.W);
        GL.Clear(ClearBufferMask.ColorBufferBit /*| ClearBufferMask.DepthBufferBit*/);
    }
    
    public void DrawTexture(Texture texture, float x, float y, float width, float height, Vector4 color,
        float rotation = 0)

    {
        int wWidth = 1280, wHeight = 720;
        if (!_framebufferBound)
        {
            SDL_GetWindowSizeInPixels(_window, &wWidth, &wHeight);
        }
        
        Matrix4 projection =
            Matrix4.CreateOrthographicOffCenter(
                0,
                wWidth,
                wHeight,
                0,
                -1f,
                1f);

        Matrix4 model =
            Matrix4.CreateTranslation(-0.5f, -0.5f, 0f) *
            Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(rotation)) *
            Matrix4.CreateTranslation(0.5f, 0.5f, 0f) *
            Matrix4.CreateScale(width, height, 1f) *
            Matrix4.CreateTranslation(x, y, 0f);

        texture.Bind();

        _spriteShader.Use();
        _spriteShader.SetInt("ourTexture", 0);
        _spriteShader.SetVector4("color", color);
        _spriteShader.SetMatrix4("model", model);
        _spriteShader.SetMatrix4("view", Matrix4.Identity);
        _spriteShader.SetMatrix4("projection", projection);

        _spriteVao.Bind();

        GL.DrawElements(
            PrimitiveType.Triangles,
            6,
            DrawElementsType.UnsignedInt,
            IntPtr.Zero);
    }

    public void DrawRectangle(float x, float y, float width, float height, Vector4 color,
        float rotation = 0)
    {
        int wWidth = 1280, wHeight = 720;
        if (!_framebufferBound)
        {
            SDL_GetWindowSizeInPixels(_window, &wWidth, &wHeight);
        }
        
        Matrix4 projection =
            Matrix4.CreateOrthographicOffCenter(
                0,
                wWidth,
                wHeight,
                0,
                -1f,
                1f);

        Matrix4 model =
            Matrix4.CreateTranslation(-0.5f, -0.5f, 0f) *
            Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(rotation)) *
            Matrix4.CreateTranslation(0.5f, 0.5f, 0f) *
            Matrix4.CreateScale(width, height, 1f) *
            Matrix4.CreateTranslation(x, y, 0f);

        _whiteTexture.Bind();

        _spriteShader.Use();
        _spriteShader.SetInt("ourTexture", 0);
        _spriteShader.SetVector4("color", color);
        _spriteShader.SetMatrix4("model", model);
        _spriteShader.SetMatrix4("view", Matrix4.Identity);
        _spriteShader.SetMatrix4("projection", projection);

        _spriteVao.Bind();

        GL.DrawElements(
            PrimitiveType.Triangles,
            6,
            DrawElementsType.UnsignedInt,
            IntPtr.Zero);
    }
    
    
    // Font stuff
    public IntPtr CreateTexture(int width, int height)
    {
        var texture = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, texture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, width, height, 0,
            PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
        GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
        GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
        textures.Add(texture);
        return (IntPtr)texture;
    }

    public void UpdateTexture(IntPtr texture, IntPtr buffer, int x, int y, int width, int height)
    {
        GL.BindTexture(TextureTarget.Texture2D, (int)texture);
        GL.TexSubImage2D(TextureTarget.Texture2D, 0, x, y, width, height, PixelFormat.Rgba, PixelType.UnsignedByte,
            buffer);
    }
        
    /*public void Start(int width, int height)
    {
        GL.Disable(EnableCap.CullFace);
        GL.Disable(EnableCap.DepthTest);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _textShader.Use();
        var m = Matrix4.CreateOrthographicOffCenter(0, width, height, 0, 0, 1);
        _textShader.SetMatrix4("viewprojection", m);
    }*/

    public void DrawText(Vector2 position, string fontfamily, FontWeight fontWeight, float size, Vector4 color, string text, bool italic = false)
    {
        BlurgColor blurgColor = new BlurgColor((byte)(color.X * 255), (byte)(color.Y * 255), (byte)(color.Z * 255), (byte)(color.W * 255));
        var str = _blurg.BuildString(_blurg.QueryFont(fontfamily, fontWeight, italic)!, size, blurgColor, text);
        DrawRects(str, (int)position.X, (int)position.Y);
        Finish();
    }
    
    public void DrawRects(BlurgResult? rects, int x, int y)
    {
        if (rects == null)
            return;
        for (int i = 0; i < rects.Count; i++)
        {
            if (vCount + 6 > vertices.Length ||
                (currentTexture != rects[i].UserData && currentTexture != IntPtr.Zero))
                Flush();
            currentTexture = rects[i].UserData;
            var o = new Vector2(x, y);
            var pos = new Vector2(rects[i].X, rects[i].Y);
            var tl = new Vertex(
                o + pos,
                new Vector2(rects[i].U0, rects[i].V0),
                new Vector4(rects[i].Color)
            );
            var tr = new Vertex(
                o + pos + new Vector2(rects[i].Width, 0),
                new Vector2(rects[i].U1, rects[i].V0),
                new Vector4(rects[i].Color)
            );
            var bl = new Vertex(
                o + pos + new Vector2(0, rects[i].Height),
                new Vector2(rects[i].U0, rects[i].V1),
                new Vector4(rects[i].Color)
            );
            var br = new Vertex(
                o + pos + new Vector2(rects[i].Width, rects[i].Height),
                new Vector2(rects[i].U1, rects[i].V1),
                new Vector4(rects[i].Color)
            );
            vertices[vCount++] = tl;
            vertices[vCount++] = tr;
            vertices[vCount++] = bl;
            vertices[vCount++] = bl;
            vertices[vCount++] = tr;
            vertices[vCount++] = br;
        }
    }
    
    public void FillBackground(int x, int y, int width, int height, BlurgColor color)
    {
        if (vCount + 6 > vertices.Length)
            Flush();
        var tcoord = new Vector2(0.5f / 1024.0f, 0.5f / 1024.0f);
        var tl = new Vertex(
            new Vector2(x, y),
            tcoord,
            new Vector4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f)
        );
        var tr = new Vertex(
            new Vector2(x + width, y),
            tcoord,
            new Vector4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f)
        );
        var bl = new Vertex(
            new Vector2(x, y + height),
            tcoord,
            new Vector4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f)
        );
        var br = new Vertex(
            new Vector2(x + width, y + height),
            tcoord,
            new Vector4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f)
        );
        vertices[vCount++] = tl;
        vertices[vCount++] = tr;
        vertices[vCount++] = bl;
        vertices[vCount++] = bl;
        vertices[vCount++] = tr;
        vertices[vCount++] = br;
    }
    
    void Flush()
    {
        int width = 1280, height = 720;
        if (!_framebufferBound)
        {
            SDL_GetWindowSizeInPixels(_window, &width, &height);
        }
        _textShader.Use();
        var m = Matrix4.CreateOrthographicOffCenter(0, width, height, 0, 0, 1);
        _textShader.SetMatrix4("viewprojection", m);
        _fontVao.Bind();
        _fontVertexBuffer.Bind();
        GL.BufferSubData(
            BufferTarget.ArrayBuffer,
            IntPtr.Zero,
            vCount * Marshal.SizeOf<Vertex>(),
            vertices
        );
        GL.BindTexture(TextureTarget.Texture2D, (int)currentTexture);
        GL.DrawArrays(PrimitiveType.Triangles, 0, vCount);
        vCount = 0;
    }

    public void Finish()
    {
        if (vCount > 0)
            Flush();
        currentTexture = IntPtr.Zero;
    }
    
    public void Dispose()
    {
        foreach (var t in textures)
            GL.DeleteTexture(t);
        textures.Clear();
        _fontVao.Dispose();
        _fontVertexBuffer.Dispose();
        
        _quadVertexBuffer.Dispose();
        _quadIndexBuffer.Dispose();
        _spriteVao.Dispose();
        _whiteTexture.Dispose();
        _spriteShader.Dispose();
        _textShader.Dispose();
    }
}