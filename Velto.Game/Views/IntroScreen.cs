using OpenTK.Mathematics;
using SDL;
using Velto.Audio;
using Velto.Core;
using Velto.Graphics;

namespace Velto.Game.Views;

public class IntroScreen : Screen
{
    private float _circleRadius;
    private AudioChannel _menuHitAudio;
    private OsuContext _context;
    
    public IntroScreen(OsuContext context) : base(context)
    {
        _context = context;
    }

    public override void Update(double dt)
    {
        var relativeSize = Math.Min(Height, Width);
        var maxCircleRadius = relativeSize / 3;

        if (Vector2.Distance(new Vector2(Input.MouseX, Input.MouseY), new Vector2(Width / 2, Height / 2)) < _circleRadius)
        {
            _circleRadius += (int)dt;
        }
        else
        {
            _circleRadius -= (int)dt;
        }
        _circleRadius = (float)Math.Clamp(_circleRadius, maxCircleRadius * 0.9f, maxCircleRadius * 1.1);

        if (Input.IsMouseJustPressed(SDLButton.SDL_BUTTON_LEFT))
        {
            if (Vector2.Distance(new Vector2(Input.MouseX, Input.MouseY), new Vector2(Width / 2, Height / 2)) < _circleRadius)
            {
                /*ViewManager.Instance.SetTree([
                    Create<GameView>(),
                    Create<SongSelectorView>(),
                ]);*/
                AudioManager.Instance.PlaySample(_menuHitAudio);
                Transition(new SongSelectScreen(_context),1000);
            }
        }

    }

    public override void Draw(double dt, Renderer r)
    {
        r.PushScissor(new ScissorRect(0, 0, Width, Height));
        r.Clear(Color4.Snow);

        r.DrawRectangle(0, 0, Width, Height, Color4.Black);


        var innerCircleRadius = _circleRadius * 0.95f;
        
        r.DrawCircle(Width/2 - _circleRadius, Height/2 - _circleRadius, _circleRadius*2, _circleRadius*2, Color4.White);
        r.DrawCircle(Width/2 - innerCircleRadius, Height/2 - innerCircleRadius, innerCircleRadius*2, innerCircleRadius*2, Color4.Hotpink);
        
        r.DrawTextWrappedCentered(Fonts.Default, "VICIOUS DIH", 
            new(Width/2, Height/2), _circleRadius/2, _circleRadius * 1.90F, Color4.White);
        r.FlushText(Fonts.Default);
        r.PopScissor();
    }

    public override void OnEnter()
    {
        _menuHitAudio = AudioManager.Instance.LoadAudio(Resources.GetPath("Resources/Textures/default/menuhit.wav"));
        AudioManager.Instance.SampleVolume = 0.5F;
        base.OnEnter();
    }

    public override void OnExit()
    {
        base.OnExit();
    }
    
    public override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        Width = e.Width;
        Height = e.Height;
    }
}