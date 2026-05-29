namespace Velto.Graphics;

using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using Velto.Gameplay;
using SDL;
using static SDL.SDL3;

public unsafe class Renderer : IDisposable
{
    private float[] vertices =
    [
        // position         // uv
        0f, 0f, 0f, 0f, 1f, // top-left
        1f, 0f, 0f, 1f, 1f, // top-right
        0f, 1f, 0f, 0f, 0f, // bottom-left
        1f, 1f, 0f, 1f, 0f // bottom-right
    ];

    private uint[] indices =
    [
        0, 2, 1, // first triangle
        1, 2, 3 // second triangle
    ];
    
    private SDL_Window* _window;
    private BufferObject<float> _quadVertexBuffer;
    private BufferObject<uint> _quadIndexBuffer;
    private VertexArrayObject<float, uint> _spriteVao;
    
    private Shader _spriteShader;
    private Shader _textShader;

    private Texture _whiteTexture;

    private bool _framebufferBound = false;


    public Renderer(SDL_Window *window)
    {
        _window = window;
        _quadVertexBuffer = new(vertices, BufferTarget.ArrayBuffer, BufferUsage.StaticDraw);
        _quadIndexBuffer = new(indices, BufferTarget.ElementArrayBuffer, BufferUsage.StaticDraw);
        _spriteVao = new VertexArrayObject<float, uint>(_quadVertexBuffer, _quadIndexBuffer);
        _spriteVao.VertexAttributePointer(0, 3, VertexAttribPointerType.Float, 5 * sizeof(float), 0);
        _spriteVao.VertexAttributePointer(1, 2, VertexAttribPointerType.Float, 5 * sizeof(float), 3 * sizeof(float));
        _spriteShader = new("sprite"); 
        _textShader = new("blurg");
        
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


    public void Dispose()
    {
        _quadVertexBuffer.Dispose();
        _quadIndexBuffer.Dispose();
        _spriteVao.Dispose();
        _whiteTexture.Dispose();
        _spriteShader.Dispose();
        _textShader.Dispose();
    }
}