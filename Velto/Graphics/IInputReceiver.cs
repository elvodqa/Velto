namespace Velto.Graphics;

public enum MouseButton
{
    None,
    Right,
    Left,
    Middle,
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