using Velto.Graphics;

namespace Velto.Core;

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
    protected IGameContext Context { get; }

    protected View(IGameContext context)
    {
        Context = context;
        Framebuffer = new Framebuffer((int)Renderer.WindowSizeInPixels.X, (int)Renderer.WindowSizeInPixels.Y);
    } 
    
    // public static T Create<T>(int width, int height)
    //     where T : View<TContext>, new()
    // {
    //     var view = new T();
    //     view.Framebuffer = new Framebuffer(width, height);
    //     return view;
    // }
    //
    // public static T Create<T>()
    //     where T : View<TContext>, new()
    // {
    //     var view = new T();
    //     view.Framebuffer = ;
    //     return view;
    // }
    
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
    
    public virtual void OnEnter() {}
    public virtual void OnExit() {}
    public virtual void OnResize(ResizeEventArgs e) {}
    public virtual void OnMouseEnter() { }
    public virtual void OnMouseLeave() { }
    public virtual void OnMouseUp(MouseButton button, MouseEventArgs e) { }

    public virtual void OnMouseDown(MouseButton button, MouseEventArgs e) { }

    public virtual void OnMouseMove(MouseEventArgs e) { }
    
    public abstract void Update(double dt);
    public abstract void Draw(double dt, Renderer r);

    public void Dispose()
    {
        Framebuffer.Dispose();
    }
}