using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using SDL;
using Velto.Core;
using Velto.Gameplay;
using Buffer = System.Buffer;

namespace Velto.Graphics;

using static SDL3;

public unsafe class Renderer : IDisposable
{
    private readonly float[] fontQuadVertices =
    {
        1f, 0f, 0f, 1f, 0f,
        1f, 1f, 0f, 1f, 1f,
        0f, 0f, 0f, 0f, 0f,
        1f, 1f, 0f, 1f, 1f,
        0f, 1f, 0f, 0f, 1f,
        0f, 0f, 0f, 0f, 0f
    };

    private readonly List<FontCall> _fontCalls = new();
    private readonly int _fontInstanceVbo;

    private readonly int _fontQuadVbo;
    private readonly Shader _fontShader;
    private readonly int _fontVao;

    private Framebuffer? _framebuffer;
    private Stack<Framebuffer> _framebufferStack = new();

    private readonly uint[] _indices =
    [
        0, 2, 1, // first triangle
        1, 2, 3 // second triangle
    ];

    private readonly BufferObject<uint> _quadIndexBuffer;
    private readonly BufferObject<float> _quadVertexBuffer;

    private readonly Shader _spriteShader;
    private readonly VertexArrayObject<float, uint> _spriteVao;

    private Shader _lineShader;
    private VertexArrayObject<float> _lineVao;
    private BufferObject<float> _lineVbo;

    private Shader _sliderShader;

    private readonly float[] _vertices =
    [
        // position         // uv
        0f, 0f, 0f, 0f, 1f, // top-left
        1f, 0f, 0f, 1f, 1f, // top-right
        0f, 1f, 0f, 0f, 0f, // bottom-left
        1f, 1f, 0f, 1f, 0f // bottom-right
    ];
    
    float[] lineVertices = {
        -0.5f, 0.0f,  // start point
        0.5f, 0.0f   // end point
    };


    private readonly Texture _whiteTexture;

    private readonly SDL_Window* _window;
    public uint DrawCallCount = 0;

    public SDL_Window* Window
    {
        get => _window;
    }


    public Renderer(SDL_Window* window)
    {
        _window = window;
        _quadVertexBuffer = new BufferObject<float>(_vertices, BufferTarget.ArrayBuffer, BufferUsage.StaticDraw);
        _quadIndexBuffer = new BufferObject<uint>(_indices, BufferTarget.ElementArrayBuffer, BufferUsage.StaticDraw);
        _spriteVao = new VertexArrayObject<float, uint>(_quadVertexBuffer, _quadIndexBuffer);
        _spriteVao.VertexAttributePointer(0, 3, VertexAttribPointerType.Float, 5 * sizeof(float), 0);
        _spriteVao.VertexAttributePointer(1, 2, VertexAttribPointerType.Float, 5 * sizeof(float), 3 * sizeof(float));
        _spriteShader = new Shader("sprite");
    
        GL.PixelStorei(PixelStoreParameter.UnpackAlignment , 1);
        
        _whiteTexture = new Texture(Resources.GetPath("Resources/Textures/white.png"));

        _fontShader = new Shader("text");
        _fontShader.Use();
        _fontShader.SetInt("uTexture", 0);
        _fontVao = GL.GenVertexArray();
        _fontQuadVbo = GL.GenBuffer();
        _fontInstanceVbo = GL.GenBuffer();

        GL.BindVertexArray(_fontVao);

        // quad buffer
        GL.BindBuffer(BufferTarget.ArrayBuffer, _fontQuadVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, fontQuadVertices.Length * sizeof(float), fontQuadVertices,
            BufferUsage.StaticDraw);
        var quadStride = 5 * sizeof(float);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, quadStride, 0);
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, quadStride, 3 * sizeof(float));

        // instance buffer
        GL.BindBuffer(BufferTarget.ArrayBuffer, _fontInstanceVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, 1024*4 * Marshal.SizeOf<FontCall>(), IntPtr.Zero,
            BufferUsage.DynamicDraw);

        var vec4Size = Vector4.SizeInBytes;
        var instanceStride = Marshal.SizeOf<FontCall>();
        var offset = 0;

        // mat4 (locations 2..5)
        for (var i = 0; i < 4; i++)
        {
            var attrib = (uint)(2 + i);
            GL.EnableVertexAttribArray(attrib);
            GL.VertexAttribPointer(attrib, 4, VertexAttribPointerType.Float, false, instanceStride, offset);
            GL.VertexAttribDivisor(attrib, 1);
            offset += vec4Size;
        }

        // Color (location 6)
        GL.EnableVertexAttribArray(6);
        GL.VertexAttribPointer(6, 4, VertexAttribPointerType.Float, false, instanceStride, offset);
        GL.VertexAttribDivisor(6, 1);
        offset += vec4Size;

        // UV (location 7)
        GL.EnableVertexAttribArray(7);
        GL.VertexAttribPointer(7, 4, VertexAttribPointerType.Float, false, instanceStride, offset);
        GL.VertexAttribDivisor(7, 1);
        offset += vec4Size;

        // Glyph Size (location 8)
        GL.EnableVertexAttribArray(8);
        GL.VertexAttribPointer(8, 2, VertexAttribPointerType.Float, false, instanceStride, offset);
        GL.VertexAttribDivisor(8, 1);
        offset += Vector2.SizeInBytes;

        // Distance Range (location 9)
        GL.EnableVertexAttribArray(9);
        GL.VertexAttribPointer(9, 1, VertexAttribPointerType.Float, false, instanceStride, offset);
        GL.VertexAttribDivisor(9, 1);

        GL.BindVertexArray(0);
        
        _sliderShader = new Shader("slider");

        _lineShader = new("line");

        _lineVbo = new(lineVertices, BufferTarget.ArrayBuffer, BufferUsage.StaticDraw);
        _lineVao = new(_lineVbo);
        _lineVao.VertexAttributePointer(0, 2, VertexAttribPointerType.Float, sizeof(float)*2, 0);
    }

    public void Line()
    {
        _lineShader.Use();
        _lineVao.Bind();
        GL.DrawArrays(PrimitiveType.Lines, 0, 2);
    }

    public Vector2 WindowSizeInPixels
    {
        get
        {
            int w, h;
            SDL_GetWindowSizeInPixels(_window, &w, &h);
            return new Vector2(w, h);
        }
    }

    public void BeginFrame()
    {
        DrawCallCount = 0;
    }
    
    public void BindFramebuffer(Framebuffer framebuffer)
    {
        ArgumentNullException.ThrowIfNull(framebuffer);

        _framebufferStack.Push(framebuffer);
        SetFramebuffer(framebuffer);
    }

    public void UnbindFramebuffer(Framebuffer? framebuffer)
    {
        if (_framebuffer != framebuffer) return;

        _framebufferStack.Pop();

        SetFramebuffer(_framebufferStack.TryPeek(out var lastFramebuffer) ? lastFramebuffer : null);
    }

    private void SetFramebuffer(Framebuffer? framebuffer = null)
    {
        _framebuffer = framebuffer;

        if (_framebuffer == null)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            int width, height;
            SDL_GetWindowSizeInPixels(_window, &width, &height);
            GL.Viewport(0, 0, width, height);
        }
        else
        {
            _framebuffer.Bind();
            GL.Viewport(0, 0, _framebuffer.Width, _framebuffer.Height);
        }
    }

    public void FixFramebuffer()
    {
        SetFramebuffer(_framebuffer);
    }

    public void SetScissor(int x, int y, int width, int height)
    {
        int vpW, vpH;
        if (_framebuffer == null)
        {
            SDL_GetWindowSizeInPixels(_window, &vpW, &vpH);
        }
        else
        {
            vpW = _framebuffer.Width;
            vpH = _framebuffer.Height;
        }

        GL.Enable(EnableCap.ScissorTest);
        GL.Scissor(x, vpH - y - height, Math.Max(0, width), Math.Max(0, height));
    }

    public void Clear(Vector4 color)
    {
        if (_framebuffer == null)
        {
            int width, height;
            SDL_GetWindowSizeInPixels(_window, &width, &height);
            GL.Viewport(0, 0, width, height);
        }
        else
        {
            GL.Viewport(0, 0, _framebuffer.Width, _framebuffer.Height);
        }

        // TODO: implement framebuffers
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
       
        GL.ClearColor(color.X, color.Y, color.Z, color.W);
        GL.Clear(ClearBufferMask.ColorBufferBit /*| ClearBufferMask.DepthBufferBit*/);
        //GL.LineWidth(50);
        //GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Line);
    }

    public void DrawSlider(Slider slider, float x, float y, float scale, float osuRadius, float fadein, float fadeout)
    {
        DrawCallCount++;
        var framebuffer = slider.SliderFramebuffer;
        if (framebuffer == null)
            return;

        // Render into the offscreen texture in its own local coordinate system.
        BindFramebuffer(framebuffer);

        GL.Disable(EnableCap.DepthTest);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.ClearColor(0, 0, 0, 0);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        var projection =
            Matrix4.CreateOrthographicOffCenter(
                0,
                framebuffer.Width,
                framebuffer.Height,
                0,
                -1f,
                1f);

        // Slider mesh vertices are in osu-playfield coordinates; shift them so CacheOffset becomes (0,0) in the FBO.
        var model = Matrix4.CreateTranslation(-slider.CacheOffset.X, -slider.CacheOffset.Y, 0f);

        _sliderShader.Use();

        _sliderShader.SetMatrix4("uProjection", projection);
        _sliderShader.SetMatrix4("uView", Matrix4.Identity);
        _sliderShader.SetMatrix4("uModel", model);

        _sliderShader.SetVector4("uColor", slider.Color);

        // Approximate end-cap size in normalized slider length units.
        float totalLen = 0f;
        for (int i = 1; i < slider.Points.Count; i++)
            totalLen += Vector2.Distance(slider.Points[i - 1], slider.Points[i]);
        float cap = totalLen > 0.001f ? (osuRadius / totalLen) : 0f;

        _sliderShader.SetFloat("uCap", cap);
        _sliderShader.SetFloat("uFadeIn", fadein);
        _sliderShader.SetFloat("uFadeOut", fadeout);

        slider.Vao.Bind();
        GL.DrawElements(PrimitiveType.Triangles, slider.IndexCount, DrawElementsType.UnsignedInt, 0);

        UnbindFramebuffer(framebuffer);

        // Draw cached texture to screen. (x,y) is expected to be the top-left screen position.
        DrawTexture(framebuffer.Texture,
            x, y, framebuffer.Width * scale, framebuffer.Height * scale, new Vector4(1, 1, 1, 1));
    }

    public void DrawTexture(int texture, float x, float y, float width, float height, Vector4 color,
        float rotation = 0)
    {
        DrawCallCount++;
        int wWidth = 1280, wHeight = 720;
        if (_framebuffer == null) SDL_GetWindowSizeInPixels(_window, &wWidth, &wHeight);
        else
        {
            wWidth = _framebuffer.Width;
            wHeight = _framebuffer.Height;
        }

        var projection =
            Matrix4.CreateOrthographicOffCenter(
                0,
                wWidth,
                wHeight,
                0,
                -1f,
                1f);

        var model =
            Matrix4.CreateTranslation(-0.5f, -0.5f, 0f) *
            Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(rotation)) *
            Matrix4.CreateTranslation(0.5f, 0.5f, 0f) *
            Matrix4.CreateScale(width, height, 1f) *
            Matrix4.CreateTranslation(x, y, 0f);

        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, texture);

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
    
    public void DrawTexture(Texture texture, float x, float y, float width, float height, Vector4 color,
        float rotation = 0)

    {
        DrawCallCount++;
        int wWidth = 1280, wHeight = 720;
        if (_framebuffer == null) SDL_GetWindowSizeInPixels(_window, &wWidth, &wHeight);
        else
        {
            wWidth = _framebuffer.Width;
            wHeight = _framebuffer.Height;
        }

        var projection =
            Matrix4.CreateOrthographicOffCenter(
                0,
                wWidth,
                wHeight,
                0,
                -1f,
                1f);

        var model =
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
        DrawTexture(_whiteTexture, x, y, width, height, color, rotation);
    }

    // font stuff 
    public void DrawText(MSDFFont font, string text, Vector2 position, float scale,
        Vector4 color)
    {
        var x = position.X;
        var baseline = position.Y + font.Ascender * font.EmSize * scale;
        uint prev = 0;
        foreach (var c in text)
        {
            if (c == '\n')
            {
                x = position.X;
                baseline += font.LineHeight * font.EmSize * scale;
                prev = 0;
                continue;
            }

            if (!font.Glyphs.TryGetValue(c, out var glyph)) continue;
            var kern = 0f;
            if (prev != 0) kern = font.GetKerning(prev, c) * scale;

            if (glyph.HasGeometry)
            {
                Vector2 glyphPos = new(x + glyph.XOffset * font.EmSize * scale,
                    baseline - glyph.YOffset * font.EmSize * scale - glyph.Height * scale);
                Vector2 glyphSize = new(glyph.Width * scale, glyph.Height * scale);
                DrawGlyph(glyph, glyphPos, glyphSize, color, font.DistanceRange);
            }

            x += glyph.XAdvance * font.EmSize * scale + kern;
            prev = c;
        }

        int wWidth = 1280, wHeight = 720;
        if (_framebuffer == null) SDL_GetWindowSizeInPixels(_window, &wWidth, &wHeight);
        else
        {
            wWidth = _framebuffer.Width;
            wHeight = _framebuffer.Height;
        }
        
        var projection =
            Matrix4.CreateOrthographicOffCenter(
                0,
                wWidth,
                wHeight,
                0,
                -1f,
                1f);
    }

    private void DrawGlyph(MSDFFont.Glyph glyph, Vector2 position, Vector2 size, Vector4 color, float distanceRange)
    {
        var model = Matrix4.CreateScale(size.X, size.Y, 1f) * Matrix4.CreateTranslation(position.X, position.Y, 0f);
        _fontCalls.Add(new FontCall
        {
            Model = model, Color = color, UV = new Vector4(glyph.U0, glyph.V0, glyph.U1, glyph.V1), GlyphSize = size,
            DistanceRange = distanceRange
        });
    }

    public void FlushText(MSDFFont font)
    {
        int wWidth = 1280, wHeight = 720;
        if (_framebuffer == null) SDL_GetWindowSizeInPixels(_window, &wWidth, &wHeight);
        else
        {
            wWidth = _framebuffer.Width;
            wHeight = _framebuffer.Height;
        }

        var projection =
            Matrix4.CreateOrthographicOffCenter(
                0,
                wWidth,
                wHeight,
                0,
                -1f,
                1f);
        
        DrawCallCount++;
        if (_fontCalls.Count == 0) return;
        // GL.UseProgram(shader);
        // int projLoc = GL.GetUniformLocation(shader, "uProjection");
        // GL.UniformMatrix4(projLoc, false, ref projection);
        _fontShader.Use();
        _fontShader.SetMatrix4("uProjection", projection);

        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, font.AtlasTexture);
        GL.BindVertexArray(_fontVao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _fontInstanceVbo);
        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, _fontCalls.Count * Marshal.SizeOf<FontCall>(),
            _fontCalls.ToArray());
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.DrawArraysInstanced(PrimitiveType.Triangles, 0, 6, _fontCalls.Count);
        _fontCalls.Clear();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Vertex
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
    
    [StructLayout(LayoutKind.Sequential)]
    public struct FontCall
    {
        public Matrix4 Model;
        public Vector4 Color;
        public Vector4 UV;
        public Vector2 GlyphSize;
        public float DistanceRange;
    }
    
    public void Dispose()
    {
        GL.DeleteBuffer(_fontInstanceVbo);
        GL.DeleteBuffer(_fontQuadVbo);
        GL.DeleteVertexArray(_fontVao);

        _quadVertexBuffer.Dispose();
        _quadIndexBuffer.Dispose();
        _spriteVao.Dispose();
        _whiteTexture.Dispose();
        _spriteShader.Dispose();
        _fontShader.Dispose();
        _sliderShader.Dispose();
    }
}