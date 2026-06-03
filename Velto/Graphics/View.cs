namespace Velto.Graphics;

public struct ResizeEventArgs
{
    public int Width;
    public int Height;
}

public struct MouseEventArgs(int x, int y)
{
    public int X = x;
    public int Y = y;
}

public abstract class View : IInputReceiver, IDisposable
{
    public Framebuffer Framebuffer;
    public float X { get; set; }
    public float Y { get; set;  }
    public bool Enabled { get; set; } = true;
 
    public float Width
    {
        get
        {
            return Framebuffer.Width;
        }
        set
        {
            if ((int)value == Framebuffer.Width) return;
            Framebuffer.Resize((int)value, Framebuffer.Height);
        }
    }

    public float Height
    {
        get
        {
            return Framebuffer.Height;
        }
        set
        {
            if ((int)value == Framebuffer.Height) return;
            Framebuffer.Resize(Framebuffer.Width, (int)value);
        }
    }
    
    public virtual bool HitTest(float mouseX, float mouseY)
    {
        return mouseX >= X && mouseX <= X + Width &&
               mouseY >= Y && mouseY <= Y + Height;
    }

    public virtual void OnMouseEnter() { }
    public virtual void OnMouseLeave() { }
    public virtual void OnMouseMove(MouseEventArgs e) { }
    public virtual void OnMouseDown(MouseButton button, MouseEventArgs e) { }
    public virtual void OnMouseUp(MouseButton button, MouseEventArgs e) { }
    public virtual void OnResize(ResizeEventArgs e) { }

    public abstract void Update(double dt);
    public abstract void Draw(double dt);

    public void Dispose()
    {
        Framebuffer.Dispose();
    }
}