using Velto.Graphics;

namespace Velto.Core;

public struct ResizeEventArgs
{
    public int Width;
    public int Height;
}

public abstract class Screen : IDisposable
{
    protected IGameContext Context { get; }
    protected IGraphicsDevice GraphicsDevice { get; }
    
    protected Screen(IGraphicsDevice device, IGameContext context)
    {
        GraphicsDevice = device;
        Context = context;
        Framebuffer = device.CreateFramebuffer((int)GraphicsDevice.Window.WindowSize.X, (int)GraphicsDevice.Window.WindowSize.Y);
    } 
    
    public IFramebuffer Framebuffer;
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
    
    public virtual void OnEnter() {}
    public virtual void OnExit() {}
    public virtual void OnResize(ResizeEventArgs e) {}
    public abstract void Update(double dt);
    public abstract void Draw(IRenderer r);
    
    public void Transition(Screen to, float length = 500, 
        Func<float, float>? disappearFunc = null, Func<float, float>? appearFunc = null)
    {
        ScreenManager.Instance.Transition(this, to, length, disappearFunc, appearFunc);
    }

    public void Dispose()
    {
        Framebuffer.Dispose();
    }
}