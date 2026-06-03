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
        if (_player.ActionPrimaryPressed) _primaryCount++;
        if (_player.ActionSecondaryPressed) _secondaryCount++;
        if (_player.ActionPrimaryDown)
        {
            //_primaryCount++;
            _progressPrimary = 0;
        }

        if (_player.ActionSecondaryPressed)
        {
            //_secondaryCount++;
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
       
        // Vector4 highlightColor = new Vector4(0.6f, 0.6f, 0.6f, 1f);
        // Vector4 baseColor = new Vector4(0.9f, 0.9f, 1.0f, 1f);
        //
        // _renderer.DrawRectangle(Width - 180, Height/2f - 200, 300, 400, new Vector4(0.3f, 0.5f, 0.8f, 0.95f));
        //
        // Vector2 primaryCenter = new Vector2(Width - 160 + 70, Height / 2f - 100);
        // DrawCenteredRect(primaryCenter, 140 + _bounceValPrimary, 160 + _bounceValPrimary, Lerp(baseColor, highlightColor, EasingFunctions.OutQuad(_progressPrimary)));
        //
        //
        // Vector2 secondaryCenter = new Vector2(Width - 160 + 70, Height / 2f + 100);
        // DrawCenteredRect(secondaryCenter, 140 + _bounceValSecondary, 160 + _bounceValSecondary, Lerp(baseColor, highlightColor, EasingFunctions.OutQuad(_progressSecondary)));
        //
        // _renderer.DrawText(_font, $"{_primaryCount}", new Vector2(Width - 180 + 10, Height / 2f - 200 + 60), 1.3f,
        //     new Vector4(0, 0, 0, 1));
        // _renderer.DrawText(_font, $"{_secondaryCount}", new Vector2(Width - 180 + 10, Height / 2f + 60), 1.3f,
        //     new Vector4(0, 0, 0, 1));
        // _renderer.FlushText(_font);
        
        //my rotation logic is fuckign stupid
        var bgTextureHeight= Height / 14; // magic number
        var bgTextureWidth = _gameView.Skin.InputOverlayBackground.Width * (bgTextureHeight/ _gameView.Skin.InputOverlayBackground.Height);
        
        _renderer.DrawCenteredTexture(_gameView.Skin.InputOverlayBackground, new(Width - bgTextureHeight/2, Height/2), 
            bgTextureWidth, bgTextureHeight, new Vector4(1, 1, 1, 1), 270);
        
        Vector2[] points = new Vector2[4];
        points[0] = new Vector2(Width - bgTextureHeight / 2, (Height / 2) - ((bgTextureWidth) / 8 * 3));
        
        var keyTexture = _gameView.Skin.InputOverlayKey;
        var ratioW = (float)_gameView.Skin.InputOverlayKey.Width / _gameView.Skin.InputOverlayBackground.Width;
        var ratioH = (float)_gameView.Skin.InputOverlayKey.Height / _gameView.Skin.InputOverlayBackground.Height;
        
        _renderer.DrawCenteredTexture(keyTexture, points[0], bgTextureHeight*ratioH, bgTextureWidth*ratioW, new Vector4(1, 1, 1, 1), 0);
        
        for (int i = 0; i < 4; i++)
        {
           
        }
    }

    public override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);

        Width = e.Width;
        Height = e.Height;
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