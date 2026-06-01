using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using Velto.Core;
using Velto.Gameplay;
using Velto.Graphics;

namespace Velto.Views;

public class InputOverlayView : View
{
    private Renderer _renderer;
    private GameView _gameView;
    private MSDFFont _font;
    private Player _player;

    private int _primaryCount = 0;
    private int _secondaryCount = 0;
    
    private const float Duration = 0.15f;

    private float _progressPrimary = 0;
    private float _bounceValPrimary = 0;
    
    private float _progressSecondary = 0;
    private float _bounceValSecondary = 0;
    
    
    public InputOverlayView(Renderer renderer, GameView gameview, MSDFFont font)
    {
        _gameView = gameview;
        _renderer = renderer;
        _font = font;
    }

    public void SetPlayer(Player player)
    {
        _player = player;
    }
    
    public override void Update(double delta)
    {
        if (_player.ActionPrimaryPressed)
        {
            _primaryCount++;
            _progressPrimary = 0;
        }

        if (_player.ActionSecondaryPressed)
        {
            _secondaryCount++;
            _progressSecondary = 0;
        }
        
        
        float step = ((float)delta/1000) / Duration;
        
        _progressPrimary = MathF.Min(_progressPrimary + step, 1f);
        float easePrimary = EasingFunctions.Bounce01To0(_progressPrimary);
        _bounceValPrimary = MathHelper.Lerp(_bounceValPrimary, easePrimary * -20f, 0.25f);
        
        _progressSecondary = MathF.Min(_progressSecondary + step, 1f);
        float easeSecondary = EasingFunctions.Bounce01To0(_progressSecondary);
        _bounceValSecondary = MathHelper.Lerp(_bounceValSecondary, easeSecondary * -20f, 0.25f);
    }



    public override void Draw(double delta)
    {
       
        Vector4 highlightColor = new Vector4(0.6f, 0.6f, 0.6f, 1f);
        Vector4 baseColor = new Vector4(0.9f, 0.9f, 1.0f, 1f);
        
        _renderer.DrawRectangle(Width - 180, Height/2f - 200, 300, 400, new Vector4(0.3f, 0.5f, 0.8f, 0.95f));
        // var bg = _gameView.Skin.InputOverlayBackground;
        // var height = 500f;
        // var width = height * ((float)bg.Height / bg.Width);
        // var x = Width - width;
        // var y = (Height - height) / 2f;
        //
        // _renderer.DrawTexture(bg, x, y, width, height, new Vector4(1, 1, 1, 1), 90);
        //
        // var key = _gameView.Skin.InputOverlayKey;
        //
        //
        // var keyHeightPrimary = height / 4 - 20 + _bounceValPrimary;
        // var keyWidthPrimary = keyHeightPrimary * ((float)key.Width / key.Height) + _bounceValPrimary;
        //
        // var baseKeyy = y + 30;
        // var baseKey1x = x + 30 - _bounceValPrimary;
        // var baseKey1y = y + 30 - _bounceValPrimary;
        //
        //
        // var keyHeightSecondary = height / 4 - 20 + _bounceValSecondary;
        // var keyWidthSecondary = keyHeightSecondary * ((float)key.Width / key.Height) + _bounceValSecondary;
        //
        // var baseKey2x = baseKey1x - _bounceValSecondary;
        // var baseKey2y = baseKeyy + keyHeightSecondary - _bounceValSecondary;
        //
        //
        // _renderer.DrawTexture(
        //     key,
        //     baseKey1x,
        //     baseKey1y,
        //     keyHeightPrimary,
        //     keyWidthPrimary,
        //     new Vector4(1, 1, 1, 1),
        //     90
        // );
        //
        // _renderer.DrawTexture(
        //     key,
        //     baseKey2x,
        //     baseKey2y,
        //     keyHeightSecondary,
        //     keyWidthSecondary,
        //     new Vector4(1, 1, 1, 1),
        //     90
        // );
        Vector2 primaryCenter = new Vector2(Width - 160 + 70, Height / 2f - 100);
        DrawCenteredRect(primaryCenter, 140 + _bounceValPrimary, 160 + _bounceValPrimary, Lerp(baseColor, highlightColor, EasingFunctions.OutQuad(_progressPrimary)));


        Vector2 secondaryCenter = new Vector2(Width - 160 + 70, Height / 2f + 100);
        DrawCenteredRect(secondaryCenter, 140 + _bounceValSecondary, 160 + _bounceValSecondary, Lerp(baseColor, highlightColor, EasingFunctions.OutQuad(_progressSecondary)));
    
        
            
        
        _renderer.DrawText(_font, $"{_primaryCount}", new Vector2(Width - 180 + 10, Height / 2f - 200 + 60), 1.3f,
            new Vector4(0, 0, 0, 1));
        _renderer.DrawText(_font, $"{_secondaryCount}", new Vector2(Width - 180 + 10, Height / 2f + 60), 1.3f,
            new Vector4(0, 0, 0, 1));
        _renderer.FlushText(_font);
    }
    
    void DrawCenteredRect(Vector2 center, float w, float h, Vector4 color)
    {
        _renderer.DrawRectangle(
            center.X - w / 2f,
            center.Y - h / 2f,
            w,
            h,
            color
        );
    }

    public void Reset()
    {
        _primaryCount = 0;
        _secondaryCount = 0;
    }
    
    public static Vector4 Lerp(Vector4 a, Vector4 b, float t)
    {
        return new Vector4(
            MathHelper.Lerp(a.X, b.X, t),
            MathHelper.Lerp(a.Y, b.Y, t),
            MathHelper.Lerp(a.Z, b.Z, t),
            MathHelper.Lerp(a.W, b.W, t)
        );
    }
}