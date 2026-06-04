using OpenTK.Mathematics;
using SDL;
using Velto.Graphics;

namespace Velto.Core;

public class ViewManager
{
    public static ViewManager Instance { get; } = new();
    public Renderer Renderer;
    
    private readonly List<View> _views = new();
    private IInputReceiver _hovered;
    private IInputReceiver _captured;
    
    private View? GetTopView(float x, float y)
    {
        for (int i = _views.Count - 1; i >= 0; i--)
        {
            var v = _views[i];
            if (v.HitTest(x, y))
                return v;
        }
        return null;
    }
    
    public ViewManager()
    {
        
    }
    
    public void ResizeCallback(int width, int height)
    {
        var size = Renderer.WindowSizeInPixels;
        foreach (var view in _views)
        {
            view.OnResize(new()
            {
                Width = (int)size.X,
                Height = (int)size.Y
            });
        }
    }

    public void Update(double delta)
    {
        var top = GetTopView(Input.MouseX, Input.MouseY);
        // Mouse move
        IInputReceiver target = _captured ?? top;
        if (target != null)
        {
            float localX = Input.MouseX - target.X;
            float localY = Input.MouseY - target.Y;

            target.OnMouseMove(new MouseEventArgs
            {
                X = (int)localX,
                Y = (int)localY
            });
        }
        

        if (top != null)
        {
            _captured = top;

            var button = MouseButton.None;
            if (Input.IsMouseJustPressed(SDLButton.SDL_BUTTON_LEFT)) button = MouseButton.Left;
            if (Input.IsMouseJustPressed(SDLButton.SDL_BUTTON_RIGHT)) button = MouseButton.Right;
            if (Input.IsMouseJustPressed(SDLButton.SDL_BUTTON_MIDDLE)) button = MouseButton.Middle;

            if (button != MouseButton.None)
            {
                top.OnMouseDown(button, new MouseEventArgs
                {
                    X = (int)(Input.MouseX - top.X),
                    Y = (int)(Input.MouseY - top.Y)
                });
            }
            
        }
        
        if (_captured != null)
        {
            var button = MouseButton.None;
            if (Input.IsMouseJustReleased(SDLButton.SDL_BUTTON_LEFT)) button = MouseButton.Left;
            if (Input.IsMouseJustReleased(SDLButton.SDL_BUTTON_RIGHT)) button = MouseButton.Right;
            if (Input.IsMouseJustReleased(SDLButton.SDL_BUTTON_MIDDLE)) button = MouseButton.Middle;
            
            _captured.OnMouseUp(button, new MouseEventArgs
            {
                X = (int)(Input.MouseX - _captured.X),
                Y = (int)(Input.MouseY - _captured.Y)
            });

            _captured = null;
        }
        
        if (top != _hovered)
        {
            _hovered?.OnMouseLeave();

            _hovered = top;

            _hovered?.OnMouseEnter();
        }
        
        foreach (var view in _views)
        {
            view.Update(delta);
        }
    }
    
    public void Draw(double delta, Renderer r)
    {
        foreach (var view in _views)
        {
            Renderer.BindFramebuffer(view.Framebuffer);
            view.Draw(delta, r);
            Renderer.UnbindFramebuffer(view.Framebuffer);   
        }
    }

    public void Present(double delta)
    {
        Renderer.PushScissor(new ScissorRect(0, 0, (int)Renderer.WindowSizeInPixels.X, (int)Renderer.WindowSizeInPixels.Y));
        Renderer.Clear(new(0, 0, 0, 1));
        foreach (var view in _views)
        {
            Renderer.DrawTexture(view.Framebuffer.Texture, view.X, view.Y,view.Width, view.Height, new Color4<Rgba>(1, 1, 1, 1));
        }
        Renderer.PopScissor();
    }

    public void SetTree(params View[] views)
    {
        foreach (var view in _views)
        {
            view.OnExit();
        }
        _views.Clear();
        
        foreach (var view in views)
        {
            view.OnEnter();
            _views.Add(view);
        }
    }

    public List<T> Get<T>()
    {
        return _views.OfType<T>().ToList();
    }

    public View? Top => _views.Count == 0 ? null : _views[^1];
}