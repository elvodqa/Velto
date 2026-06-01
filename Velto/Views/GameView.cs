using System.Drawing;
using ManagedBass;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using Velto.Gameplay;
using SDL;
using Velto.Audio;
using Velto.Core;
using Velto.Graphics;
using static SDL.SDL3;

namespace Velto.Views;

public unsafe class GameView : View
{
    private const int PLAYFIELD_W = 512;
    private const int PLAYFIELD_H = 384;
    private const double WAITINGTIME = 3000f;
    private double _startingTimer;

    private Texture? _backgroundTexture;

    private float _baseCircleSize;

    private Beatmap _beatmap;

    private bool _isPaused;
    private readonly MSDFFont _msdfFont;


    private bool _musicStarted;
    private float _musicVolume;

    private readonly Renderer _renderer;
    private string _skinName = "default";

    private double _songCursor;
    private double _songLength;
    private IOrderedEnumerable<HitObject> _sortedObjects;

    private Vector2 lastCursorPosition = Vector2.Zero;
    private Shader shader;

    private readonly Queue<TrailInfo> trails = new();

    private Player _player;
    private SettingsView _settingsView;
    private SongSelectorView _songSelectorView;
    private InputOverlayView _inputOverlayView;
    public Skin Skin;

    private Track? _songTrack;
    private AudioChannel? _songAudio;
    private AudioChannel? _hitSoundAudio;
    private double _audioLock = 0.0f;

    private SliderFramebuffer[] _sliderFramebuffers = new SliderFramebuffer[16];
    private float _prevWidth, _prevHeight;

    public struct SliderFramebuffer
    {
        public Framebuffer Framebuffer;
        public float Time;
        public float Duration;
        public bool InUse;
    }

    public GameView(Renderer renderer)
    {
        Skin = new Skin(Resources.GetPath($"Resources/Textures/{_skinName}"));
        _renderer = renderer;
        _msdfFont = MSDFFont.Load(Resources.GetPath("Resources/Fonts/arial/arial"));

        _settingsView = new(_renderer);
        _songSelectorView = new(_renderer, this);
        _inputOverlayView = new(_renderer, this, _msdfFont);

        _hitSoundAudio =
            AudioManager.Instance.LoadAudio(Resources.GetPath($"Resources/Textures/default/soft-hitnormal.wav"));

        for (int i = 0; i < 16; i++)
        {
            _sliderFramebuffers[i] = new()
            {
                Framebuffer = new Framebuffer(_renderer, 1280, 720),
                Duration = 0,
                Time = 0,
            };
        }


        SetBeatmap(new Beatmap(Resources.GetPath(
            "Resources/Songs/Wakeshima Kanon/ASCA - Nisemono no Koi ni Sayounara with Wakeshima Kanon (timemon) [Kyou's Extra].osu")));
    }

    float playfieldWidth, playfieldHeight;
    private Vector2 playfieldTopLeft;

    public override void Update(double delta)
    {
        // rollback if there the offset is too high
        _audioLock += delta;
        if (_audioLock >= 1000)
        {
            var songPos = _songTrack!.Position;
            if (Math.Abs(songPos - _songCursor) > 50)
            {
                _songCursor = songPos;
            }
        }

        if (_prevWidth != Width || _prevHeight != Height)
        {
            for (int i = 0; i < _sliderFramebuffers.Length; i++)
            {
                var fb = _sliderFramebuffers[i];
                fb.Framebuffer.Resize((int)Width, (int)Height);
                _sliderFramebuffers[i] = fb;
            }
        }

        if (_startingTimer > 0)
        {
            if (_songCursor != 0)
            {
                // Something move the cursor
                _musicStarted = true;
                _songTrack?.Play();
                _songTrack?.Position = _songCursor;
            }

            _startingTimer -= delta;

            if (_startingTimer <= 0 && !_musicStarted)
            {
                _musicStarted = true;
                _songTrack?.Play();
                _songCursor = 0;
            }
        }

        // Use REAL audio position when music is playing
        if (_musicStarted && !_isPaused && _songTrack != null)
        {
            _songCursor = _songTrack.Position;
            _songCursor -= 2.66f;
            //_songCursor += delta;
            _songCursor = Math.Clamp(_songCursor, 0, _songLength);
        }

        RectangleF hitbox = new(0, Height - 40, Width, 40);
        if (Input.IsMouseDown(SDLButton.SDL_BUTTON_LEFT) && hitbox.Contains(Input.MouseX, Input.MouseY))
        {
            double targetMs = Util.MapRange(Input.MouseX, 0, Width, 0, (float)_songLength);

            if (_startingTimer >= 0 && !_musicStarted)
            {
                _musicStarted = true;
                _songTrack?.Play();
                _startingTimer = 0;
            }

            bool wasPlaying = _songTrack!.Playing;
            _songTrack.Pause();
            _songTrack.Position = targetMs;
            _songCursor = targetMs;
            ResetObjectsAfter(_songCursor);

            if (wasPlaying)
                _songTrack.Resume();
        }

        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_LEFT))
        {
            _songCursor -= 1000;
            _songTrack?.Position = _songCursor;

            ResetObjectsAfter(_songCursor);
        }

        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_RIGHT))
        {
            _songCursor += 1000;
            _songTrack?.Position = _songCursor;

            ResetObjectsAfter(_songCursor);
        }


        _settingsView.Width = Width;
        _settingsView.Height = Height;
        _settingsView.Update(delta);
        _songSelectorView.Width = Width;
        _songSelectorView.Height = Height;
        _songSelectorView.Update(delta);
        _inputOverlayView.Width = Width;
        _inputOverlayView.Height = Height;

        var scale = playfieldWidth / PLAYFIELD_W;
        var osuRadius = 54.4f - 4.48f * _beatmap.CircleSize;
        _baseCircleSize = osuRadius * 2f * scale;

        // hitobjects
        var playfieldAspect = PLAYFIELD_W / (float)PLAYFIELD_H;
        var windowAspect = Width / (float)Height;
        float playfieldScale = 0.76f;

        if (windowAspect > playfieldAspect)
        {
            // window is wider → height is limiting (letterbox left/right)
            playfieldHeight = Height * playfieldScale;
            playfieldWidth = playfieldHeight * playfieldAspect;
        }
        else
        {
            // window is taller → width is limiting (letterbox top/bottom)
            playfieldWidth = Width * playfieldScale;
            playfieldHeight = playfieldWidth / playfieldAspect;
        }

        playfieldTopLeft = new(
            (Width - playfieldWidth) / 2f,
            (Height - playfieldHeight) / 2f
        );


        _sortedObjects = _beatmap.HitObjects
            .OrderByDescending(h => Math.Abs(h.Time - _songCursor));


        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_SPACE))
        {
            if (_isPaused)
            {
                _songTrack?.Resume();
            }
            else
            {
                _songTrack?.Pause();
            }

            _isPaused = !_isPaused;
        }

        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_A))
        {
            _player.Autoplay = !_player.Autoplay;
            if (_player.Autoplay) SDL_ShowCursor();
            else SDL_HideCursor();
        }

        if (Input.IsKeyDown(SDL_Scancode.SDL_SCANCODE_LCTRL))
        {
            if (Math.Abs(Input.WheelY) > 0.01f)
            {
                _musicVolume += Input.WheelY * 0.01f;
                _musicVolume = Math.Clamp(_musicVolume, 0, 1);
                //Bass.ChannelSetAttribute(_musicChannel, ChannelAttribute.Volume, _musicVolume);
                _songTrack?.Volume = _musicVolume;
            }

            if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_O))
            {
                _settingsView.Toggle();
            }

            if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_S))
            {
                Skin.Dispose();
                if (_skinName == "rafis") _skinName = "default";
                else _skinName = "rafis";
                Skin = new Skin(Resources.GetPath($"Resources/Textures/{_skinName}"));
            }
        }

        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_TAB))
        {
            _songSelectorView.Toggle();
        }

        // Update player just before doing judgement
        _player.Update(delta, _songCursor, playfieldTopLeft, scale);
        _inputOverlayView.Update(delta);
        // Handle objects bkz: https://osu.ppy.sh/wiki/en/Gameplay/Judgement/osu%21 
        foreach (var hitObject in _beatmap.HitObjects)
        {
            if (hitObject is HitCircle circle)
            {
                if (hitObject.HitResult == HitResult.None)
                {
                    if (_songCursor - 150 >= circle.Time)
                    {
                        hitObject.HitResult = HitResult.Miss;
                        hitObject.Failed = true;
                        AddResultParticle(hitObject.Position, hitObject.HitResult, hitObject.Time, 150, 400);
                    }

                    var playerCursor = _player.Cursor;
                    float radiusScreen = osuRadius * scale;
                    var circlePosition = playfieldTopLeft + circle.Position * scale;
                    if (Vector2.Distance(circlePosition, playerCursor) <= radiusScreen)
                        if (_player.ActionPrimaryPressed || _player.ActionSecondaryPressed)
                        {
                            // bkz: https://osu.ppy.sh/wiki/en/Gameplay/Judgement/osu%21
                            //circle.Color = Vector4.Zero;
                            hitObject.HitTime = _songCursor;
                            var difference = Math.Abs(_songCursor - hitObject.Time);
                            if (difference <= 80 - 6 * _beatmap.OverallDifficulty) // 300
                            {
                                hitObject.HitResult = HitResult.Good;
                            }
                            else if (difference <= 140 - 8 * _beatmap.OverallDifficulty) // 100
                            {
                                hitObject.HitResult = HitResult.Ok;
                            }
                            else if (difference <= 200 - 10 * _beatmap.OverallDifficulty) // 50
                            {
                                hitObject.HitResult = HitResult.Meh;
                            }
                            else // miss
                            {
                                hitObject.HitResult = HitResult.Miss;
                            }

                            circle.HitTime = _songCursor;
                            AddResultParticle(hitObject.Position, hitObject.HitResult, hitObject.HitTime, 150, 400);
                            AudioManager.Instance.PlaySample(_hitSoundAudio!);
                        }

                    // Noteblock
                    if (_player.ActionPrimaryPressed || _player.ActionSecondaryPressed)
                    {
                        break;
                    }
                }
            }
            
            // https://github.com/ppy/osu/wiki/Anatomy-of-a-slider
            if (hitObject is Slider slider)
            {
                if (hitObject.HitResult == HitResult.None || _songCursor < slider.Time + slider.Duration + _beatmap.Posttime/2)
                {
                    if (_songCursor - 150 >= slider.Time)
                    {
                        hitObject.HitResult = HitResult.Miss;
                        AddResultParticle(hitObject.Position, hitObject.HitResult, hitObject.Time, 150, 400);
                    }

                    var playerCursor = _player.Cursor;
                    float radiusHitCircle = osuRadius * scale;
                    var circlePosition = playfieldTopLeft + slider.Position * scale;
                    if (Vector2.Distance(circlePosition, playerCursor) <= radiusHitCircle)
                    {
                        if ((_player.ActionPrimaryPressed || _player.ActionSecondaryPressed) && slider.HitResult == HitResult.None)
                        {
                            slider.HitTime = _songCursor;
                            var difference = Math.Abs(_songCursor - slider.Time);
                            if (difference <= 80 - 6 * _beatmap.OverallDifficulty) // 300
                            {
                                hitObject.HitResult = HitResult.Good;
                            }
                            else if (difference <= 140 - 8 * _beatmap.OverallDifficulty) // 100
                            {
                                hitObject.HitResult = HitResult.Ok;
                            }
                            else if (difference <= 200 - 10 * _beatmap.OverallDifficulty) // 50
                            {
                                hitObject.HitResult = HitResult.Meh;
                            }
                            else // miss
                            {
                                hitObject.HitResult = HitResult.Miss;
                            }

                            slider.HitTime = _songCursor;
                            AddResultParticle(hitObject.Position, hitObject.HitResult, hitObject.HitTime, 150, 400);
                            AudioManager.Instance.PlaySample(_hitSoundAudio!);
                        }
                    }

                    var _scale = playfieldWidth / PLAYFIELD_W;
                    var followCirclePos = slider.GetPositionAt(_songCursor);
                    var ballPosition = playfieldTopLeft + followCirclePos * _scale;
                    var diameter = _baseCircleSize * 2.5f;
                    var radiusFollowCircle = diameter / 2f;
                    
                    bool sliderActive =
                        _songCursor >= slider.Time &&
                        _songCursor <= slider.Time + slider.Duration;
                    if (sliderActive && Vector2.Distance(ballPosition, playerCursor) <= radiusFollowCircle)
                    {
                        if (_player.ActionPrimaryDown || _player.ActionSecondaryDown)
                        {
                            //AudioManager.Instance.PlaySample(_hitSoundAudio!);
                        }
                    }

                    if (_player.ActionPrimaryPressed || _player.ActionSecondaryPressed)
                    {
                        break;
                    }
                }
            }
        }


        // if (lastCursorPosition != Vector2.Zero && Vector2.Distance(_player.Cursor, lastCursorPosition) > 8)
        //     trails.Enqueue(new TrailInfo
        //     {
        //         Position = lastCursorPosition,
        //         Life = 50
        //     });
        float distance = Vector2.Distance(_player.Cursor, lastCursorPosition);
        if (lastCursorPosition != Vector2.Zero && distance > 8)
        {
            int steps = (int)(distance / 6f);
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector2 interpolated = Vector2.Lerp(lastCursorPosition, _player.Cursor, t);

                trails.Enqueue(new TrailInfo
                {
                    Position = interpolated,
                    Life = 45 + (steps - i) * 2 // newer points live longer
                });
            }
        }


        while (trails.Count > 120)
            trails.Dequeue();


        foreach (var particle in _hitResultParticles.ToList())
        {
            //particle.Life -= (float)delta;
            if (_songCursor > particle.Begin + particle.Duration)
            {
                _hitResultParticles.Remove(particle);
            }
        }

        _prevWidth = Width;
        _prevHeight = Height;
    }

    void ResetObjectsAfter(double cursorMs)
    {
        foreach (var obj in _beatmap.HitObjects)
        {
            if (obj.Time >= cursorMs)
            {
                obj.HitTime = 0;
                obj.HitResult = HitResult.None;
                obj.Failed = false;
            }
        }
    }


    public override void Draw(double delta)
    {
        _renderer.Clear(new Vector4(0, 0, 0, 1));
        _renderer.SetScissor(0, 0, (int)Width, (int)Height);

        // bg
        if (Width * ((float)_backgroundTexture.Height / _backgroundTexture.Width) > Height)
        {
            // sağ sol bar
            var padding = Width -
                          Height * (_backgroundTexture.Width / (float)_backgroundTexture.Height);
            _renderer.DrawTexture(_backgroundTexture, padding / 2, 0,
                Height * ((float)_backgroundTexture.Width / _backgroundTexture.Height), Height,
                new Vector4(1, 1, 1, 1));
        }
        else
        {
            // üst alt bar
            var padding = Height - Width * (_backgroundTexture.Height /
                                            (float)_backgroundTexture.Width);
            _renderer.DrawTexture(_backgroundTexture, 0, padding / 2, Width,
                Width * ((float)_backgroundTexture.Height / _backgroundTexture.Width), new Vector4(1, 1, 1, 1));
        }

        // bg dim
        _renderer.DrawRectangle(0, 0, Width, Height, new Vector4(0, 0, 0, 0.55f));


        // draw playfield
        _renderer.DrawRectangle(
            playfieldTopLeft.X,
            playfieldTopLeft.Y,
            playfieldWidth,
            playfieldHeight,
            new Vector4(0, 0, 0, 0.3f)
        );


        var scale = playfieldWidth / PLAYFIELD_W;

        foreach (var hitObject in _sortedObjects)
        {
            var posX = playfieldTopLeft.X + hitObject.Position.X * scale;
            var posY = playfieldTopLeft.Y + hitObject.Position.Y * scale;
            var objectCircleSize = _baseCircleSize;
            float fadein;
            float fadeout;
            float drawSize;
            float approachCircleSize;
            if (hitObject.HitResult == HitResult.None)
            {
                fadein =
                    Math.Clamp(
                        Util.MapRange((float)_songCursor, hitObject.Time - _beatmap.Preempt,
                            hitObject.Time - _beatmap.Preempt / 3,
                            0, 1), 0, 1);
                fadeout =
                    Math.Clamp(
                        Util.MapRange((float)_songCursor, hitObject.Time, hitObject.Time + _beatmap.Posttime, 1, 0),
                        0, 1);
                drawSize = (float)(objectCircleSize + objectCircleSize * (1 - fadeout) * 0.2);
                approachCircleSize =
                    Math.Max(
                        Util.MapRange((float)_songCursor, hitObject.Time - _beatmap.Preempt, hitObject.Time + 0,
                            drawSize * 4, drawSize), drawSize);
            }
            else
            {
                fadein = 1;

                fadeout =
                    Math.Clamp(
                        Util.MapRange((float)_songCursor, (float)hitObject.HitTime,
                            (float)(hitObject.HitTime + _beatmap.Posttime), 1, 0),
                        0, 1);

                drawSize = (float)(objectCircleSize + objectCircleSize * (1 - fadeout) * 0.2);
                approachCircleSize =
                    Math.Max(
                        Util.MapRange((float)_songCursor, hitObject.Time - _beatmap.Preempt, hitObject.Time + 0,
                            drawSize * 4, drawSize), drawSize);
            }


            // Gameplay drawing
            if (hitObject is HitCircle circle)
            {
                if (hitObject.Time + _startingTimer - (int)_songCursor > -_beatmap.Posttime &&
                    hitObject.Time + _startingTimer - (int)_songCursor < _beatmap.Preempt)
                {
                    //if (hitObject.HitResult != HitResult.None) continue;
                    _renderer.DrawTexture(Skin.ApproachCircle,
                        posX - approachCircleSize / 2,
                        posY - approachCircleSize / 2,
                        approachCircleSize,
                        approachCircleSize, hitObject.Color with
                        {
                            W = Math.Min(fadein, fadeout)
                        });

                    _renderer.DrawTexture(Skin.HitCircle,
                        posX - drawSize / 2,
                        posY - drawSize / 2,
                        drawSize,
                        drawSize, hitObject.Color with { W = Math.Min(fadein, fadeout) });

                    _renderer.DrawTexture(Skin.HitCircleOverlay,
                        posX - drawSize / 2,
                        posY - drawSize / 2,
                        drawSize,
                        drawSize, new Vector4(1, 1, 1, 1) with { W = Math.Min(fadein, fadeout) });

                    var num = hitObject.ComboNumber.ToString();


                    var digitScale = drawSize / (Skin.NumbersHd ? 256f : 128f);

                    float digitWidthTotal = 0;

                    // first pass: compute total width
                    foreach (var c in num)
                    {
                        var digit = c - '0';
                        var tex = Skin.Numbers[digit];
                        digitWidthTotal += tex.Width * digitScale;
                    }

                    // start centered
                    var cursorX = posX - digitWidthTotal / 2;

                    foreach (var c in num)
                    {
                        var digit = c - '0';
                        var tex = Skin.Numbers[digit];

                        var w = tex.Width * digitScale;
                        var h = tex.Height * digitScale;

                        _renderer.DrawTexture(
                            tex,
                            cursorX,
                            posY - h / 2,
                            w,
                            h,
                            new Vector4(1, 1, 1, 1) with { W = Math.Min(fadein, fadeout) }
                        );

                        cursorX += w;
                    }
                }
            }

            if (hitObject is Slider slider)
            {
                //var sliderLife = slider.Length / (_beatmap.SliderMultiplier * 100 * _beatmap.Slid);
                //if (!(slider.Time - (int)_songCursor > - slider.Duration) ||
                //    !(slider.Time - (int)_songCursor < _beatmap.Preempt)) continue;
                if (!(_songCursor < slider.Time + slider.Duration + _beatmap.Posttime) ||
                    !(_songCursor > slider.Time - _beatmap.Preempt)) continue;

                slider.Sliding =
                    _songCursor >= slider.Time &&
                    _songCursor <= slider.Time + slider.Duration;

                int fbIndex = AcquireSliderFramebuffer();
                var fb = _sliderFramebuffers[fbIndex];
                _renderer.BindFramebuffer(fb.Framebuffer);
                _renderer.Clear(new Vector4(0, 0, 0, 0));
                GL.BlendFunc(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
                foreach (var point in slider.Points)
                {
                    var scaledX = playfieldTopLeft.X + point.X * scale;
                    var scaledY = playfieldTopLeft.Y + point.Y * scale;

                    var _drawSize = objectCircleSize * 0.92f;
                    _renderer.DrawCircle(
                        scaledX - _drawSize / 2,
                        scaledY - _drawSize / 2,
                        _drawSize,
                        _drawSize,
                        new Vector4(1, 1, 1, 1));
                }

                foreach (var point in slider.Points)
                {
                    var scaledX = playfieldTopLeft.X + point.X * scale;
                    var scaledY = playfieldTopLeft.Y + point.Y * scale;
                    var _drawSize = objectCircleSize * 0.8f;

                    _renderer.DrawCircle(
                        scaledX - _drawSize / 2,
                        scaledY - _drawSize / 2,
                        _drawSize,
                        _drawSize,
                        // hitObject.Color with { W = Math.Min(sliderFadein, sliderFadeout) });
                        new Vector4(0.1f, 0.1f, 0.1f, 1));
                }

                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                _renderer.UnbindFramebuffer(fb.Framebuffer);
                ReleaseSliderFramebuffer(fbIndex);

                _renderer.SetScissor(0, 0, (int)Width, (int)Height);

                var sliderOpacity = 0f;
                if (_songCursor > slider.Time - _beatmap.Preempt && _songCursor < slider.Time)
                {
                    sliderOpacity = Util.MapRange((float)_songCursor, slider.Time - _beatmap.Preempt,
                        slider.Time - _beatmap.Preempt / 2, 0, 1);
                }
                else if (_songCursor >= slider.Time - _beatmap.Preempt / 2 &&
                         _songCursor < slider.Time + slider.Duration)
                {
                    sliderOpacity = 1.0f;
                }
                else
                {
                    float window = (float)(slider.Time + slider.Duration);
                    sliderOpacity = Util.MapRange((float)_songCursor, window, window + _beatmap.Posttime, 1, 0);
                }

                _renderer.DrawTexture(fb.Framebuffer.Texture, 0, 0, Width, Height,
                    new Vector4(1, 1, 1, 1) { W = sliderOpacity });

                if (slider.Sliding)
                {
                    var position = slider.GetPositionAt(_songCursor);

                    var scaledX = playfieldTopLeft.X + position.X * scale;
                    var scaledY = playfieldTopLeft.Y + position.Y * scale;
                    var _drawSize = objectCircleSize * 1f;

                    Vector2 prevPos;
                    if (_songCursor == slider.Time)
                    {
                        prevPos = slider.Position;
                    }
                    else
                    {
                        prevPos = slider.GetPositionAt(_songCursor-1);
                    }
                    
                    Vector2 direction = position - prevPos;
                    var rotation = Math.Atan2(direction.Y, direction.X) * -MathHelper.RadToDeg + 180;
                    
                    if (Skin.SliderBallAnimated)
                    {
                        var ballIndex = Math.Clamp(
                            (int)Util.MapRange(
                                (float)_songCursor,
                                (float)slider.Time,
                                (float)(slider.Time + slider.Duration),
                                0,
                                Skin.SliderBalls.Count - 1),
                            0,
                            Skin.SliderBalls.Count - 1);
                        _renderer.DrawTexture(Skin.SliderBalls[ballIndex],
                            scaledX - _drawSize / 2,
                            scaledY - _drawSize / 2,
                            _drawSize,
                            _drawSize,
                            hitObject.Color with { W = 1 }, (float)rotation);
                    }
                    else
                    {
                        _renderer.DrawTexture(Skin.SliderBalls.First(),
                            scaledX - _drawSize / 2,
                            scaledY - _drawSize / 2,
                            _drawSize,
                            _drawSize,
                            hitObject.Color with { W = 1 });
                    }

                    _drawSize = objectCircleSize * 2.5f;
                    _renderer.DrawTexture(Skin.SliderFollowCircle,
                        scaledX - _drawSize / 2,
                        scaledY - _drawSize / 2,
                        _drawSize,
                        _drawSize,
                        new Vector4(1, 1, 1, 1) with { W = 1 });

                    
                    var playerCursor = _player.Cursor;
                    float radiusHitCircle = objectCircleSize / 2 * scale;
                    float radiusFollowCircle = objectCircleSize / 2 * scale;
                    var circlePosition = playfieldTopLeft + slider.Position * scale;
                    var ballPosition = playfieldTopLeft + slider.GetPositionAt(_songCursor) * scale;
                    bool sliderActive =
                        _songCursor >= slider.Time &&
                        _songCursor <= slider.Time + slider.Duration;
                    if (sliderActive)
                    {
                        _renderer.DrawCircle(ballPosition.X - radiusFollowCircle / 2,
                            ballPosition.Y - radiusFollowCircle / 2, radiusFollowCircle, radiusFollowCircle,
                            new Vector4(1, 1, 1, 0.3f));
                    }
                }

                if (slider.SlideRepeatCount > 1)
                {
                    var position = slider.Points.Last();
                    var scaledX = playfieldTopLeft.X + position.X * scale;
                    var scaledY = playfieldTopLeft.Y + position.Y * scale;
                    var _drawSize = objectCircleSize * 1f;

                    var prevPos = slider.Points[slider.Points.Count - 2];

                    Vector2 direction = position - prevPos;
                    var rotation = Math.Atan2(direction.Y, direction.X) * -MathHelper.RadToDeg + 180;

                    _renderer.DrawTexture(Skin.ReverseArrow,
                        scaledX - _drawSize / 2,
                        scaledY - _drawSize / 2,
                        _drawSize,
                        _drawSize,
                        new Vector4(1, 1, 1, 1) with { W = 1 },
                        (float)rotation
                    );
                }

                _renderer.DrawTexture(Skin.ApproachCircle,
                    posX - approachCircleSize / 2,
                    posY - approachCircleSize / 2,
                    approachCircleSize,
                    approachCircleSize, hitObject.Color with { W = Math.Min(fadein, fadeout) });

                _renderer.DrawTexture(Skin.SliderStartCircle,
                    posX - drawSize / 2,
                    posY - drawSize / 2,
                    drawSize,
                    drawSize, hitObject.Color with { W = Math.Min(fadein, fadeout) });


                if (!Skin.SliderStartCircleExists)
                {
                    _renderer.DrawTexture(Skin.HitCircleOverlay,
                        posX - drawSize / 2,
                        posY - drawSize / 2,
                        drawSize,
                        drawSize, new Vector4(1, 1, 1, 1) with { W = Math.Min(fadein, fadeout) });
                }


                var num = hitObject.ComboNumber.ToString();

                var digitScale = drawSize / (Skin.NumbersHd ? 256f : 128f);
                float digitWidthTotal = 0;

                // first pass: compute total width
                foreach (var c in num)
                {
                    var digit = c - '0';
                    var tex = Skin.Numbers[digit];
                    digitWidthTotal += tex.Width * digitScale;
                }

                // start centered
                var cursorX = posX - digitWidthTotal / 2;

                foreach (var c in num)
                {
                    var digit = c - '0';
                    var tex = Skin.Numbers[digit];

                    var w = tex.Width * digitScale;
                    var h = tex.Height * digitScale;

                    _renderer.DrawTexture(
                        tex,
                        cursorX,
                        posY - h / 2,
                        w,
                        h,
                        new Vector4(1, 1, 1, 1) with { W = Math.Min(fadein, fadeout) }
                    );

                    cursorX += w;
                }
            }
        }

        foreach (var particle in _hitResultParticles)
        {
            var posX = playfieldTopLeft.X + particle.Position.X * scale;
            var posY = playfieldTopLeft.Y + particle.Position.Y * scale;
            var objectCircleSize = _baseCircleSize;

            float t = (float)_songCursor;

            float fadein = Math.Clamp(Util.MapRange(t, (float)particle.Begin,
                (float)(particle.Begin + particle.Appear), 0, 1), 0, 1);

            float fadeout = Math.Clamp(Util.MapRange(t,
                (float)(particle.Begin + particle.Appear),
                (float)(particle.Begin + particle.Duration), 1, 0), 0, 1);

            float alpha = fadein * fadeout;

            var texture = particle.Result switch
            {
                HitResult.None => Skin.Hit0,
                HitResult.Good => Skin.Hit300,
                HitResult.Ok => Skin.Hit100,
                HitResult.Meh => Skin.Hit50,
                HitResult.Miss => Skin.Hit0,
                _ => throw new ArgumentOutOfRangeException()
            };


            var drawSize = (float)(objectCircleSize + objectCircleSize * (1 - fadeout) * 0.2);
            var w = drawSize;
            var h = texture.Height * (w / texture.Width);

            _renderer.DrawCenteredTexture(texture,
                new Vector2(posX, posY),
                w,
                h, new Vector4(1, 1, 1, 1) with { W = alpha });
        }

        var scoreboardBgWidth = Width / 2.5f;

        var bgScale = scoreboardBgWidth / Skin.ScorebarBg.Width;
        var scoreboardBgHeight = Skin.ScorebarBg.Height * bgScale;

        _renderer.DrawTexture(Skin.ScorebarBg, 0, 0, scoreboardBgWidth, scoreboardBgHeight, new Vector4(1, 1, 1, 1)
        );
        
        
        var scoreboardColourWidth = Width / 2.5f;

        var colourScale = scoreboardColourWidth / Skin.ScorebarColour.Width;
        var scoreboardColourHeight = Skin.ScorebarColour.Height * colourScale;

        _renderer.DrawTexture(Skin.ScorebarColour, 0, 0, scoreboardColourWidth, scoreboardColourHeight, new Vector4(1, 1, 1, 1)
        );
        

        //_renderer.DrawText(_msdfFont, $"{_player.Cursor.X.ToString("0000")}x{_player.Cursor.Y.ToString("0000")}", new Vector2(10, 600), 1, new Vector4(1, 1, 1, 1));

        var yellow = new Vector4(242 / 255f, 191 / 255f, 36 / 255f, 1);

        // volume control
        _renderer.DrawRectangle(0, (float)Height / 2 - 150, 40, 300, new Vector4(0.1f, 0.1f, 0.1f, 1));

        var length = Util.MapRange(_musicVolume, 0, 1, 0, 300);
        _renderer.DrawRectangle(10, (float)Height / 2 - 150, 20, length, yellow);


        // timeline
        var songPointer = Util.MapRange((float)_songCursor, 0, (float)_songLength, 0, Width);
        _renderer.DrawRectangle(0, Height - 20, songPointer, 20, yellow);
        var cursorSize = new Vector2(30, 60);
        _renderer.DrawRectangle(songPointer - cursorSize.X / 2, Height - cursorSize.Y, cursorSize.X, cursorSize.Y,
            new Vector4(1, 1, 1, 1));

        if (_player.Autoplay)
        {
            _renderer.DrawTexture(Skin.ModAutoplay, Width - 200, 50, 150, 150, new Vector4(1, 1, 1, 1));
        }

        _inputOverlayView.Draw(delta);

        // Game cursor
        // Update trail lifetimes first so the draw loop can use a stable count (prevents 1-frame alpha spikes).
        var trailCount = trails.Count;
        for (var t = 0; t < trailCount; t++)
        {
            var trail = trails.Dequeue();
            trail.Life -= (float)delta;
            if (trail.Life > 0)
                trails.Enqueue(trail);
        }

        var trailSnapshot = trails.ToArray();

        var size = _baseCircleSize / 2f;


        // var _posX = playfieldTopLeft.X + _player.Cursor.X * scale;
        // var _posY = playfieldTopLeft.Y + _player.Cursor.Y * scale;
        // _posX = _player.Cursor.X;
        // _posY = _player.Cursor.Y;
        // if (!_player.Autoplay)
        // {
        //     _posX = Input.MouseX;
        //     _posY = Input.MouseY;
        // }

        lastCursorPosition = new(_player.Cursor.X, _player.Cursor.Y);

        if (trailSnapshot.Length > 0)
        {
            var i = trailSnapshot.Length;
            for (var idx = trailSnapshot.Length - 1; idx >= 0; idx--)
            {
                var trail = trailSnapshot[idx];
                size -= 1;

                var alpha = (float)i / trailSnapshot.Length;
                _renderer.DrawTexture(Skin.CursorTrail,
                    trail.Position.X - size / 2,
                    trail.Position.Y - size / 2,
                    size, size, new Vector4(1, 1, 1, 1) * alpha);
                i--;
            }
        }


        size = _baseCircleSize / 2f; // _cursorTexture.Width * 1.5f;
        _renderer.DrawTexture(Skin.Cursor,
            _player.Cursor.X - size / 2,
            _player.Cursor.Y - size / 2,
            size, size, new Vector4(1, 1, 1, 1));


        _renderer.DrawText(_msdfFont,
            $"Cursor: {_songCursor:F0}ms | TrackPos: {_songTrack?.Position:F0}ms\nSongLength: {_songLength:F0}ms\nSampleTracks: {AudioManager.Instance.SampleTracks.Count}",
            new Vector2(10, 200), 1, new Vector4(1, 1, 0, 1));
        _renderer.FlushText(_msdfFont);

        _songSelectorView.Draw(delta);
        _settingsView.Draw(delta);
        _renderer.SetScissor(0, 0, (int)Width, (int)Height);
    }

    public void SetBeatmap(Beatmap beatmap)
    {
        _beatmap = beatmap;

        _songTrack?.Dispose();
        _songAudio?.Dispose();

        _songTrack = AudioManager.Instance.CreateTrack();
        _songAudio = AudioManager.Instance.LoadAudio(Path.Combine(_beatmap.Folder, _beatmap.AudioFilename));
        _songTrack.Volume = 0.10f;
        AudioManager.Instance.SampleVolume = 0.10f;
        _songTrack.Audio = _songAudio;
        _songLength = _songAudio.Length; // Todo: maybe move Length back to Audio
        _songCursor = 0;
        _songTrack.Speed = 1.00f;

        _startingTimer = WAITINGTIME + _beatmap.AudioLeadIn;
        _musicStarted = false;

        _beatmap.CalculatePrepass(_renderer.Window);
        _player = new Player(_beatmap);
        SDL_ShowCursor();

        _inputOverlayView.SetPlayer(_player);
        _inputOverlayView.Reset();
        ResetObjectsAfter(0);

        _sortedObjects = _beatmap.HitObjects
            .OrderByDescending(h => Math.Abs(h.Time - _songCursor));

        _backgroundTexture?.Dispose();
        _backgroundTexture = new Texture(Path.Combine(_beatmap.Folder, _beatmap.BackgroundFile));

        Logger.Instance.Info($"Beatmap set to {beatmap}");
    }

    public void Dispose()
    {
        _backgroundTexture?.Dispose();
        Skin.Dispose();
    }

    public class TrailInfo
    {
        public float Life = 50;
        public Vector2 Position;
    }

    private List<HitResultParticle> _hitResultParticles = new();

    private void AddResultParticle(Vector2 position, HitResult result, double begin, double appear, double duration)
    {
        _hitResultParticles.Add(new()
        {
            Position = position,
            Result = result,
            Begin = begin,
            Appear = appear,
            Duration = duration,
        });
    }

    private int AcquireSliderFramebuffer()
    {
        for (int i = 0; i < _sliderFramebuffers.Length; i++)
        {
            if (!_sliderFramebuffers[i].InUse)
            {
                var fb = _sliderFramebuffers[i];
                fb.InUse = true;
                _sliderFramebuffers[i] = fb;
                return i;
            }
        }

        int newIndex = _sliderFramebuffers.Length;
        // fallback: expand pool if needed
        /*
        _sliderFramebuffers.Add(new SliderFramebuffer
        {
            Framebuffer = new Framebuffer(_renderer, (int)Width, (int)Height),
            InUse = true
        });*/

        return newIndex;
    }

    private void ReleaseSliderFramebuffer(int index)
    {
        var fb = _sliderFramebuffers[index];
        fb.InUse = false;
        fb.Time = 0;
        fb.Duration = 0;
        _sliderFramebuffers[index] = fb;
    }

    public class HitResultParticle
    {
        public Vector2 Position;
        public HitResult Result;
        public double Begin; // starting offset in ms
        public double Appear; // time it takes to fully be opaque
        public double Duration; // the time it takes to die
    }
}