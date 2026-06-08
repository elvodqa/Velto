using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using SDL;
using Velto.Core;

namespace Velto.Graphics.OpenGL;

public struct ScissorRect
{
    public int X, Y, W, H;

    public ScissorRect(int x, int y, int w, int h)
    {
        X = x;
        Y = y;
        W = w;
        H = h;
    }

    public ScissorRect(float x, float y, float w, float h)
    {
        X = (int)x;
        Y = (int)y;
        W = (int)w;
        H = (int)h;
    }
}


public unsafe class OpenGLRenderer : IRenderer
{
    [StructLayout(LayoutKind.Sequential)]
    public struct PrimitiveCall
    {
        public Matrix4 Model;
        public Vector4 Color;
    }

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
    private readonly OpenGLShader _fontOpenGlShader;
    private readonly int _fontVao;

    public static OpenGLFramebuffer Framebuffer;
    private Stack<IFramebuffer> FramebufferStack = new();

    private readonly uint[] _indices =
    [
        0, 2, 1, // first triangle
        1, 2, 3 // second triangle
    ];
    
    private readonly OpenGLBufferObject<uint> _quadIndexOpenGlBuffer;
    private readonly OpenGLBufferObject<float> _quadVertexOpenGlBuffer;

    private readonly OpenGLShader _spriteOpenGlShader;
    private readonly VertexArrayObject<float, uint> _spriteVao;

    Stack<ScissorRect> _scissors = new();

    private readonly float[] _vertices =
    [
        // position         // uv
        0f, 0f, 0f, 0f, 1f, // top-left
        1f, 0f, 0f, 1f, 1f, // top-right
        0f, 1f, 0f, 0f, 0f, // bottom-left
        1f, 1f, 0f, 1f, 0f // bottom-right
    ];

    float[] lineVertices =
    {
        -0.5f, 0.0f, // start point
        0.5f, 0.0f // end point
    };
    
    public Window Window { get; private set; }
    
    public static ITexture WhiteTexture;
    private readonly ITexture _circleTexture;

    private OpenGLGraphicsDevice device;
    private static SDL_Window* _window;
    public uint DrawCallCount { get; private set; }
    
    public OpenGLRenderer(OpenGLGraphicsDevice device, Window window)
    {
        this.device = device;
        Window = window;
        _quadVertexOpenGlBuffer = new OpenGLBufferObject<float>(_vertices, BufferTarget.ArrayBuffer, BufferUsage.StaticDraw);
        _quadIndexOpenGlBuffer = new OpenGLBufferObject<uint>(_indices, BufferTarget.ElementArrayBuffer, BufferUsage.StaticDraw);
        _spriteVao = new VertexArrayObject<float, uint>(_quadVertexOpenGlBuffer, _quadIndexOpenGlBuffer);
        _spriteVao.VertexAttributePointer(0, 3, VertexAttribPointerType.Float, 5 * sizeof(float), 0);
        _spriteVao.VertexAttributePointer(1, 2, VertexAttribPointerType.Float, 5 * sizeof(float), 3 * sizeof(float));
        _spriteOpenGlShader = new OpenGLShader("sprite");

        GL.PixelStorei(PixelStoreParameter.UnpackAlignment, 1);

        WhiteTexture = device.CreateTexture(Resources.GetPath("Resources/Textures/white.png"));
        _circleTexture = device.CreateTexture(Resources.GetPath("Resources/Textures/circle.png"));

        _fontOpenGlShader = new OpenGLShader("text");
        _fontOpenGlShader.Use();
        _fontOpenGlShader.SetInt("uTexture", 0);
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
        GL.BufferData(BufferTarget.ArrayBuffer, 1024 * 8 * Marshal.SizeOf<FontCall>(), IntPtr.Zero,
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
    }

    ScissorRect Intersect(ScissorRect a, ScissorRect b)
    {
        int x1 = Math.Max(a.X, b.X);
        int y1 = Math.Max(a.Y, b.Y);
        int x2 = Math.Min(a.X + a.W, b.X + b.W);
        int y2 = Math.Min(a.Y + a.H, b.Y + b.H);

        return new ScissorRect
        {
            X = x1,
            Y = y1,
            W = Math.Max(0, x2 - x1),
            H = Math.Max(0, y2 - y1)
        };
    }
    
    public void PushScissor(int x, int y, int w, int h) => PushScissor(new ScissorRect(x, y, w, h));
    public void PushScissor(float x, float y, float w, float h) => PushScissor(new ScissorRect(x, y, w, h));

    public void PushScissor(ScissorRect r)
    {
        if (_scissors.Count > 0)
        {
            ScissorRect rect;
            if (_scissors.Count == 0)
            {
                rect = new ScissorRect(0, 0,
                    (int)Window.WindowSize.X, (int)Window.WindowSize.Y);
            }
            else
            {
                rect = _scissors.Peek();
            }

            var p = rect;

            int x1 = Math.Max(p.X, r.X);
            int y1 = Math.Max(p.Y, r.Y);
            int x2 = Math.Min(p.X + p.W, r.X + r.W);
            int y2 = Math.Min(p.Y + p.H, r.Y + r.H);

            r = new ScissorRect
            {
                X = x1,
                Y = y1,
                W = Math.Max(0, x2 - x1),
                H = Math.Max(0, y2 - y1)
            };
        }

        _scissors.Push(r);
        SetScissor(r);
    }

    public void PopScissor()
    {
        _scissors.Pop();
        ScissorRect rect;
        if (_scissors.Count == 0)
        {
            rect = new ScissorRect(0, 0,
                (int)Window.WindowSize.X, (int)Window.WindowSize.Y);
        }
        else
        {
            rect = _scissors.Peek();
        }

        SetScissor(rect);
    }

    public void PushFramebuffer(IFramebuffer framebuffer)
    {
        FramebufferStack.Push(framebuffer);
        SetFramebuffer(framebuffer);
    }

    public void PopFramebuffer()
    {
        if (FramebufferStack.Count != 0) FramebufferStack.Pop();

        SetFramebuffer(FramebufferStack.TryPeek(out var lastFramebuffer) ? lastFramebuffer : null);
    }

    public void BeginFrame()
    {
        DrawCallCount = 0;
    }

    public void EndFrame()
    {
        // doesn't do shit
    }
    
    private void SetFramebuffer(IFramebuffer? framebuffer = null)
    {
        Framebuffer = framebuffer as OpenGLFramebuffer;

        if (Framebuffer == null)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.Viewport(0, 0, (int)Window.WindowSize.X, (int)Window.WindowSize.Y);
        }
        else
        {
            Framebuffer.Bind();
            GL.Viewport(0, 0, Framebuffer.Width, Framebuffer.Height);
        }
    }

    public void SetScissor(int x, int y, int w, int h)
    {
        var rect = new ScissorRect(x, y, w, h);
        int vpW, vpH;
        if (Framebuffer == null)
        {
            vpW = (int)Window.WindowSize.X;
            vpH = (int)Window.WindowSize.Y;
        }
        else
        {
            vpW = Framebuffer.Width;
            vpH = Framebuffer.Height;
        }

        GL.Enable(EnableCap.ScissorTest);
        GL.Scissor(rect.X, vpH - rect.Y - rect.H, Math.Max(0, rect.W), Math.Max(0, rect.H));
    }

    public void SetScissor(ScissorRect rect)
    {
        int vpW, vpH;
        if (Framebuffer == null)
        {
            vpW = (int)Window.WindowSize.X;
            vpH = (int)Window.WindowSize.Y;
        }
        else
        {
            vpW = Framebuffer.Width;
            vpH = Framebuffer.Height;
        }

        GL.Enable(EnableCap.ScissorTest);
        GL.Scissor(rect.X, vpH - rect.Y - rect.H, Math.Max(0, rect.W), Math.Max(0, rect.H));
    }

    public void Clear(Color4<Rgba> color)
    {
        if (Framebuffer == null)
        {
            GL.Viewport(0, 0, (int)Window.WindowSize.X, (int)Window.WindowSize.Y);
        }
        else
        {
            GL.Viewport(0, 0, Framebuffer.Width, Framebuffer.Height);
        }

        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        GL.ClearColor(color.X, color.Y, color.Z, color.W);
        GL.Clear(ClearBufferMask.ColorBufferBit /*| ClearBufferMask.DepthBufferBit*/);
    }
    
    public void DrawRectangleCentered(Vector2 center, float w, float h, Color4<Rgba> color,
        float rotation = 0)
    {
        DrawTexture(
            WhiteTexture,
            new Vector2(center.X - w / 2f,
            center.Y - h / 2f),
            new Vector2(w,
            h),
            color,
            rotation
        );
    }

    public void DrawTexture(int texture, float x, float y, float width, float height, Color4<Rgba> color,
        float rotation = 0)
    {
        DrawCallCount++;
        int wWidth = 1280, wHeight = 720;
        if (Framebuffer == null)
        {
            wWidth = (int)Window.WindowSize.X;
            wHeight = (int)Window.WindowSize.Y;
        }
        else
        {
            wWidth = Framebuffer.Width;
            wHeight = Framebuffer.Height;
        }

        var projection =
            Matrix4.CreateOrthographicOffCenter(
                0,
                wWidth,
                wHeight,
                0,
                -1f,
                1f);

        // Correct 2D composition order (prevents skew/"wrong axis" look on non-square sprites):
        // center (unit quad) -> scale (local space) -> rotate (around center) -> translate (to sprite center).
        // Keeps rotation=0 behavior identical: [0..1] quad maps to [x..x+width, y..y+height].
        var model =
            Matrix4.CreateTranslation(-0.5f, -0.5f, 0f) *
            Matrix4.CreateScale(width, height, 1f) *
            Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(-rotation)) *
            Matrix4.CreateTranslation(x + width / 2f, y + height / 2f, 0f);

        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, texture);

        _spriteOpenGlShader.Use();
        _spriteOpenGlShader.SetInt("ourTexture", 0);
        _spriteOpenGlShader.SetColor4("color", color);
        _spriteOpenGlShader.SetMatrix4("model", model);
        _spriteOpenGlShader.SetMatrix4("view", Matrix4.Identity);
        _spriteOpenGlShader.SetMatrix4("projection", projection);

        _spriteVao.Bind();

        GL.DrawElements(
            PrimitiveType.Triangles,
            6,
            DrawElementsType.UnsignedInt,
            IntPtr.Zero);
    }

    public void DrawTexture(ITexture texture, Vector2 position, Vector2 size, Color4<Rgba> color,
        float rotation = 0)

    {
        DrawCallCount++;
        int wWidth = 1280, wHeight = 720;
        if (Framebuffer == null)
        {
            wWidth = (int)Window.WindowSize.X;
            wHeight = (int)Window.WindowSize.Y;
        }
        else
        {
            wWidth = Framebuffer.Width;
            wHeight = Framebuffer.Height;
        }

        var projection =
            Matrix4.CreateOrthographicOffCenter(
                0,
                wWidth,
                wHeight,
                0,
                -1f,
                1f);

        // Correct 2D composition order (prevents skew/"wrong axis" look on non-square sprites):
        // center (unit quad) -> scale (local space) -> rotate (around center) -> translate (to sprite center).
        // Keeps rotation=0 behavior identical: [0..1] quad maps to [x..x+width, y..y+height].
        var model =
            Matrix4.CreateTranslation(-0.5f, -0.5f, 0f) *
            Matrix4.CreateScale(size.X, size.Y, 1f) *
            Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(-rotation)) *
            Matrix4.CreateTranslation(position.X + size.X / 2f, position.Y + size.Y / 2f, 0f);

        (texture as OpenGLTexture)?.Bind();

        _spriteOpenGlShader.Use();
        _spriteOpenGlShader.SetInt("ourTexture", 0);
        _spriteOpenGlShader.SetColor4("color", color);
        _spriteOpenGlShader.SetMatrix4("model", model);
        _spriteOpenGlShader.SetMatrix4("view", Matrix4.Identity);
        _spriteOpenGlShader.SetMatrix4("projection", projection);
       

        if (Framebuffer != null)
        {
            _spriteOpenGlShader.SetVector2("resolution", new Vector2(Framebuffer.Width, Framebuffer.Height));
        }
        else
        {
            _spriteOpenGlShader.SetVector2("resolution", new Vector2(Window.WindowSize.X, Window.WindowSize.Y));
        }

        _spriteVao.Bind();

        GL.DrawElements(
            PrimitiveType.Triangles,
            6,
            DrawElementsType.UnsignedInt,
            IntPtr.Zero);
    }

    public void DrawFramebuffer(IFramebuffer framebuffer, Vector2 position, Vector2 size, Color4<Rgba> color, float rotation = 0)
    {
        DrawTexture((framebuffer as OpenGLFramebuffer).Texture, position.X, position.Y, size.X, size.Y, color,
            rotation);
    }

    public void DrawLine(float x1, float y1, float x2, float y2,
        float thickness, Color4<Rgba> color)
    {
        float dx = x2 - x1;
        float dy = y2 - y1;

        float length = MathF.Sqrt(dx * dx + dy * dy);
        float angle = MathHelper.RadiansToDegrees(MathF.Atan2(-dy, dx));

        float centerX = (x1 + x2) * 0.5f;
        float centerY = (y1 + y2) * 0.5f;

        DrawRectangleCentered(
            new Vector2(centerX, centerY),
            length,
            thickness,
            color,
            angle
        );
    }

    public void DrawLine(Vector2 begin, Vector2 end,
        float thickness, Color4<Rgba> color)
    {
        float x1 = begin.X;
        float y1 = begin.Y;
        float x2 = end.X;
        float y2 = end.Y;

        float dx = x2 - x1;
        float dy = y2 - y1;

        float length = MathF.Sqrt(dx * dx + dy * dy);
        float angle = MathHelper.RadiansToDegrees(MathF.Atan2(-dy, dx));

        float centerX = (x1 + x2) * 0.5f;
        float centerY = (y1 + y2) * 0.5f;

        DrawRectangleCentered(
            new Vector2(centerX, centerY),
            length,
            thickness,
            color,
            angle
        );
    }

    public void DrawRectangle(Vector2 position, Vector2 size, Color4<Rgba> color, float rotation = 0)
    {
        DrawRectangle(position.X, position.Y, size.X, size.Y, color, rotation);
    }

    public void DrawRectangle(float x, float y, float width, float height, Color4<Rgba> color,
        float rotation = 0)
    {
        DrawTexture((WhiteTexture as OpenGLTexture).Handle, x, y, width, height, color, rotation);
    }

    public void DrawCircle(Vector2 position, Vector2 size, Color4<Rgba> color, float rotation = 0)
    {
        DrawCircle(position.X, position.Y, size.X, size.Y, color, rotation);
    }

    public void DrawCircle(float x, float y, float width, float height, Color4<Rgba> color,
        float rotation = 0)
    {
        DrawTexture((_circleTexture as OpenGLTexture).Handle, x, y, width, height, color, rotation);
    }

    public void DrawRectangleBorder(
        float x,
        float y,
        float width,
        float height,
        float thickness,
        Color4<Rgba> color,
        bool inside = true)
    {
        if (!inside)
        {
            // Expand the rect outward so the original rect is the inner area
            x -= thickness;
            y -= thickness;
            width += thickness * 2;
            height += thickness * 2;
        }

        thickness = MathF.Min(thickness, MathF.Min(width * 0.5f, height * 0.5f));

        // Top
        DrawRectangle(x, y, width, thickness, color);

        // Bottom
        DrawRectangle(x, y + height - thickness, width, thickness, color);

        // Left
        DrawRectangle(x, y + thickness, thickness, height - thickness * 2, color);

        // Right
        DrawRectangle(x + width - thickness, y + thickness, thickness, height - thickness * 2, color);
    }

    // font stuff 
    public void DrawText(Font font, string text, Vector2 position, float pixelLineHeight, Color4<Rgba> color)
    {
        // Compute scale from desired line height
        float scale = pixelLineHeight / (font.LineHeight * font.EmSize);

        // Remove the old hack (no longer needed)
        // scale /= 1.3f;

        float x = position.X;
        float baseline = position.Y + font.Ascender * font.EmSize * scale;
        uint prev = 0;

        foreach (var c in text)
        {
            if (c == '\n')
            {
                x = position.X;
                baseline += font.LineHeight * font.EmSize * scale; // moves by exactly pixelLineHeight
                prev = 0;
                continue;
            }

            if (!font.Glyphs.TryGetValue(c, out var glyph))
                continue;

            float kern = 0f;
            if (prev != 0)
                kern = font.GetKerning(prev, c) * scale;

            if (glyph.HasGeometry)
            {
                Vector2 glyphPos = new(
                    x + glyph.XOffset * font.EmSize * scale,
                    baseline - glyph.YOffset * font.EmSize * scale - glyph.Height * scale);
                Vector2 glyphSize = new(glyph.Width * scale, glyph.Height * scale);
                DrawGlyph(glyph, glyphPos, glyphSize, color, font.DistanceRange);
            }

            x += glyph.XAdvance * font.EmSize * scale + kern;
            prev = c;
        }
    }

    public void DrawTextWrapped(
        Font font,
        string text,
        Vector2 position,
        float pixelLineHeight,
        float maxWidth,
        Color4<Rgba> color)
    {
        float scale = pixelLineHeight / (font.LineHeight * font.EmSize);

        float startX = position.X;
        float x = startX;
        float baseline = position.Y + font.Ascender * font.EmSize * scale;

        uint prev = 0;

        int i = 0;

        while (i < text.Length)
        {
            // Handle explicit newline
            if (text[i] == '\n')
            {
                x = startX;
                baseline += font.LineHeight * font.EmSize * scale;
                prev = 0;
                i++;
                continue;
            }

            // Build a word (or single char fallback)
            int wordStart = i;
            float wordWidth = 0f;
            uint tempPrev = prev;

            while (i < text.Length && text[i] != ' ' && text[i] != '\n')
            {
                char c = text[i];

                if (!font.Glyphs.TryGetValue(c, out var glyph))
                {
                    i++;
                    continue;
                }

                float kern = 0f;
                if (tempPrev != 0)
                    kern = font.GetKerning(tempPrev, c) * scale;

                wordWidth += (glyph.XAdvance * font.EmSize * scale) + kern;

                tempPrev = c;
                i++;
            }

            // If word doesn't fit → wrap
            if (x > startX && x + wordWidth > startX + maxWidth)
            {
                x = startX;
                baseline += font.LineHeight * font.EmSize * scale;
                prev = 0;
            }

            // Render word
            for (int j = wordStart; j < i; j++)
            {
                char c = text[j];

                if (!font.Glyphs.TryGetValue(c, out var glyph))
                    continue;

                float kern = 0f;
                if (prev != 0)
                    kern = font.GetKerning(prev, c) * scale;

                if (glyph.HasGeometry)
                {
                    Vector2 glyphPos = new(
                        x + glyph.XOffset * font.EmSize * scale,
                        baseline - glyph.YOffset * font.EmSize * scale - glyph.Height * scale);

                    Vector2 glyphSize = new(
                        glyph.Width * scale,
                        glyph.Height * scale);

                    DrawGlyph(glyph, glyphPos, glyphSize, color, font.DistanceRange);
                }

                x += glyph.XAdvance * font.EmSize * scale + kern;
                prev = c;
            }

            // Space handling
            if (i < text.Length && text[i] == ' ')
            {
                if (font.Glyphs.TryGetValue(' ', out var spaceGlyph))
                    x += spaceGlyph.XAdvance * font.EmSize * scale;

                i++;
                prev = 0;
            }
        }
    }

    private void DrawGlyph(Font.Glyph glyph, Vector2 position, Vector2 size, Color4<Rgba> color,
        float distanceRange)
    {
        var model = Matrix4.CreateScale(size.X, size.Y, 1f) * Matrix4.CreateTranslation(position.X, position.Y, 0f);
        _fontCalls.Add(new FontCall
        {
            Model = model, Color = color, UV = new Vector4(glyph.U0, glyph.V0, glyph.U1, glyph.V1), GlyphSize = size,
            DistanceRange = distanceRange
        });
    }

    public Vector2 MeasureText(Font font, string text, float pixelLineHeight)
    {
        float scale = pixelLineHeight / (font.LineHeight * font.EmSize);

        float x = 0f;
        float maxX = 0f;
        float lineHeight = font.LineHeight * font.EmSize * scale;

        uint prev = 0;

        foreach (var c in text)
        {
            if (c == '\n')
            {
                maxX = MathF.Max(maxX, x);
                x = 0f;
                prev = 0;
                continue;
            }

            if (!font.Glyphs.TryGetValue(c, out var glyph))
                continue;

            float kern = prev != 0 ? font.GetKerning(prev, c) * scale : 0f;

            x += glyph.XAdvance * font.EmSize * scale + kern;
            prev = c;
        }

        maxX = MathF.Max(maxX, x);

        return new Vector2(maxX, lineHeight + (text.Count(c => c == '\n') * lineHeight));
    }

    public Vector2 MeasureTextWrapped(
        Font font,
        string text,
        float pixelLineHeight,
        float maxWidth)
    {
        float scale = pixelLineHeight / (font.LineHeight * font.EmSize);

        float startX = 0f;
        float x = 0f;
        float maxX = 0f;

        float lineHeight = font.LineHeight * font.EmSize * scale;
        float widthLimit = maxWidth;

        int lineCount = 1;

        uint prev = 0;
        int i = 0;

        while (i < text.Length)
        {
            if (text[i] == '\n')
            {
                maxX = MathF.Max(maxX, x);
                x = 0f;
                prev = 0;
                lineCount++;
                i++;
                continue;
            }

            int wordStart = i;
            float wordWidth = 0f;
            uint tempPrev = prev;

            while (i < text.Length && text[i] != ' ' && text[i] != '\n')
            {
                char c = text[i];

                if (font.Glyphs.TryGetValue(c, out var glyph))
                {
                    float kern = tempPrev != 0 ? font.GetKerning(tempPrev, c) * scale : 0f;
                    wordWidth += glyph.XAdvance * font.EmSize * scale + kern;
                    tempPrev = c;
                }

                i++;
            }

            if (x > 0f && x + wordWidth > widthLimit)
            {
                maxX = MathF.Max(maxX, x);
                x = 0f;
                lineCount++;
                prev = 0;
            }

            for (int j = wordStart; j < i; j++)
            {
                char c = text[j];

                if (!font.Glyphs.TryGetValue(c, out var glyph))
                    continue;

                float kern = prev != 0 ? font.GetKerning(prev, c) * scale : 0f;

                x += glyph.XAdvance * font.EmSize * scale + kern;
                prev = c;
            }

            if (i < text.Length && text[i] == ' ')
            {
                if (font.Glyphs.TryGetValue(' ', out var sg))
                    x += sg.XAdvance * font.EmSize * scale;

                i++;
                prev = 0;
            }
        }

        maxX = MathF.Max(maxX, x);

        float height = lineCount * lineHeight;

        return new Vector2(maxX, height);
    }
    
    public void DrawTextCentered(
        Font font,
        string text,
        Vector2 center,
        float pixelLineHeight,
        Color4<Rgba> color)
    {
        Vector2 size = MeasureText(font, text, pixelLineHeight);

        Vector2 topLeft = new Vector2(
            center.X - size.X * 0.5f,
            center.Y - size.Y * 0.5f
        );

        DrawText(font, text, topLeft, pixelLineHeight, color);
    }
    
    public void DrawTextWrappedCentered(
        Font font,
        string text,
        Vector2 center,
        float pixelLineHeight,
        float maxWidth,
        Color4<Rgba> color)
    {
        Vector2 size = MeasureTextWrapped(font, text, pixelLineHeight, maxWidth);

        Vector2 topLeft = new Vector2(
            center.X - size.X * 0.5f,
            center.Y - size.Y * 0.5f
        );

        DrawTextWrapped(font, text, topLeft, pixelLineHeight, maxWidth, color);
    }

    public void FlushText(Font font)
    {
        int wWidth = 1280, wHeight = 720;
        if (Framebuffer == null)
        {
            wWidth = (int)Window.WindowSize.X;
            wHeight = (int)Window.WindowSize.Y;
        }
        else
        {
            wWidth = Framebuffer.Width;
            wHeight = Framebuffer.Height;
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
        _fontOpenGlShader.Use();
        _fontOpenGlShader.SetMatrix4("uProjection", projection);

        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, ((font.AtlasTexture as OpenGLTexture)!).Handle);
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
        public Color4<Rgba> Color;
        public Vector4 UV;
        public Vector2 GlyphSize;
        public float DistanceRange;
    }

    public void Dispose()
    {
        GL.DeleteBuffer(_fontInstanceVbo);
        GL.DeleteBuffer(_fontQuadVbo);
        GL.DeleteVertexArray(_fontVao);

        _quadVertexOpenGlBuffer.Dispose();
        _quadIndexOpenGlBuffer.Dispose();
        _spriteVao.Dispose();
        WhiteTexture.Dispose();
        _spriteOpenGlShader.Dispose();
        _fontOpenGlShader.Dispose();
    }
}