using OpenTK.Mathematics;

namespace Velto.Graphics.OpenGL;

public class OpenGLRenderer : IRenderer
{
    public IWindow Window { get; }
    
    public void PushScissor(ScissorRect rect)
    {
        throw new NotImplementedException();
    }

    public void PushScissor(int x, int y, int w, int h)
    {
        throw new NotImplementedException();
    }

    public void PopScissor()
    {
        throw new NotImplementedException();
    }

    public void PushFramebuffer(IFramebuffer framebuffer)
    {
        throw new NotImplementedException();
    }

    public void PopFramebuffer()
    {
        throw new NotImplementedException();
    }

    public void BeginFrame()
    {
        throw new NotImplementedException();
    }

    public void EndFrame()
    {
        throw new NotImplementedException();
    }

    public void Clear(Color4<Rgba> color)
    {
        throw new NotImplementedException();
    }

    public void DrawTexture(ITexture texture, Vector2 position, Vector2 size, Color4<Rgba> color, float rotation = 0)
    {
        throw new NotImplementedException();
    }

    public void DrawRectangle(Vector2 position, Vector2 size, Color4<Rgba> color, float rotation = 0)
    {
        throw new NotImplementedException();
    }

    public void DrawCircle(Vector2 position, Vector2 size, Color4<Rgba> color, float rotation = 0)
    {
        throw new NotImplementedException();
    }

    public Texture CreateTexture(int width, int height, TextureFilteringMode filteringMode = TextureFilteringMode.Linear,
        TextureWrapMode wrapMode = TextureWrapMode.ClampToEdge)
    {
        throw new NotImplementedException();
    }

    public IFramebuffer CreateFramebuffer(int width, int height, TextureFilteringMode filteringMode = TextureFilteringMode.Linear)
    {
        throw new NotImplementedException();
    }
}