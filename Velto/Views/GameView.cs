using System.Drawing;
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
    private string _skinName = "rafis";

    private double _songCursor;
    private double _lastSongCursor;
    private double _songLength;
    private IOrderedEnumerable<HitObject> _sortedObjects;

    private Vector2 lastCursorPosition = Vector2.Zero;
    private Shader shader;

    private readonly Queue<TrailInfo> trails = new();

    private Player _player;
    private InputOverlayView _inputOverlayView;
    public Skin Skin;
    public bool Hidden;

    private Track? _songTrack;
    private AudioChannel? _songAudio;
    private double _audioLock = 0.0f;

    private SliderFramebuffer[] _sliderFramebuffers = new SliderFramebuffer[16];
    private float _prevWidth, _prevHeight;

    private bool _debugEnabled = true;
    private bool _doubleTimeEnabled = false;

    // https://osu.ppy.sh/wiki/en/Gameplay/Score/ScoreV1/osu%21
    private float _health = 1.0f;
    private int _comboCount = 0;
    private float _totalScore = 0;
    private float _difficultyMultiplier = 1.0f;
    private float _modMultiplier = 1.0f;
    private const double HIT_INDICATOR_MAX_LIFE = 2000f;
    private List<HitIndicator> _hitIndicators = new();

    public float MouseX;
    public float MouseY;

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
        
        //_inputOverlayView = new(_renderer, this, _msdfFont);

        for (int i = 0; i < 16; i++)
        {
            _sliderFramebuffers[i] = new()
            {
                Framebuffer = new Framebuffer(_renderer, 1280, 720),
                Duration = 0,
                Time = 0,
            };
        }

        Hidden = true;
        // SetBeatmap(new Beatmap(Resources.GetPath("Resources/Songs/Wakeshima Kanon/ASCA - Nisemono no Koi ni Sayounara with Wakeshima Kanon (timemon) [Kyou's Extra].osu")));
        // _player.SetReplay(Replay.ParseReplay(Resources.GetPath("Resources/Replays/kanon.osr")));
        SetBeatmap(new Beatmap(Resources.GetPath("Resources/Songs/983942 Oomori Seiko - JUSTadICE (TV Size)/Oomori Seiko - JUSTadICE (TV Size) (fieryrage) [Extreme].osu")));
        _player.SetReplay(Replay.ParseReplay(Resources.GetPath("Resources/Replays/fiery.osr")));
        _doubleTimeEnabled = true; _songTrack.Speed = 1.5f;
        _player.SetState(PlayerState.Replay);
    }

    float playfieldWidth, playfieldHeight;
    private Vector2 playfieldTopLeft;

    public void ToggleMenu()
    {
        _isPaused = !_isPaused;
        if (_isPaused)
        {
            _songTrack?.Pause();
        }
        else
        {
            _songTrack?.Resume();
        }
    }
    

    public override void Update(double delta)
    {
        var previousSongCursor = _lastSongCursor;
        _lastSongCursor = _songCursor;
        
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
        if (Input.IsMouseDown(SDLButton.SDL_BUTTON_LEFT) && hitbox.Contains(MouseX, MouseY) &&
            _debugEnabled)
        {
            double targetMs = Util.MapRange(MouseX, 0, Width, 0, (float)_songLength);

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
        
        


        if (Input.IsKeyDown(SDL_Scancode.SDL_SCANCODE_LSHIFT))
        {
            if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_RIGHT))
            {
                _songTrack?.Speed += 0.10f;
            }

            if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_LEFT))
            {
                _songTrack?.Speed -= 0.10f;
            }
        }
        else
        {
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
        }

        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_F1))
        {
            _debugEnabled = !_debugEnabled;
        }
        
        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_ESCAPE))
        {
            ToggleMenu();
        }

        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_GRAVE))
        {
            _songTrack?.Position = 0;
            _songTrack?.Play();
            _songCursor = 0;
            _comboCount = 0;
            _totalScore = 0;
            //_inputOverlayView.Reset();
            ResetObjectsAfter(0);
        }

        foreach (var hitIndicator in _hitIndicators.ToList())
        {
            hitIndicator.Life -= delta;
            if (hitIndicator.Life <= 0)
            {
                _hitIndicators.Remove(hitIndicator);
            }
        }

        
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
            /*_player.Autoplay = !_player.Autoplay;
            if (_player.Autoplay) SDL_ShowCursor();
            else SDL_HideCursor();*/
        }

        if (Input.IsKeyDown(SDL_Scancode.SDL_SCANCODE_LCTRL))
        {
            if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_D))
            {
                _doubleTimeEnabled = !_doubleTimeEnabled;
                if (_doubleTimeEnabled) _songTrack?.Speed = 1.5f;
                else _songTrack?.Speed = 1.0f;
            }

            if (Math.Abs(Input.WheelY) > 0.01f)
            {
                _musicVolume += Input.WheelY * 0.01f;
                _musicVolume = Math.Clamp(_musicVolume, 0, 1);
                //Bass.ChannelSetAttribute(_musicChannel, ChannelAttribute.Volume, _musicVolume);
                _songTrack?.Volume = _musicVolume;
            }
            
            if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_S))
            {
                Skin.Dispose();
                if (_skinName == "rafis") _skinName = "default";
                else _skinName = "rafis";
                Skin = new Skin(Resources.GetPath($"Resources/Textures/{_skinName}"));
            }
        }
        
        // notes
        // you can click another object after hitting a slider head
        // TODO:
        // you can click for too early for a miss

        // Update player just before doing judgement
        _player.Update(delta, _songCursor, previousSongCursor, playfieldTopLeft, scale);
        //_inputOverlayView.Update(delta);
        // Handle objects bkz: https://osu.ppy.sh/wiki/en/Gameplay/Judgement/osu%21 
        foreach (var hitObject in _beatmap.HitObjects)
        {
            if (hitObject.Time > _songCursor + _beatmap.Preempt) continue;
            
            if (hitObject is HitCircle circle)
            {
                var playerCursor = _player.Cursor;
                if (hitObject.HitResult == HitResult.None)
                {
                    if (_songCursor - 150 >= circle.Time)
                    {
                        _comboCount = 0;
                        hitObject.HitResult = HitResult.Miss;
                        hitObject.Failed = true;
                        AddResultParticle(hitObject.Position, hitObject.HitResult, _songCursor);
                        AudioManager.Instance.PlaySample(Skin.ComboBreak);
                    }
        
                    
                    float radiusScreen = osuRadius * scale;
                    var circlePosition = playfieldTopLeft + circle.Position * scale;
                    if (Vector2.Distance(circlePosition, playerCursor) <= radiusScreen)
                        if (_player.ActionPrimaryPressed || _player.ActionSecondaryPressed)
                        {
                            // bkz: https://osu.ppy.sh/wiki/en/Gameplay/Judgement/osu%21
                            //circle.Color = Vector4.Zero;
                            _hitIndicators.Add(new()
                            {
                                Life = HIT_INDICATOR_MAX_LIFE,
                                Offset = hitObject.Time - _songCursor,
                            });
                            hitObject.HitTime = _songCursor;
                            var difference = Math.Abs(_songCursor - hitObject.Time);
                            if (difference <= 80 - 6 * _beatmap.OverallDifficulty) // 300
                            {
                                _comboCount++;
                                var score = 300 * (1 + (Math.Max(_comboCount - 1, 0) * _difficultyMultiplier *
                                    _modMultiplier / 25));
                                _totalScore += score;
                                hitObject.HitResult = HitResult.Good;
                            }
                            else if (difference <= 140 - 8 * _beatmap.OverallDifficulty) // 100
                            {
                                _comboCount++;
                                var score = 100 * (1 + (Math.Max(_comboCount - 1, 0) * _difficultyMultiplier *
                                    _modMultiplier / 25));
                                _totalScore += score;
                                hitObject.HitResult = HitResult.Ok;
                            }
                            else if (difference <= 200 - 10 * _beatmap.OverallDifficulty) // 50
                            {
                                _comboCount++;
                                var score = 50 * (1 + (Math.Max(_comboCount - 1, 0) * _difficultyMultiplier *
                                    _modMultiplier / 25));
                                _totalScore += score;
                                hitObject.HitResult = HitResult.Meh;
                            }
                            else // miss
                            {
                                _comboCount = 0;
                                hitObject.HitResult = HitResult.Miss;
                                AudioManager.Instance.PlaySample(Skin.ComboBreak);
                            }

                            circle.HitTime = _songCursor;
                            AddResultParticle(hitObject.Position, hitObject.HitResult, _songCursor);
                            AudioManager.Instance.PlaySample(Skin.Normal.HitNormal);
                        }

                    // Noteblock
                    if (_player.ActionPrimaryPressed || _player.ActionSecondaryPressed)
                    {
                        break;
                    }
                }
            }
            // https://github.com/ppy/osu/wiki/Anatomy-of-a-slider
            else if (hitObject is Slider slider)
            {
                if (_songCursor >= slider.Time + slider.Duration && !slider.JudgementDone)
                {
                    if (slider.LastHeld <= slider.Time + slider.Duration && slider.LastHeld >= slider.Time + slider.Duration - Slider.FORGIVING_TIME)
                    {
                        slider.WasFollowedAtEnd = slider.IsCurrentlyBeingFollowed;
                    }

                    var sliderCompletion = 0;
                    
                    // if (slider.WasFollowedAtEnd)
                    // {
                    //     _comboCount++;
                    //     var score = 300 * (1 + (Math.Max(_comboCount - 1, 0) * _difficultyMultiplier * _modMultiplier / 25));
                    //     _totalScore += score;
                    //     AddResultParticle(slider.GetPositionAt(slider.Time + slider.Duration), HitResult.Good, _songCursor);
                    //     AudioManager.Instance.PlaySample(Skin.Normal.HitNormal);
                    // }
                    // else
                    // {
                    //     _comboCount = 0;
                    //     AddResultParticle(slider.GetPositionAt(slider.Time + slider.Duration), HitResult.Miss, _songCursor);
                    //     AudioManager.Instance.PlaySample(Skin.ComboBreak);
                    // }

                    slider.JudgementDone = true;
                    //Logger.Instance.Info($"Slider held for {slider.TotalFollowTime}/{slider.Duration}");
                }
                
                if (_songCursor > slider.Time + slider.Duration)
                    continue; // already judged
                
                var playerCursor = _player.Cursor;
                
                if (_songCursor - 150 >= slider.Time && slider.HitResult == HitResult.None)
                {
                    _comboCount = 0;
                    AudioManager.Instance.PlaySample(Skin.ComboBreak);
                    hitObject.HitResult = HitResult.Miss;
                    AddResultParticle(hitObject.Position, hitObject.HitResult, _songCursor);
                }
                
                float radiusHitCircle = osuRadius * scale;
                var circlePosition = playfieldTopLeft + slider.Position * scale;
                if (Vector2.Distance(circlePosition, playerCursor) <= radiusHitCircle)
                {
                    if ((_player.ActionPrimaryPressed || _player.ActionSecondaryPressed) &&
                        slider.HitResult == HitResult.None)
                    {
                        _hitIndicators.Add(new()
                        {
                            Life = HIT_INDICATOR_MAX_LIFE,
                            Offset = _songCursor - hitObject.Time,
                        });
                        slider.HitTime = _songCursor;
                        var difference = Math.Abs(_songCursor - slider.Time);
                        if (difference <= 80 - 6 * _beatmap.OverallDifficulty) // 300
                        {
                            _comboCount++;
                            var score = 300 * (1 + (Math.Max(_comboCount - 1, 0) * _difficultyMultiplier *
                                _modMultiplier / 25));
                            _totalScore += score;
                            hitObject.HitResult = HitResult.Good;
                        }
                        else if (difference <= 140 - 8 * _beatmap.OverallDifficulty) // 100
                        {
                            _comboCount++;
                            var score = 100 * (1 + (Math.Max(_comboCount - 1, 0) * _difficultyMultiplier *
                                _modMultiplier / 25));
                            _totalScore += score;
                            hitObject.HitResult = HitResult.Ok;
                        }
                        else if (difference <= 200 - 10 * _beatmap.OverallDifficulty) // 50
                        {
                            _comboCount++;
                            var score = 50 * (1 + (Math.Max(_comboCount - 1, 0) * _difficultyMultiplier *
                                _modMultiplier / 25));
                            _totalScore += score;
                            hitObject.HitResult = HitResult.Meh;
                        }
                        else // miss
                        {
                            hitObject.HitResult = HitResult.Miss;
                            _comboCount = 0;
                            AudioManager.Instance.PlaySample(Skin.ComboBreak);
                        }

                        slider.HitTime = _songCursor;
                        AddResultParticle(hitObject.Position, hitObject.HitResult, _songCursor);
                        AudioManager.Instance.PlaySample(Skin.Normal.HitNormal);
                    }
                }
                
                
                
                var followCirclePos = slider.GetPositionAt(_songCursor);
                var ballPosition = playfieldTopLeft + followCirclePos * scale;
                var radiusFollowCircle = (_baseCircleSize * 2.5f) / 2f;

                bool sliderActive = _songCursor >= slider.Time && _songCursor <= slider.Time + slider.Duration;
                bool isInFollowRange = sliderActive && 
                                       Vector2.Distance(ballPosition, playerCursor) <= radiusFollowCircle;
                bool isHolding = _player.ActionPrimaryDown || _player.ActionSecondaryDown;

                bool isFollowingNow = sliderActive && isInFollowRange && isHolding;

                if (sliderActive)
                {
                    slider.IsCurrentlyBeingFollowed = isFollowingNow;

                    if (isFollowingNow)
                    {
                        // Accumulate follow time
                        double deltaFollow = _songCursor - Math.Max(slider.LastFollowUpdate, slider.Time);
                        slider.TotalFollowTime += deltaFollow;

                        // Continuous follow tracking
                        if (slider.WasFollowingPreviousFrame)
                        {
                            slider.CurrentContinuousFollow += deltaFollow;
                        }
                        else
                        {
                            // Just started following again
                            slider.CurrentContinuousFollow = deltaFollow;
                        }

                        if (slider.CurrentContinuousFollow > slider.LongestContinuousFollow)
                            slider.LongestContinuousFollow = slider.CurrentContinuousFollow;

                        slider.LastHeld = _songCursor;
                        slider.LastFollowUpdate = _songCursor;
                    }

                    slider.WasFollowingPreviousFrame = isFollowingNow;
                }
                
                // Optional: play tick sounds while following
                if (isInFollowRange && isHolding)
                {
                    // You can add logic here for slider ticks
                }

                if (_player.ActionPrimaryPressed || _player.ActionSecondaryPressed)
                {
                    break;
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


        _hitResultParticles.RemoveAll(p => _songCursor - p.StartTime > p.MaxLife);

        _prevWidth = Width;
        _prevHeight = Height;
    }
    
    public override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        
        MouseX = e.X;
        MouseY = e.Y;
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

                if (obj is Slider s)
                {
                    s.WasFollowedAtEnd = false;
                    s.IsCurrentlyBeingFollowed = false;
                    s.TotalFollowTime = 0.0;
                    s.LastFollowUpdate = 0.0;
                    s.CurrentContinuousFollow = 0.0;
                    s.LongestContinuousFollow = 0.0;
                    s.WasFollowingPreviousFrame = false;
                }
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
        _renderer.DrawRectangle(0, 0, Width, Height, new Vector4(0, 0, 0, 0.80f));


        // draw playfield
        /*_renderer.DrawRectangle(
            playfieldTopLeft.X,
            playfieldTopLeft.Y,
            playfieldWidth,
            playfieldHeight,
            new Vector4(0, 0, 0, 0.3f)
        );*/


        var scale = playfieldWidth / PLAYFIELD_W;
        
        var prevHitObject = (HitObject)null;
        var nextHitObject = (HitObject)null;

        foreach (var obj in _beatmap.HitObjects)
        {
            var currTime = _songCursor + 200;
            
            if (obj.Time <= currTime)
            {
                prevHitObject = obj; // keeps updating until we pass currTime
            }
            else
            {
                nextHitObject = obj; // first one after currTime
                break;  
            }
        }

        double prevHitObjectTime = 0;
        double nextHitObjectTime = 0;
        Vector2 prevHitObjectPos = new();
        Vector2 nextHitObjectPos = new();
        if (prevHitObject != null)
        {
            prevHitObjectPos = prevHitObject.Position;
            prevHitObjectTime = prevHitObject.Time;
        }
        if (nextHitObject != null)
        {
            nextHitObjectPos = nextHitObject.Position;
            nextHitObjectTime = nextHitObject.Time;
        }

        nextHitObjectPos = playfieldTopLeft + nextHitObjectPos * scale;
        prevHitObjectPos = playfieldTopLeft + prevHitObjectPos * scale;

        var diff = nextHitObjectPos - prevHitObjectPos;
        var degree = Math.Atan2(diff.Y, diff.X);
        var distance = 100;
        
        if (!Skin.HasAnimatedFollowPoints)
        {
            var direction = new Vector2(
                (float)Math.Cos(degree),
                (float)Math.Sin(degree)
            );

            int count = (int)(diff.Length / distance);

            for (int i = 0; i < count; i++)
            {
                var pos = prevHitObjectPos + direction * (i * distance);

                _renderer.DrawTexture(Skin.FollowPoint, pos.X, pos.Y, 100f, 100f, new Vector4(1, 1, 1, 1), (float)degree * MathHelper.RadToDeg + 135);
            }
        }
        
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

                drawSize = (float)(objectCircleSize + objectCircleSize * (1 - fadeout) * 0.3);
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
                    if (!Hidden)
                    {
                        _renderer.DrawTexture(Skin.ApproachCircle,
                            posX - approachCircleSize / 2,
                            posY - approachCircleSize / 2,
                            approachCircleSize,
                            approachCircleSize, hitObject.Color with
                            {
                                W = Math.Min(fadein, fadeout)
                            });
                    }
                    
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
                        var tex = Skin.DefaultNumbers[digit];
                        digitWidthTotal += tex.Width * digitScale;
                    }

                    // start centered
                    var cursorX = posX - digitWidthTotal / 2;

                    foreach (var c in num)
                    {
                        var digit = c - '0';
                        var tex = Skin.DefaultNumbers[digit];

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

                var maxSliderOpacity = 0.8f;
                float sliderOpacity;
                if (_songCursor > slider.Time - _beatmap.Preempt && _songCursor < slider.Time)
                {
                    sliderOpacity = Util.MapRange((float)_songCursor, slider.Time - _beatmap.Preempt,
                        slider.Time - _beatmap.Preempt / 2, 0, maxSliderOpacity);
                }
                else if (_songCursor >= slider.Time - _beatmap.Preempt / 2 &&
                         _songCursor < slider.Time + slider.Duration)
                {
                    sliderOpacity = maxSliderOpacity;
                }
                else
                {
                    float window = (float)(slider.Time + slider.Duration);
                    sliderOpacity = Util.MapRange((float)_songCursor, window, window + _beatmap.Posttime,
                        maxSliderOpacity,
                        0);
                }

                sliderOpacity = Math.Clamp(sliderOpacity, 0f, maxSliderOpacity);
                if (sliderOpacity > 0.9f)
                {
                    throw new Exception("Slider opactiy > maxSliderOpacity. Fix.");
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
                        prevPos = slider.GetPositionAt(_songCursor - 1);
                    }

                    if (slider.SlideRepeatCount > 1) // sliderepeatcount = actual repeat count + 1
                    {
                        for (int i = 0; i < slider.SlideRepeatCount; i++)
                        {
                            //if (_songCursor >= slider.Time + slider.Duration - slider.SpanDuration) continue;
                            if (i - 2 >= slider.SlideRepeatCount) continue;
                            
                            var repeatPosition = slider.GetPositionAt(slider.Time + i * slider.SpanDuration);
                                
                            var repeatScaledX = playfieldTopLeft.X + repeatPosition.X * scale;
                            var repeatScaledY = playfieldTopLeft.Y + repeatPosition.Y * scale;
                            var repeatDrawSize = objectCircleSize * 1f;

                            var repeatPrevPos = slider.Points[slider.Points.Count - 2];
                                
                            Vector2 repeatDirection = repeatPosition - repeatPrevPos;
                            var repeatRotation = Math.Atan2(repeatDirection.Y, repeatDirection.X) * -MathHelper.RadToDeg +
                                                 180;

                            _renderer.DrawTexture(Skin.ReverseArrow,
                                repeatScaledX - repeatDrawSize / 2,
                                repeatScaledY - repeatDrawSize / 2,
                                repeatDrawSize,
                                repeatDrawSize,
                                new Vector4(1, 1, 1, 1) with { W = sliderOpacity },
                                (float)repeatRotation
                            );
                        }
                    }

                    Vector2 direction = position - prevPos;
                    var rotation = Math.Atan2(direction.Y, direction.X) * -MathHelper.RadToDeg + 180;

                    if (Skin.HasSliderSpec)
                    {
                        _renderer.DrawCircle(
                            scaledX - _drawSize / 2,
                            scaledY - _drawSize / 2,
                            _drawSize,
                            _drawSize,
                            hitObject.Color with { W = 1 }, (float)rotation);
                    }

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
                            new Vector4(0, 0, 0, 1) { W = 1 }, (float)rotation);
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
                }

                if (!Hidden)
                {
                    _renderer.DrawTexture(Skin.ApproachCircle,
                        posX - approachCircleSize / 2,
                        posY - approachCircleSize / 2,
                        approachCircleSize,
                        approachCircleSize, hitObject.Color with { W = Math.Min(fadein, fadeout) });
                }
                

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
                    var tex = Skin.DefaultNumbers[digit];
                    digitWidthTotal += tex.Width * digitScale;
                }

                // start centered
                var cursorX = posX - digitWidthTotal / 2;

                foreach (var c in num)
                {
                    var digit = c - '0';
                    var tex = Skin.DefaultNumbers[digit];

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

        foreach (var particle in _hitResultParticles.ToList())
        {
            float age = (float)(_songCursor - particle.StartTime);
            if (age < 0 || age > particle.MaxLife)
            {
                _hitResultParticles.Remove(particle);
                continue;
            }

            float t = age / particle.MaxLife; // 0.0 → 1.0

            // --- Scale Animation ---
            float _scale;
            if (age < 120f)
            {
                // Quick shrink from big → normal
                _scale = MathHelper.Lerp(1.55f, 1.0f, age / 120f);
            }
            else
            {
                _scale = 1.0f;
            }

            // --- Fade & Movement ---
            float alpha;
            float yOffset = 0f;

            if (age < 380f)
            {
                alpha = 1f;
            }
            else
            {
                float fadeT = (age - 380f) / (particle.MaxLife - 380f);
                alpha = MathHelper.Lerp(1f, 0f, fadeT);
                //yOffset = fadeT * -45f; // move up ~45 pixels
            }

            var texture = particle.Result switch
            {
                HitResult.Good => Skin.Hit300,
                HitResult.Ok => Skin.Hit100,
                HitResult.Meh => Skin.Hit50,
                _ => Skin.Hit0, // miss
            };

            float baseSize = _baseCircleSize * 1.1f; // slightly bigger than circle
            float w = baseSize * _scale;
            float h = texture.Height * (w / texture.Width);

            Vector2 drawPos = playfieldTopLeft + particle.Position * scale;
            drawPos.Y += yOffset;

            _renderer.DrawCenteredTexture(texture, drawPos, w, h,
                new Vector4(1, 1, 1, alpha));
        }


        // bkz: https://osu.ppy.sh/community/forums/topics/1857309?n=1
        _health = Math.Abs((float)Math.Cos(_songCursor / 1000));

        var scoreboardBgWidth = Width / 2.5f;

        var bgScale = scoreboardBgWidth / Skin.ScorebarBg.Width;
        var scoreboardBgHeight = Skin.ScorebarBg.Height * bgScale;

        _renderer.DrawTexture(Skin.ScorebarBg, 0, 0, scoreboardBgWidth, scoreboardBgHeight, new Vector4(1, 1, 1, 1));


        var scoreboardColourWidth = Width / 2.5f;

        var colourScale = scoreboardColourWidth / Skin.ScorebarColour.Width;
        var scoreboardColourHeight = Skin.ScorebarColour.Height * colourScale;

        _renderer.SetScissor(20, 22, (int)(20 + scoreboardColourWidth * _health), (int)scoreboardColourHeight);
        _renderer.DrawTexture(Skin.ScorebarColour, 20, 22, scoreboardColourWidth / 1f, scoreboardColourHeight / 1f,
            new Vector4(1, 1, 1, 1));
        _renderer.SetScissor(0, 0, (int)Width, (int)Height);


        // Draw combo count
        var comboNum = _comboCount.ToString();

        var comboDigitScale = Height / 15 / Skin.ScoreNumbers[0].Height;
        float comboTotalWidth = 0;

        // first pass: compute total width
        foreach (var c in comboNum)
        {
            var digit = c - '0';
            var tex = Skin.ScoreNumbers[digit];
            comboTotalWidth += tex.Width * comboDigitScale;
        }

        float comboCursorX = 30;

        foreach (var c in comboNum)
        {
            var digit = c - '0';
            var tex = Skin.ScoreNumbers[digit];

            var w = tex.Width * comboDigitScale;
            var h = tex.Height * comboDigitScale;

            _renderer.DrawTexture(
                tex,
                comboCursorX,
                Height - h - 30,
                w,
                h,
                new Vector4(1, 1, 1, 1)
            );

            comboCursorX += w;
        }

        // TODO: Fix the way combo x's position/scale is calculated. its def wrong i think
        var comboxXScale = Height / 20 / Skin.ScoreX.Height;

        _renderer.DrawTexture(
            Skin.ScoreX,
            comboCursorX,
            Height - Skin.ScoreX.Height * comboxXScale - 30,
            Skin.ScoreX.Width * comboxXScale,
            Skin.ScoreX.Height * comboxXScale,
            new Vector4(1, 1, 1, 1)
        );


        // Draw score
        var scoreNum = ((int)_totalScore).ToString();

        var scoreDigitScale = Height / 15 / Skin.ScoreNumbers[0].Height; //400 / (Skin.NumbersHd ? 256f : 128f);
        float scoreTotalWidth = 0;

        // first pass: compute total width
        foreach (var c in scoreNum)
        {
            var digit = c - '0';
            var tex = Skin.ScoreNumbers[digit];
            scoreTotalWidth += tex.Width * scoreDigitScale;
        }

        float scoreCursorX = Width - scoreTotalWidth - 30;

        foreach (var c in scoreNum)
        {
            var digit = c - '0';
            var tex = Skin.ScoreNumbers[digit];

            var w = tex.Width * scoreDigitScale;
            var h = tex.Height * scoreDigitScale;

            _renderer.DrawTexture(
                tex,
                scoreCursorX,
                30,
                w,
                h,
                new Vector4(1, 1, 1, 1)
            );

            scoreCursorX += w;
        }


        var yellow = new Vector4(242 / 255f, 191 / 255f, 36 / 255f, 1);

        // draw volume control
        /*_renderer.DrawRectangle(0, (float)Height / 2 - 150, 40, 300, new Vector4(0.1f, 0.1f, 0.1f, 1));

        var length = Util.MapRange(_musicVolume, 0, 1, 0, 300);
        _renderer.DrawRectangle(10, (float)Height / 2 - 150, 20, length, yellow);*/


        // timeline
        if (_debugEnabled)
        {
            var songPointer = Util.MapRange((float)_songCursor, 0, (float)_songLength, 0, Width);
            _renderer.DrawRectangle(0, Height - 20, songPointer, 20, yellow);
            var cursorSize = new Vector2(30, 60);
            _renderer.DrawRectangle(songPointer - cursorSize.X / 2, Height - cursorSize.Y, cursorSize.X, cursorSize.Y,
                new Vector4(1, 1, 1, 1));
        }

        // TODO: add actual stacking for mod icons
        if (_player.State == PlayerState.Autoplay)
        {
            _renderer.DrawTexture(Skin.ModAutoplay, Width - Width / 20 - 50, Height / 8, Width / 20, Width / 20,
                new Vector4(1, 1, 1, 1));
        }

        if (_doubleTimeEnabled)
        {
            _renderer.DrawTexture(Skin.ModNightcore, Width - Width / 20 - 50 - Width / 40, Height / 8, Width / 20,
                Width / 20, new Vector4(1, 1, 1, 1));
        }
        if (Hidden)
        {
            _renderer.DrawTexture(Skin.ModHidden, Width - Width / 20 - 50 - Width / 40 - 50 - Width / 40, Height / 8, Width / 20,
                Width / 20, new Vector4(1, 1, 1, 1));
        }

        //_inputOverlayView.Draw(delta);

        // Draw timing window
        var timingWindowWidth = Width / 7;
        var timingWindowHeight = timingWindowWidth / 25;
        var timingWindowX = Width / 2 - timingWindowWidth / 2;
        var timingWindowY = Height - timingWindowHeight - timingWindowHeight;
        _renderer.DrawRectangle(timingWindowX, timingWindowY, timingWindowWidth, timingWindowHeight,
            new Vector4(1, 1, 1, 1));

        var timingSegmentSize = timingWindowWidth / 6;
        for (int i = 0; i < 6; i++)
        {
            var hitWindowColor = new Vector4(1, 1, 1, 1);
            if (i == 0 || i == 5)
            {
                hitWindowColor = new Vector4(246 / 255f, 205 / 255f, 77 / 255f, 1.0f);
            }
            else if (i == 1 || i == 4)
            {
                hitWindowColor = new Vector4(144 / 255f, 177 / 255f, 54 / 255f, 1.0f);
            }
            else
            {
                hitWindowColor = new Vector4(128 / 255f, 201 / 255f, 249 / 255f, 1.0f);
            }

            _renderer.DrawRectangle(
                Width / 2 - timingWindowWidth / 2 + (timingSegmentSize * i),
                Height - timingWindowHeight - timingWindowHeight,
                timingSegmentSize,
                timingWindowHeight,
                hitWindowColor);
        }

        foreach (var indicator in _hitIndicators)
        {
            var range = 200 - 10 * _beatmap.OverallDifficulty;
            var cursorX = Util.MapRange((float)indicator.Offset, -range, range, timingWindowX,
                timingWindowX + timingWindowWidth);
            _renderer.DrawRectangle(cursorX, timingWindowY - timingWindowHeight, 3, timingWindowHeight * 2,
                new Vector4(1, 1, 1, (float)(indicator.Life / HIT_INDICATOR_MAX_LIFE)));
        }
        
        // watermark unranked
        if (_player.State == PlayerState.Autoplay || _player.State == PlayerState.Replay)
        {
            var unrankedWidth = Width / 10;
            var unrankedHeight = Skin.PlayUnranked.Height * (unrankedWidth / Skin.PlayUnranked.Width);
            _renderer.DrawTexture(Skin.PlayUnranked, Width/2 - unrankedWidth/2, Height/10, unrankedWidth, unrankedHeight, new Vector4(1, 1, 1, 1));
        }

        if (_isPaused)
        {
            //_renderer.DrawRectangle(0, 0, Width, Height, new Vector4(0, 0, 0, 0.85f));
        }
        
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


        // draw cursor
        size = _baseCircleSize / 2f; // _cursorTexture.Width * 1.5f;
        if (Skin.HasCursorMiddle)
        {
            _renderer.DrawTexture(Skin.CursorMiddle,
                _player.Cursor.X - size / 4,
                _player.Cursor.Y - size / 4,
                size / 2, size / 2, new Vector4(1, 1, 1, 1));
        }

        _renderer.DrawTexture(Skin.Cursor,
            _player.Cursor.X - size / 2,
            _player.Cursor.Y - size / 2,
            size, size, new Vector4(1, 1, 1, 1));
        
        
        if (_debugEnabled)
        {
            _renderer.DrawText(_msdfFont,
                $"Cursor: {_songCursor:F0}ms | TrackPos: {_songTrack?.Position:F0}ms\nSongLength: {_songLength:F0}ms\nSampleTracks: {AudioManager.Instance.SampleTracks.Count}/50",
                new Vector2(10, 200), Height/45, new Vector4(1, 1, 0, 1));
            _renderer.FlushText(_msdfFont);
        }
        
        //_renderer.DrawCenteredRect(new Vector2(MouseX, MouseY), 50, 50, new Vector4(1, 0, 1, 1));
        
        _renderer.SetScissor(0, 0, (int)Width, (int)Height);
    }

    public override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        Width = e.Width;
        Height = e.Height;
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
        _player.View = this;
        SDL_ShowCursor();

        //_inputOverlayView.SetPlayer(_player);
        //_inputOverlayView.Reset();
        ResetObjectsAfter(0);

        _sortedObjects = _beatmap.HitObjects
            .OrderByDescending(h => Math.Abs(h.Time - _songCursor));

        _backgroundTexture?.Dispose();
        _backgroundTexture = new Texture(Path.Combine(_beatmap.Folder, _beatmap.BackgroundFile));

        //Difficulty multiplier = Round((HP Drain + Circle Size + Overall Difficulty + Clamp(Hit object count / Drain time in seconds * 8, 0, 16)) / 38 * 5)

        var objs = _beatmap.HitObjects.OrderBy(t => t.Time);
        var drainTime = Math.Abs(objs.First().Time - objs.Last().Time) / 1000f;
        _difficultyMultiplier = (float)Math.Round((_beatmap.HPDrainRate + _beatmap.CircleSize +
                                                   _beatmap.OverallDifficulty
                                                   + Math.Clamp(_beatmap.HitObjects.Count / drainTime * 8, 0, 16)) /
                                                  38 *
                                                  5);
        _comboCount = 0;
        _totalScore = 0;

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

    private void AddResultParticle(Vector2 position, HitResult result, double hitTime)
    {
        _hitResultParticles.Add(new HitResultParticle
        {
            Position = position,
            Result = result,
            StartTime = hitTime,
            CurrentScale = 1.55f, // Start bigger
            CurrentAlpha = 1f,
            CurrentOffset = Vector2.Zero
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
        public double StartTime; // When the hit happened
        public float MaxLife = 620f; // Total lifetime in ms (osu! ~600-650)

        // Current state (for smoother animation)
        public float CurrentScale = 1.4f;
        public float CurrentAlpha = 1f;
        public Vector2 CurrentOffset = Vector2.Zero;
    }

    public class HitIndicator
    {
        public double Offset;
        public double Life;
    }
}