namespace Velto.Core;

public enum MouseButton
{
    None,
    Right,
    Left,
    Middle,
}

public struct MouseEventArgs(int x, int y)
{
    public int X = x;
    public int Y = y;
}

public interface IInputReceiver
{
    float X { get; }
    float Y { get; }
    float Width { get; }
    float Height { get; }

    bool HitTest(float mouseX, float mouseY);

    void OnMouseEnter();
    void OnMouseLeave();
    void OnMouseMove(MouseEventArgs e);
    void OnMouseDown(MouseButton button, MouseEventArgs e);
    void OnMouseUp(MouseButton button, MouseEventArgs e);
}