using OpenTK.Mathematics;

namespace Velto.Graphics;

public interface IWindow {}



public interface IRenderer
{
    public IWindow Window { get; }
    
    void PushScissor(ScissorRect rect);
    void PushScissor(int x, int y, int w, int h);
    void PopScissor();

    void PushFramebuffer(IFramebuffer framebuffer);
    void PopFramebuffer();

    void BeginFrame();
    void EndFrame();

    void Clear(Color4<Rgba> color);

    void DrawTexture(ITexture texture, Vector2 position, Vector2 size, Color4<Rgba> color, float rotation = 0);
    void DrawTextureCentered(ITexture texture, Vector2 position, Vector2 size, Color4<Rgba> color, 
        float rotation = 0) => DrawTexture(texture, 
        new Vector2(position.X - size.X / 2, position.Y - size.Y / 2), 
        size, color, rotation);
    void DrawRectangle(Vector2 position, Vector2 size, Color4<Rgba> color, float rotation = 0);
    void DrawRectangleCentered(Vector2 position, Vector2 size, Color4<Rgba> color, float rotation = 0) =>
        DrawRectangle(new Vector2(position.X - size.X / 2, position.Y - size.Y / 2),
            size, color, rotation);

    void DrawCircle(Vector2 position, Vector2 size, Color4<Rgba> color, float rotation = 0);
}