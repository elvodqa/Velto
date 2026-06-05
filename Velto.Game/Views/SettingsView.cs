using OpenTK.Mathematics;
using Velto.Core;
using Velto.Graphics;

namespace Velto.Game.Views;

public class SettingsView : View
{
    public enum PanelState
    {
        Open,
        Closed,
    }
    
    private float _maxWidth = 400;
    private float _currentWidth = 0;
    private PanelState _state = PanelState.Closed;
    
    private float _progress; // 0 = closed, 1 = open
    private const float Duration = 0.3f;
    private MSDFFont _font;
    private float _cursor = 0;
    private bool _isMouseHovering = false;

   
    public SettingsView()
    {
        _font = MSDFFont.Load(Resources.GetPath("Resources/Fonts/arial/arial"));
    }
   
    public override void Update(double delta)
    {
        X = 0;
        
        float step = ((float)delta/1000) / Duration;

        if (_state == PanelState.Open)
        {
            _progress = MathF.Min(_progress + step, 1f);
        }
        else
        {
            _progress = MathF.Max(_progress - step, 0f);
        }

        float eased = _state == PanelState.Open
            ? EasingFunctions.OutCubic(_progress)
            : EasingFunctions.InCubic(_progress);

        _currentWidth = _maxWidth * eased;
        
        _cursor += Input.WheelY * 10;
        _cursor = Math.Clamp(_cursor, -4000, 0);
    }

    public override void Draw(double delta, Renderer r)
    {
        _maxWidth = Math.Max(500 * r.DisplayScale, Renderer.WindowSizeInPixels.X / 4f);
        
        if (_currentWidth == 0) return;
        r.Clear(new(0, 0, 0, 0));
        r.SetScissor(0, 0, (int)_currentWidth, (int)Height);
        r.DrawRectangle(0, 0, _currentWidth, Height, new Color4<Rgba>(74f/255, 79f/255, 33f/255, 1));
        
        int leftPad = 50, rightPad = 50;
        int topPad = 50, bottomPad = 50;
        var contentWidth = _currentWidth - leftPad - rightPad;
        
        var settingsTextSize = contentWidth / 7;
        r.DrawText(_font, "Settings", new Vector2(leftPad, rightPad), settingsTextSize, new Color4<Rgba>(1, 1, 1, 1));
        r.FlushText(_font);
        
        float contentY = settingsTextSize + topPad;
        
        r.SetScissor(leftPad, (int)(contentY), (int)_currentWidth - leftPad - rightPad, (int)((int)Height - contentY - bottomPad));
        r.DrawRectangleBorder(
            leftPad,
            contentY,
            _currentWidth - leftPad - rightPad,
            Height - contentY - bottomPad,
            4,
            new Color4<Rgba>(1,1,1,1));

        var textBuffer = "";
        for (int i = 0; i < 100; i++)
        {
            textBuffer += " Fooo bar ";
        }
        
        
        r.DrawTextWrapped(_font, textBuffer, new Vector2(leftPad*2, contentY + topPad),  45,contentWidth - rightPad, new Color4<Rgba>(1, 1, 1, 1));
        r.FlushText(_font);
    }

    public override bool HitTest(float mouseX, float mouseY)
    {
        return (mouseX >= X && mouseX <= X + _currentWidth) && (mouseY >= Y && mouseY <= Y + Height);
    }

    public override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        //Width = e.Width;
        Height = e.Height;
        Width = _maxWidth;
    }

    public override void OnMouseEnter()
    {
        base.OnMouseEnter();
        _isMouseHovering = true;
    }

    public override void OnMouseDown(MouseButton button, MouseEventArgs e)
    {
        base.OnMouseDown(button, e);
       
    }

    public override void OnMouseUp(MouseButton button, MouseEventArgs e)
    {
        base.OnMouseDown(button, e);
    }

    public override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
    }

    public override void OnMouseLeave()
    {
        base.OnMouseLeave();
        _isMouseHovering = false;
    }

    public void Open()
    {
        _state = PanelState.Open;
    }

    public void Close()
    {
        _state = PanelState.Closed;
    }

    public void Toggle()
    {
        if (_state == PanelState.Closed)
        {
            _state = PanelState.Open;
        }
        else  
        {
            _state = PanelState.Closed;
        }
    }
}