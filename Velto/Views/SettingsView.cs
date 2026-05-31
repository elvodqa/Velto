using OpenTK.Mathematics;
using Velto.Graphics;

namespace Velto.Views;

public class SettingsView : View
{
    public enum PanelState
    {
        Open,
        Closed,
    }
    
    private Renderer _renderer;

    private float _maxWidth = 600;
    private float _currentWidth = 0;
    private PanelState _state = PanelState.Closed;
    
    private float _progress; // 0 = closed, 1 = open
    private const float Duration = 0.3f;
    private MSDFFont _font;
    private float _cursor = 0;
    
    
    public SettingsView(Renderer renderer)
    {
        _renderer = renderer;
        _font = MSDFFont.Load(Resources.GetPath("Resources/Fonts/arial/arial"));
    }
   
    public override void Update(double delta)
    {
        _maxWidth = Width / 4f;
        
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

    public override void Draw(double delta)
    {
        if (_currentWidth == 0) return;
        _renderer.SetScissor(0, 0, (int)_currentWidth, (int)Height);
        _renderer.DrawRectangle(0, 0, _currentWidth, Height, new Vector4(74f/255, 79f/255, 33f/255, 1));
        
        _renderer.DrawText(_font, "Settings", new Vector2(60, _cursor + 40), 1.3f, new Vector4(1, 1, 1, 1));
        
        
        for (int i = 0; i < 100; i++)
        {
            _renderer.DrawText(_font, "Fooo bar", new Vector2(60, _cursor + 100 + i * 100), 0.7f, new Vector4(1, 1, 1, 1));
        }
        _renderer.FlushText(_font);
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
        if (_state == PanelState.Closed) _state = PanelState.Open;
        else _state = PanelState.Closed;
    }
}