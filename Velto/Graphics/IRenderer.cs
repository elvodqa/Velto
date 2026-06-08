using OpenTK.Mathematics;
using Velto.Graphics.OpenGL;

namespace Velto.Graphics;

public interface IWindow
{
}

public interface IRenderer : IDisposable
{
    public uint DrawCallCount { get; }
    public Window Window { get; }

    void PushScissor(ScissorRect rect);
    void PushScissor(int x, int y, int w, int h);
    void PopScissor();

    void PushFramebuffer(IFramebuffer framebuffer);
    void PopFramebuffer();

    void BeginFrame();
    void EndFrame();

    void Clear(Color4<Rgba> color);

    void DrawTexture(ITexture texture, float x, float y, float width, float height, Color4<Rgba> color,
        float rotation = 0) => DrawTexture(texture, new Vector2(x, y), new Vector2(width, height), color, rotation);
    void DrawTexture(ITexture texture, Vector2 position, Vector2 size, Color4<Rgba> color, float rotation = 0);
    void DrawFramebuffer(IFramebuffer framebuffer, Vector2 position, Vector2 size, Color4<Rgba> color, float rotation = 0);

    void DrawTextureCentered(ITexture texture, Vector2 position, Vector2 size, Color4<Rgba> color,
        float rotation = 0)
    {
        DrawTexture(texture,
            new Vector2(position.X - size.X / 2, position.Y - size.Y / 2),
            size, color, rotation);
    }

    void DrawRectangle(float x, float y, float width, float height, Color4<Rgba> color, float rotation = 0) =>
        DrawRectangle(new Vector2(x, y), new Vector2(width, height), color, rotation);
    void DrawRectangle(Vector2 position, Vector2 size, Color4<Rgba> color, float rotation = 0);

    void DrawRectangleCentered(Vector2 position, Vector2 size, Color4<Rgba> color, float rotation = 0)
    {
        DrawRectangle(new Vector2(position.X - size.X / 2, position.Y - size.Y / 2),
            size, color, rotation);
    }

    void DrawCircle(float x, float y, float width, float height, Color4<Rgba> color, float rotation = 0) =>
        DrawCircle(new Vector2(x, y), new Vector2(width, height), color, rotation);
    void DrawCircle(Vector2 position, Vector2 size, Color4<Rgba> color, float rotation = 0);

    void DrawText(Font font, string text, Vector2 position, float pixelLineHeight, Color4<Rgba> color);
    void DrawTextWrapped(
        Font font,
        string text,
        Vector2 position,
        float pixelLineHeight,
        float maxWidth,
        Color4<Rgba> color);

    public void DrawTextCentered(
        Font font,
        string text,
        Vector2 center,
        float pixelLineHeight,
        Color4<Rgba> color);

    public void DrawTextWrappedCentered(
        Font font,
        string text,
        Vector2 center,
        float pixelLineHeight,
        float maxWidth,
        Color4<Rgba> color);

    public void FlushText(Font font);

}