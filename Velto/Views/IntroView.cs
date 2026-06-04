using OpenTK.Mathematics;
using Velto.Core;
using Velto.Graphics;

namespace Velto.Views;

public class IntroView : View
{
    private Texture _backgroundTexture;
    
    
    public override void Update(double dt)
    {
        
    }

    public override void Draw(double dt, Renderer r)
    {
        r.PushScissor(new ScissorRect(X, Y, Width, Height));
        r.Clear(Color4.Snow);
        
        if (Width * ((float)_backgroundTexture.Height / _backgroundTexture.Width) > Height)
        {
            var padding = Width -
                          Height * (_backgroundTexture.Width / (float)_backgroundTexture.Height);
            r.DrawTexture(_backgroundTexture, padding / 2, 0,
                Height * ((float)_backgroundTexture.Width / _backgroundTexture.Height), Height,
                new Color4<Rgba>(1, 1, 1, 1));
        }
        else
        {
            var padding = Height - Width * (_backgroundTexture.Height /
                                            (float)_backgroundTexture.Width);
            r.DrawTexture(_backgroundTexture, 0, padding / 2, Width,
                Width * ((float)_backgroundTexture.Height / _backgroundTexture.Width), new Color4<Rgba>(1, 1, 1, 1));
        }

        var outerCircleRadius = Height / 3;
        var innerCircleRadius = Height / 3.5f;
        
        r.DrawCircle(Width/2 - outerCircleRadius, Height/2 - outerCircleRadius, outerCircleRadius*2, outerCircleRadius*2, Color4.White);
        r.DrawCircle(Width/2 - innerCircleRadius, Height/2 - innerCircleRadius, innerCircleRadius*2, innerCircleRadius*2, Color4.Hotpink);
        
        r.PopScissor();
    }

    public override void OnEnter()
    {
        _backgroundTexture = new Texture(Resources.GetPath("Resources/Textures/bg.png"));
        
        base.OnEnter();
    }

    public override void OnExit()
    {
        _backgroundTexture.Dispose();
        base.OnExit();
    }

    public override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
    }

    public override void OnMouseDown(MouseButton button, MouseEventArgs e)
    {
        base.OnMouseDown(button, e);
        var outerCircleRadius = Height / 3;

        if (Vector2.Distance(new Vector2(e.X, e.Y), new Vector2(Width / 2, Height / 2)) < outerCircleRadius)
        {
            ViewManager.Instance.SetTree([
                Create<GameView>(),
                Create<SongSelectorView>(),
            ]);
        }
    }

    public override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        Width = e.Width;
        Height = e.Height;
    }
}