using System.Drawing;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using SDL;
using Velto.Audio;
using Velto.Core;
using Velto.Core.Timing;
using Velto.Game;
using Velto.Game.osu;
using Velto.Graphics;
using static SDL.SDL3;

namespace Velto.Game.Views;

public class GameScreen : Screen, IDisposable
{
    private const int PlayfieldW = 512;
    private const int PlayfieldH = 384;
    private const double WaitingTime = 1000f;
    private const double HitIndicatorMaxLife = 2000f;
    
    private double _startingTimer;

    private Texture? _backgroundTexture;

    private float _baseCircleSize;

    private Beatmap _beatmap;
    private IOrderedEnumerable<HitObject> _sortedObjects;

    private bool _isPaused;
    
    private bool _musicStarted;
    private float _musicVolume;
    
    private string _skinName = "rafis";

    
    private double _lastCurrentTime;
    private double _songLength;
    

    private Vector2 _lastCursorPosition = Vector2.Zero;
    
    private readonly Queue<TrailInfo> trails = new();

    public Player Player;
    private bool _hidden;

    private Track? _songTrack;
    private AudioChannel? _songAudio;
    
    private readonly SliderFramebuffer[] _sliderFramebuffers = new SliderFramebuffer[16];
    private float _prevWidth, _prevHeight;

    private bool _debugEnabled = true;
    private bool _doubleTimeEnabled;

    // https://osu.ppy.sh/wiki/en/Gameplay/Score/ScoreV1/osu%21
    private float _health = 1.0f;
    private int _comboCount = 0;
    private float _totalScore = 0;
    private float _difficultyMultiplier = 1.0f;
    private float _modMultiplier = 1.0f;
    private readonly List<HitIndicator> _hitIndicators = new();

    private float _playfieldWidth, _playfieldHeight;
    private Vector2 _playfieldTopLeft;

    public struct SliderFramebuffer
    {
        public Framebuffer Framebuffer;
        public float Time;
        public float Duration;
        public bool InUse;
    }

    private OsuContext _context;
    private StopwatchClock clock;

    public GameScreen(OsuContext context) : base(context)
    {
        _context = context;
        clock = new(false);
    }

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
        _lastCurrentTime = clock.CurrentTime;
        
        if ((int)_prevWidth != (int)Width || (int)_prevHeight != (int)Height)
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
            if (clock.CurrentTime != 0)
            {
                // Something move the cursor
                _musicStarted = true;
                _songTrack?.Play();
                _songTrack?.Position = clock.CurrentTime;
            }

            _startingTimer -= delta;

            if (_startingTimer <= 0 && !_musicStarted)
            {
                _musicStarted = true;
                _songTrack?.Play();
                clock.Seek(0);
                clock.Start();
            }
        }
        
        if (_musicStarted && !_isPaused && _songTrack != null)
        {
            //clock.CurrentTime = _songTrack.Position;
            //clock.CurrentTime += delta;

            //clock.Rate = 1f;
            //_songTrack.Speed = 1f;
            //_songTrack.Position = clock.CurrentTime;
        }

        RectangleF hitbox = new(0, Height - 40, Width, 40);
        if (Input.IsMouseDown(SDLButton.SDL_BUTTON_LEFT) && hitbox.Contains(Input.MouseX, Input.MouseY) &&
            _debugEnabled)
        {
            double targetMs = Interpolation.Map(Input.MouseX, 0, Width, 0, (float)_songLength);

            if (_startingTimer >= 0 && !_musicStarted)
            {
                _musicStarted = true;
                _songTrack?.Play();
                _startingTimer = 0;
            }

            bool wasPlaying = _songTrack!.Playing;
            _songTrack.Pause();
            _songTrack.Position = targetMs;
            //clock.CurrentTime = targetMs;
            clock.Seek(targetMs);
            ResetObjectsAfter(clock.CurrentTime);

            if (wasPlaying)
                _songTrack.Resume();
        }

        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_F2))
        {
            Player.SetState(PlayerState.Player);
        }
        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_F3))
        {
            Player.SetState(PlayerState.Autoplay);
        }

        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_F4))
        {
            SetBeatmap(new Beatmap(Resources.GetPath("Resources/Songs/983942 Oomori Seiko - JUSTadICE (TV Size)/Oomori Seiko - JUSTadICE (TV Size) (fieryrage) [Extreme].osu")));
            Player.SetReplay(Replay.ParseReplay(Resources.GetPath("Resources/Replays/fiery.osr")));
            Player.SetState(PlayerState.Replay);
        }

        if (Input.IsKeyDown(SDL_Scancode.SDL_SCANCODE_LSHIFT))
        {
            if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_RIGHT))
            {
                _songTrack?.Speed += 0.10f;
                clock.Rate += 0.10f;
            }

            if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_LEFT))
            {
                _songTrack?.Speed -= 0.10f;
                clock.Rate -= 0.10f;
            }
        }
        else
        {
            if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_LEFT))
            {
                //clock.CurrentTime -= 1000;
                clock.Seek(clock.CurrentTime - 1000);
                _songTrack?.Position = clock.CurrentTime;

                ResetObjectsAfter(clock.CurrentTime);
            }

            if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_RIGHT))
            {
                //clock.CurrentTime += 1000;
                clock.Seek(clock.CurrentTime + 1000);
                _songTrack?.Position = clock.CurrentTime;

                ResetObjectsAfter(clock.CurrentTime);
            }
        }

        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_F1))
        {
            _debugEnabled = !_debugEnabled;
        }
        
        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_ESCAPE))
        {
            //ToggleMenu();
            _isPaused = true;
            Transition(new SongSelectScreen(_context), 200);
        }

        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_GRAVE))
        {
            // _songTrack?.Position = 0;
            // _songTrack?.Play();
            // clock.CurrentTime = 0;
            // _comboCount = 0;
            // _totalScore = 0;
            // ResetObjectsAfter(0);
            //ViewManager.Instance.Transition(this, this, 1000);
            var game = new GameScreen(_context);
            game.SetBeatmap(_beatmap);
            game.Player.SetState(PlayerState.Autoplay);
            _context.SystemTrack.Audio = _context.Skin.PauseRetryClick;
            _context.SystemTrack.Play();
            Transition(game, 200);
            return;
        }

        foreach (var hitIndicator in _hitIndicators.ToList())
        {
            hitIndicator.Life -= delta;
            if (hitIndicator.Life <= 0)
            {
                _hitIndicators.Remove(hitIndicator);
            }
        }
        
        // Calculate playfield
        var scale = _playfieldWidth / PlayfieldW;
        var osuRadius = 54.4f - 4.48f * _beatmap.CircleSize;
        _baseCircleSize = osuRadius * 2f * scale;
        
        var playfieldAspect = PlayfieldW / (float)PlayfieldH;
        var windowAspect = Width / (Height);
        float playfieldScale = 0.76f;
        
        if (windowAspect > playfieldAspect)
        {
            _playfieldHeight = Height * playfieldScale;
            _playfieldWidth = _playfieldHeight * playfieldAspect;
        }
        else
        {
            _playfieldWidth = Width * playfieldScale;
            _playfieldHeight = _playfieldWidth / playfieldAspect;
        }

        _playfieldTopLeft = new(
            (Width - _playfieldWidth) / 2f,
            (Height - _playfieldHeight) / 2f
        );
        
        _sortedObjects = _beatmap.HitObjects
            .OrderByDescending(h => Math.Abs(h.Time - clock.CurrentTime));
        
        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_SPACE))
        {
            if (_isPaused)
            {
                _songTrack?.Resume();
                clock.Start();
                _songTrack.Position = clock.CurrentTime;
            }
            else
            {
                clock.Stop();
                _songTrack?.Pause();
            }
            _isPaused = !_isPaused;
        }
        
        if (Input.IsKeyDown(SDL_Scancode.SDL_SCANCODE_LCTRL))
        {
            if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_D))
            {
                _doubleTimeEnabled = !_doubleTimeEnabled;
                _songTrack?.Speed = _doubleTimeEnabled ? 1.5f : 1.0f;
                clock.Rate = _doubleTimeEnabled ? 1.5f : 1.0f;
            }

            if (Math.Abs(Input.WheelY) > 0.01f)
            {
                _musicVolume += Input.WheelY * 0.01f;
                _musicVolume = Math.Clamp(_musicVolume, 0, 1);
                _songTrack?.Volume = _musicVolume;
                AudioManager.Instance.SampleVolume = _musicVolume;
            }
        }
        
        Player.Update(delta, clock.CurrentTime, _playfieldTopLeft, scale);
        // Handle objects bkz: https://osu.ppy.sh/wiki/en/Gameplay/Judgement/osu%21 
        //Judge(clock.CurrentTime);
        
        // if (_lastCursorPosition != Vector2.Zero && Vector2.Distance(Player.Cursor, _lastCursorPosition) > 8)
        //     trails.Enqueue(new TrailInfo
        //     {
        //         Position = _lastCursorPosition,
        //         Life = 50
        //     });
        float distance = Vector2.Distance(Player.Cursor, _lastCursorPosition);
        if (_lastCursorPosition != Vector2.Zero && distance > 8)
        {
            int steps = (int)(distance / 6f);
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector2 interpolated = Vector2.Lerp(_lastCursorPosition, Player.Cursor, t);
        
                trails.Enqueue(new TrailInfo
                {
                    Position = interpolated,
                    Life = 45 + (steps - i) * 2 // newer points live longer
                });
            }
        }
        
        while (trails.Count > 40)
            trails.Dequeue();


        _hitResultParticles.RemoveAll(p => clock.CurrentTime - p.StartTime > p.MaxLife);

        _prevWidth = Width;
        _prevHeight = Height;
        
        var trailCount = trails.Count;
        for (var t = 0; t < trailCount; t++)
        {
            var trail = trails.Dequeue();
            trail.Life -= (float)delta;
            if (trail.Life > 0)
                trails.Enqueue(trail);
        }
    }

    public void Judge(double time)
    {
        var scale = _playfieldWidth / PlayfieldW;
        var osuRadius = 54.4f - 4.48f * _beatmap.CircleSize;
        foreach (var hitObject in _beatmap.HitObjects)
        {
            if (hitObject.Time > time + _beatmap.Preempt) continue;
            
            if (hitObject is HitCircle circle)
            {
                var playerCursor = Player.Cursor;
                if (hitObject.HitResult == HitResult.None)
                {
                    if (time -  (200 - 10 * _beatmap.OverallDifficulty) >= circle.Time)
                    {
                        _comboCount = 0;
                        hitObject.HitResult = HitResult.Miss;
                        hitObject.Failed = true;
                        AddResultParticle(hitObject.Position, hitObject.HitResult, time);
                        AudioManager.Instance.PlaySample(_context.Skin.ComboBreak);
                    }
                    
                    float radiusScreen = osuRadius * scale;
                    var circlePosition = _playfieldTopLeft + circle.Position * scale;
                    if (Vector2.Distance(circlePosition, playerCursor) <= radiusScreen)
                        if (Player.ActionPrimaryPressed || Player.ActionSecondaryPressed)
                        {
                            // bkz: https://osu.ppy.sh/wiki/en/Gameplay/Judgement/osu%21
                            var difference = Math.Abs(time - hitObject.Time);
                            // osu! only registers a click as a hit when it lands inside the
                            // hit window. Clicks earlier than the 50 window are ignored (the
                            // note can still be hit on time); clicks past it have already been
                            // auto-missed above. So we never produce a Miss from an early click.
                            if (difference <= 200 - 10 * _beatmap.OverallDifficulty)
                            {
                                _hitIndicators.Add(new()
                                {
                                    Life = HitIndicatorMaxLife,
                                    Offset = hitObject.Time - time,
                                });
                                hitObject.HitTime = time;
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
                                else // 50
                                {
                                    _comboCount++;
                                    var score = 50 * (1 + (Math.Max(_comboCount - 1, 0) * _difficultyMultiplier *
                                        _modMultiplier / 25));
                                    _totalScore += score;
                                    hitObject.HitResult = HitResult.Meh;
                                }

                                circle.HitTime = time;
                                AddResultParticle(hitObject.Position, hitObject.HitResult, time);
                                AudioManager.Instance.PlaySample(_context.Skin.Normal.HitNormal);
                            }
                        }

                    // Noteblock
                    if (Player.ActionPrimaryPressed || Player.ActionSecondaryPressed)
                    {
                        break;
                    }
                }
            }
            // https://github.com/ppy/osu/wiki/Anatomy-of-a-slider
            else if (hitObject is Slider slider)
            {
                if (time >= slider.Time + slider.Duration && !slider.JudgementDone)
                {
                    if (slider.LastHeld <= slider.Time + slider.Duration && slider.LastHeld >= slider.Time + slider.Duration - Slider.FORGIVING_TIME)
                    {
                        slider.WasFollowedAtEnd = slider.IsCurrentlyBeingFollowed;
                    }

                    // var sliderCompletion = 0;
                    // if (slider.WasFollowedAtEnd)
                    // {
                    //     _comboCount++;
                    //     var score = 300 * (1 + (Math.Max(_comboCount - 1, 0) * _difficultyMultiplier * _modMultiplier / 25));
                    //     _totalScore += score;
                    //     AddResultParticle(slider.GetPositionAt(slider.Time + slider.Duration), HitResult.Good, _songCursor);
                    //     AudioManager.Instance.PlaySample(_context.Skin.Normal.HitNormal);
                    // }
                    // else
                    // {
                    //     _comboCount = 0;
                    //     AddResultParticle(slider.GetPositionAt(slider.Time + slider.Duration), HitResult.Miss, _songCursor);
                    //     AudioManager.Instance.PlaySample(_context.Skin.ComboBreak);
                    // }
                    slider.JudgementDone = true;
                    //Logger.Instance.Info($"Slider held for {slider.TotalFollowTime}/{slider.Duration}");
                }
                
                if (time > slider.Time + slider.Duration)
                    continue; // already judged
                
                var playerCursor = Player.Cursor;
                
                if (time - 150 >= slider.Time && slider.HitResult == HitResult.None)
                {
                    _comboCount = 0;
                    AudioManager.Instance.PlaySample(_context.Skin.ComboBreak);
                    hitObject.HitResult = HitResult.Miss;
                    AddResultParticle(hitObject.Position, hitObject.HitResult, time);
                }
                
                float radiusHitCircle = osuRadius * scale;
                var circlePosition = _playfieldTopLeft + slider.Position * scale;
                if (Vector2.Distance(circlePosition, playerCursor) <= radiusHitCircle)
                {
                    if ((Player.ActionPrimaryPressed || Player.ActionSecondaryPressed) &&
                        slider.HitResult == HitResult.None)
                    {
                        var difference = Math.Abs(time - slider.Time);
                        // Same as hit circles: an early click (before the 50 window) is ignored
                        // rather than turned into a Miss, so the slider head can still be hit.
                        if (difference <= 200 - 10 * _beatmap.OverallDifficulty)
                        {
                            _hitIndicators.Add(new()
                            {
                                Life = HitIndicatorMaxLife,
                                Offset = time - hitObject.Time,
                            });
                            slider.HitTime = time;
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
                            else // 50
                            {
                                _comboCount++;
                                var score = 50 * (1 + (Math.Max(_comboCount - 1, 0) * _difficultyMultiplier *
                                    _modMultiplier / 25));
                                _totalScore += score;
                                hitObject.HitResult = HitResult.Meh;
                            }

                            slider.HitTime = time;
                            AddResultParticle(hitObject.Position, hitObject.HitResult, time);
                            AudioManager.Instance.PlaySample(_context.Skin.Normal.HitNormal);
                        }
                    }
                }
                
                var followCirclePos = slider.GetPositionAt(time);
                var ballPosition = _playfieldTopLeft + followCirclePos * scale;
                var radiusFollowCircle = (_baseCircleSize * 2.5f) / 2f;

                bool sliderActive = time >= slider.Time && time <= slider.Time + slider.Duration;
                bool isInFollowRange = sliderActive && 
                                       Vector2.Distance(ballPosition, playerCursor) <= radiusFollowCircle;
                bool isHolding = Player.ActionPrimaryDown || Player.ActionSecondaryDown;
                bool isFollowingNow = sliderActive && isInFollowRange && isHolding;

                if (sliderActive)
                {
                    slider.IsCurrentlyBeingFollowed = isFollowingNow;

                    if (isFollowingNow)
                    {
                        // Accumulate follow time
                        double deltaFollow = time - Math.Max(slider.LastFollowUpdate, slider.Time);
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

                        slider.LastHeld = time;
                        slider.LastFollowUpdate = time;
                    }

                    slider.WasFollowingPreviousFrame = isFollowingNow;
                }
                
                // Optional: play tick sounds while following
                if (isInFollowRange && isHolding)
                {
                    // You can add logic here for slider ticks
                }

                if (Player.ActionPrimaryPressed || Player.ActionSecondaryPressed)
                {
                    break;
                }
            }
        }
    }

    public override void Draw(Renderer r)
    {
        r.Clear(new(0, 0, 0, 1));
        r.PushScissor(0, 0, (int)Width, (int)Height);
        
        var playfieldScale = _playfieldWidth / PlayfieldW;

        // Background Texture
        if (Width * ((float)_backgroundTexture!.Height / _backgroundTexture.Width) > Height)
        {
            // sağ sol bar
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

        // Background Dim
        r.DrawRectangle(0, 0, Width, Height, new Color4<Rgba>(0, 0, 0, 0.80f));
        
        // Followpoints
        
        // var diff = nextHitObjectPos - prevHitObjectPos;
        // var degree = Math.Atan2(diff.Y, diff.X);
        // var distance = 100;
        //
        // if (!_context.Skin.HasAnimatedFollowPoints)
        // {
        //     var direction = new Vector2(
        //         (float)Math.Cos(degree),
        //         (float)Math.Sin(degree)
        //     );
        //     int count = diff.Length / distance;
        //     for (int i = 0; i < count; i++)
        //     {
        //         var pos = prevHitObjectPos + direction * (i * distance);
        //         r.DrawTexture(_context.Skin.FollowPoint, pos.X, pos.Y, 100f, 100f, new Color4<Rgba>(1, 1, 1, 1), (float)degree * MathHelper.RadToDeg + 135);
        //     }
        // }
        
        foreach (var hitObject in _sortedObjects)
        {
            var posX = _playfieldTopLeft.X + hitObject.Position.X * playfieldScale;
            var posY = _playfieldTopLeft.Y + hitObject.Position.Y * playfieldScale;
            var objectCircleSize = _baseCircleSize;
            float fadein;
            float fadeout;
            float drawSize;
            if (hitObject.HitResult == HitResult.None)
            {
                fadein =
                    Math.Clamp(
                        Interpolation.Map((float)clock.CurrentTime, (float)hitObject.Time - _beatmap.Preempt,
                            (float)(hitObject.Time - _beatmap.Preempt / 3),
                            0, 1), 0, 1);
                fadeout =
                    Math.Clamp(
                        Interpolation.Map((float)clock.CurrentTime, (float)hitObject.Time, (float)(hitObject.Time + _beatmap.Posttime), 1, 0),
                        0, 1);
                drawSize = (float)(objectCircleSize + objectCircleSize * (1 - fadeout) * 0.2);
            }
            else
            {
                fadein = 1;

                fadeout =
                    Math.Clamp(
                        Interpolation.Map((float)clock.CurrentTime, (float)hitObject.HitTime,
                            (float)(hitObject.HitTime + _beatmap.Posttime), 1, 0),
                        0, 1);

                drawSize = (float)(objectCircleSize + objectCircleSize * (1 - fadeout) * 0.3);
            }
            var approachCircleSize = Math.Max(
                Interpolation.Map((float)clock.CurrentTime, (float)(hitObject.Time - _beatmap.Preempt), (float)(hitObject.Time + 0),
                    drawSize * 4, drawSize), drawSize);
            
            switch (hitObject)
            {
                // Gameplay drawing
                case HitCircle circle:
                {
                    if (circle.Time + _beatmap.Posttime > clock.CurrentTime &&
                        circle.Time - _beatmap.Preempt < clock.CurrentTime)
                    {
                        if (!_hidden)
                        {
                            r.DrawTexture(_context.Skin.ApproachCircle,
                                posX - approachCircleSize / 2,
                                posY - approachCircleSize / 2,
                                approachCircleSize,
                                approachCircleSize, circle.Color with
                                {
                                    W = Math.Min(fadein, fadeout)
                                });
                        }
                    
                        r.DrawTexture(_context.Skin.HitCircle,
                            posX - drawSize / 2,
                            posY - drawSize / 2,
                            drawSize,
                            drawSize, circle.Color with { W = Math.Min(1, 1) });

                        r.DrawTexture(_context.Skin.HitCircleOverlay,
                            posX - drawSize / 2,
                            posY - drawSize / 2,
                            drawSize,
                            drawSize, new Color4<Rgba>(1, 1, 1, 1) with { W = Math.Min(fadein, fadeout) });

                        var num = circle.ComboNumber.ToString();


                        var digitScale = drawSize / (_context.Skin.NumbersHd ? 256f : 128f);

                        float digitWidthTotal = 0;

                        // first pass: compute total width
                        foreach (var c in num)
                        {
                            var digit = c - '0';
                            var tex = _context.Skin.DefaultNumbers[digit];
                            digitWidthTotal += tex.Width * digitScale;
                        }

                        // start centered
                        var cursorX = posX - digitWidthTotal / 2;

                        foreach (var c in num)
                        {
                            var digit = c - '0';
                            var tex = _context.Skin.DefaultNumbers[digit];

                            var w = tex.Width * digitScale;
                            var h = tex.Height * digitScale;

                            r.DrawTexture(
                                tex,
                                cursorX,
                                posY - h / 2,
                                w,
                                h,
                                new Color4<Rgba>(1, 1, 1, 1) with { W = Math.Min(fadein, fadeout) }
                            );

                            cursorX += w;
                        }
                    }

                    break;
                }
                case Slider slider when !(clock.CurrentTime < slider.Time + slider.Duration + _beatmap.Posttime) ||
                                        !(clock.CurrentTime > slider.Time - _beatmap.Preempt):
                    continue;
                case Slider slider:
                {
                    slider.Sliding =
                        clock.CurrentTime >= slider.Time &&
                        clock.CurrentTime <= slider.Time + slider.Duration;

                    int fbIndex = AcquireSliderFramebuffer();
                    var fb = _sliderFramebuffers[fbIndex];
                    r.BindFramebuffer(fb.Framebuffer);
                    r.Clear(new(0, 0, 0, 0));
                    GL.BlendFunc(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
                    foreach (var point in slider.Points)
                    {
                        var scaledX = _playfieldTopLeft.X + point.X * playfieldScale;
                        var scaledY = _playfieldTopLeft.Y + point.Y * playfieldScale;

                        var segmentSize = objectCircleSize * 0.92f;
                        r.DrawCircle(
                            scaledX - segmentSize / 2,
                            scaledY - segmentSize / 2,
                            segmentSize,
                            segmentSize,
                            new Color4<Rgba>(1, 1, 1, 1));
                    }

                    foreach (var point in slider.Points)
                    {
                        var scaledX = _playfieldTopLeft.X + point.X * playfieldScale;
                        var scaledY = _playfieldTopLeft.Y + point.Y * playfieldScale;
                        var segmentSize = objectCircleSize * 0.8f;

                        r.DrawCircle(
                            scaledX - segmentSize / 2,
                            scaledY - segmentSize / 2,
                            segmentSize,
                            segmentSize,
                            // hitObject.Color with { W = Math.Min(sliderFadein, sliderFadeout) });
                            new Color4<Rgba>(0.1f, 0.1f, 0.1f, 1));
                    }

                    GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                    r.UnbindFramebuffer(fb.Framebuffer);
                    ReleaseSliderFramebuffer(fbIndex);
                
                    var maxSliderOpacity = 0.8f;
                    float sliderOpacity;
                    if (clock.CurrentTime > slider.Time - _beatmap.Preempt && clock.CurrentTime < slider.Time)
                    {
                        sliderOpacity = Interpolation.Map((float)clock.CurrentTime, (float)(slider.Time - _beatmap.Preempt),
                            (float)(slider.Time - _beatmap.Preempt / 2), 0, maxSliderOpacity);
                    }
                    else if (clock.CurrentTime >= slider.Time - _beatmap.Preempt / 2 &&
                             clock.CurrentTime < slider.Time + slider.Duration)
                    {
                        sliderOpacity = maxSliderOpacity;
                    }
                    else
                    {
                        float window = (float)(slider.Time + slider.Duration);
                        sliderOpacity = Interpolation.Map((float)clock.CurrentTime, window, window + _beatmap.Posttime,
                            maxSliderOpacity,
                            0);
                    }

                    sliderOpacity = Math.Clamp(sliderOpacity, 0f, maxSliderOpacity);
                    if (sliderOpacity > 0.9f)
                    {
                        throw new Exception("Slider opactiy > maxSliderOpacity. Fix.");
                    }

                    r.DrawTexture(fb.Framebuffer.Texture, 0, 0, Width, Height,
                        new Color4<Rgba>(1, 1, 1, 1) { W = sliderOpacity });

                    if (slider.Sliding)
                    {
                        var position = slider.GetPositionAt(clock.CurrentTime);

                        var scaledX = _playfieldTopLeft.X + position.X * playfieldScale;
                        var scaledY = _playfieldTopLeft.Y + position.Y * playfieldScale;
                        var segmentSize = objectCircleSize * 1f;

                        Vector2 prevPos;
                        if (clock.CurrentTime == slider.Time)
                        {
                            prevPos = slider.Position;
                        }
                        else
                        {
                            prevPos = slider.GetPositionAt(clock.CurrentTime - 1);
                        }

                        if (slider.SlideRepeatCount > 1) // sliderepeatcount = actual repeat count + 1
                        {
                            for (int i = 0; i < slider.SlideRepeatCount; i++)
                            {
                                //if (_songCursor >= slider.Time + slider.Duration - slider.SpanDuration) continue;
                                if (i - 2 >= slider.SlideRepeatCount) continue;
                            
                                var repeatPosition = slider.GetPositionAt(slider.Time + i * slider.SpanDuration);
                                
                                var repeatScaledX = _playfieldTopLeft.X + repeatPosition.X * playfieldScale;
                                var repeatScaledY = _playfieldTopLeft.Y + repeatPosition.Y * playfieldScale;
                                var repeatDrawSize = objectCircleSize * 1f;

                                var repeatPrevPos = slider.Points[slider.Points.Count - 2];
                                
                                Vector2 repeatDirection = repeatPosition - repeatPrevPos;
                                var repeatRotation = Math.Atan2(repeatDirection.Y, repeatDirection.X) * -MathHelper.RadToDeg +
                                                     180;

                                r.DrawTexture(_context.Skin.ReverseArrow,
                                    repeatScaledX - repeatDrawSize / 2,
                                    repeatScaledY - repeatDrawSize / 2,
                                    repeatDrawSize,
                                    repeatDrawSize,
                                    new Color4<Rgba>(1, 1, 1, 1) with { W = sliderOpacity },
                                    (float)repeatRotation
                                );
                            }
                        }

                        Vector2 direction = position - prevPos;
                        var rotation = Math.Atan2(direction.Y, direction.X) * -MathHelper.RadToDeg + 180;

                        if (_context.Skin.HasSliderSpec)
                        {
                            r.DrawCircle(
                                scaledX - segmentSize / 2,
                                scaledY - segmentSize / 2,
                                segmentSize,
                                segmentSize,
                                hitObject.Color with { W = 1 }, (float)rotation);
                        }

                        if (_context.Skin.SliderBallAnimated)
                        {
                            var ballIndex = Math.Clamp(
                                (int)Interpolation.Map(
                                    (float)clock.CurrentTime,
                                    (float)slider.Time,
                                    (float)(slider.Time + slider.Duration),
                                    0,
                                    _context.Skin.SliderBalls.Count - 1),
                                0,
                                _context.Skin.SliderBalls.Count - 1);
                            r.DrawTexture(_context.Skin.SliderBalls[ballIndex],
                                scaledX - segmentSize / 2,
                                scaledY - segmentSize / 2,
                                segmentSize,
                                segmentSize,
                                new Color4<Rgba>(0, 0, 0, 1) { W = 1 }, (float)rotation);
                        }
                        else
                        {
                            r.DrawTexture(_context.Skin.SliderBalls.First(),
                                scaledX - segmentSize / 2,
                                scaledY - segmentSize / 2,
                                segmentSize,
                                segmentSize,
                                hitObject.Color with { W = 1 });
                        }

                        segmentSize = objectCircleSize * 2.5f;
                        r.DrawTexture(_context.Skin.SliderFollowCircle,
                            scaledX - segmentSize / 2,
                            scaledY - segmentSize / 2,
                            segmentSize,
                            segmentSize,
                            new Color4<Rgba>(1, 1, 1, 1) with { W = 1 });
                    }

                    if (!_hidden)
                    {
                        r.DrawTexture(_context.Skin.ApproachCircle,
                            posX - approachCircleSize / 2,
                            posY - approachCircleSize / 2,
                            approachCircleSize,
                            approachCircleSize, hitObject.Color with { W = Math.Min(fadein, fadeout) });
                    }
                

                    r.DrawTexture(_context.Skin.SliderStartCircle,
                        posX - drawSize / 2,
                        posY - drawSize / 2,
                        drawSize,
                        drawSize, hitObject.Color with { W = Math.Min(fadein, fadeout) });


                    if (!_context.Skin.SliderStartCircleExists)
                    {
                        r.DrawTexture(_context.Skin.HitCircleOverlay,
                            posX - drawSize / 2,
                            posY - drawSize / 2,
                            drawSize,
                            drawSize, new Color4<Rgba>(1, 1, 1, 1) with { W = Math.Min(fadein, fadeout) });
                    }


                    var num = hitObject.ComboNumber.ToString();

                    var digitScale = drawSize / (_context.Skin.NumbersHd ? 256f : 128f);
                    float digitWidthTotal = 0;

                    // first pass: compute total width
                    foreach (var c in num)
                    {
                        var digit = c - '0';
                        var tex = _context.Skin.DefaultNumbers[digit];
                        digitWidthTotal += tex.Width * digitScale;
                    }

                    // start centered
                    var cursorX = posX - digitWidthTotal / 2;

                    foreach (var c in num)
                    {
                        var digit = c - '0';
                        var tex = _context.Skin.DefaultNumbers[digit];

                        var w = tex.Width * digitScale;
                        var h = tex.Height * digitScale;

                        r.DrawTexture(
                            tex,
                            cursorX,
                            posY - h / 2,
                            w,
                            h,
                            new Color4<Rgba>(1, 1, 1, 1) with { W = Math.Min(fadein, fadeout) }
                        );

                        cursorX += w;
                    }

                    break;
                }
            }
        }

        foreach (var particle in _hitResultParticles.ToList())
        {
            var age = (float)(clock.CurrentTime - particle.StartTime);
            if (age < 0 || age > particle.MaxLife)
            {
                _hitResultParticles.Remove(particle);
                continue;
            }

            var t = age / particle.MaxLife; // 0.0 → 1.0

            // --- Scale Animation ---
            // Quick shrink from big → normal
            var scale = age < 120f ? MathHelper.Lerp(1.55f, 1.0f, age / 120f) : 1.0f;
            
            // --- Fade & Movement ---
            float alpha;
            const float yOffset = 0f;

            if (age < 380f)
            {
                alpha = 1f;
            }
            else
            {
                var fadeT = (age - 380f) / (particle.MaxLife - 380f);
                alpha = MathHelper.Lerp(1f, 0f, fadeT);
                //yOffset = fadeT * -45f; // move up ~45 pixels
            }

            var texture = particle.Result switch
            {
                HitResult.Good => _context.Skin.Hit300,
                HitResult.Ok => _context.Skin.Hit100,
                HitResult.Meh => _context.Skin.Hit50,
                _ => _context.Skin.Hit0, // miss
            };

            float baseSize = _baseCircleSize * 1.1f; // slightly bigger than circle
            float w = baseSize * scale;
            float h = texture.Height * (w / texture.Width);

            Vector2 drawPos = _playfieldTopLeft + particle.Position * playfieldScale;
            drawPos.Y += yOffset;

            r.DrawCenteredTexture(texture, drawPos, w, h,
                new Color4<Rgba>(1, 1, 1, alpha));
        }


        // bkz: https://osu.ppy.sh/community/forums/topics/1857309?n=1
        _health = Math.Abs((float)Math.Cos(clock.CurrentTime / 1000));

        var scoreboardBgWidth = Width / 2.5f;

        var bgScale = scoreboardBgWidth / _context.Skin.ScorebarBg.Width;
        var scoreboardBgHeight = _context.Skin.ScorebarBg.Height * bgScale;

        r.DrawTexture(_context.Skin.ScorebarBg, 0, 0, scoreboardBgWidth, scoreboardBgHeight, new Color4<Rgba>(1, 1, 1, 1));


        var scoreboardColourWidth = Width / 2.5f;

        var colourScale = scoreboardColourWidth / _context.Skin.ScorebarColour.Width;
        var scoreboardColourHeight = _context.Skin.ScorebarColour.Height * colourScale;

        r.SetScissor(20, 22, (int)(20 + scoreboardColourWidth * _health), (int)scoreboardColourHeight);
        r.DrawTexture(_context.Skin.ScorebarColour, 20, 22, scoreboardColourWidth / 1f, scoreboardColourHeight / 1f,
            new Color4<Rgba>(1, 1, 1, 1));
        r.SetScissor(0, 0, (int)Width, (int)Height);


        // Draw combo count
        var comboNum = _comboCount.ToString();

        var comboDigitScale = Height / 15 / _context.Skin.ScoreNumbers[0].Height;
        float comboTotalWidth = 0;

        // first pass: compute total width
        foreach (var c in comboNum)
        {
            var digit = c - '0';
            var tex = _context.Skin.ScoreNumbers[digit];
            comboTotalWidth += tex.Width * comboDigitScale;
        }

        float comboCursorX = 30;

        foreach (var c in comboNum)
        {
            var digit = c - '0';
            var tex = _context.Skin.ScoreNumbers[digit];

            var w = tex.Width * comboDigitScale;
            var h = tex.Height * comboDigitScale;

            r.DrawTexture(
                tex,
                comboCursorX,
                Height - h - 30,
                w,
                h,
                new Color4<Rgba>(1, 1, 1, 1)
            );

            comboCursorX += w;
        }

        // TODO: Fix the way combo x's position/scale is calculated. its def wrong i think
        var comboxXScale = Height / 20 / _context.Skin.ScoreX.Height;

        r.DrawTexture(
            _context.Skin.ScoreX,
            comboCursorX,
            Height - _context.Skin.ScoreX.Height * comboxXScale - 30,
            _context.Skin.ScoreX.Width * comboxXScale,
            _context.Skin.ScoreX.Height * comboxXScale,
            new Color4<Rgba>(1, 1, 1, 1)
        );


        // Draw score
        var scoreNum = ((int)_totalScore).ToString();

        var scoreDigitScale = Height / 15 / _context.Skin.ScoreNumbers[0].Height; //400 / (_context.Skin.NumbersHd ? 256f : 128f);
        float scoreTotalWidth = 0;

        // first pass: compute total width
        foreach (var c in scoreNum)
        {
            var digit = c - '0';
            var tex = _context.Skin.ScoreNumbers[digit];
            scoreTotalWidth += tex.Width * scoreDigitScale;
        }

        float scoreCursorX = Width - scoreTotalWidth - 30;

        foreach (var c in scoreNum)
        {
            var digit = c - '0';
            var tex = _context.Skin.ScoreNumbers[digit];

            var w = tex.Width * scoreDigitScale;
            var h = tex.Height * scoreDigitScale;

            r.DrawTexture(
                tex,
                scoreCursorX,
                30,
                w,
                h,
                new Color4<Rgba>(1, 1, 1, 1)
            );

            scoreCursorX += w;
        }


        var yellow = new Color4<Rgba>(242 / 255f, 191 / 255f, 36 / 255f, 1);

        // draw volume control
        /*r.DrawRectangle(0, (float)Height / 2 - 150, 40, 300, new Vector4(0.1f, 0.1f, 0.1f, 1));

        var length = Interpolation.Map(_musicVolume, 0, 1, 0, 300);
        r.DrawRectangle(10, (float)Height / 2 - 150, 20, length, yellow);*/


        // timeline
        if (_debugEnabled)
        {
            var songPointer = Interpolation.Map((float)clock.CurrentTime, 0, (float)_songLength, 0, Width);
            r.DrawRectangle(0, Height - 20, songPointer, 20, yellow);
            var cursorSize = new Vector2(30, 60);
            r.DrawRectangle(songPointer - cursorSize.X / 2, Height - cursorSize.Y, cursorSize.X, cursorSize.Y,
                new Color4<Rgba>(1, 1, 1, 1));
        }

        // TODO: add actual stacking for mod icons
        if (Player.State == PlayerState.Autoplay)
        {
            r.DrawTexture(_context.Skin.ModAutoplay, Width - Width / 20 - 50, Height / 8, Width / 20, Width / 20,
                new Color4<Rgba>(1, 1, 1, 1));
        }

        if (_doubleTimeEnabled)
        {
            r.DrawTexture(_context.Skin.ModNightcore, Width - Width / 20 - 50 - Width / 40, Height / 8, Width / 20,
                Width / 20, new Color4<Rgba>(1, 1, 1, 1));
        }
        if (_hidden)
        {
            r.DrawTexture(_context.Skin.ModHidden, Width - Width / 20 - 50 - Width / 40 - 50 - Width / 40, Height / 8, Width / 20,
                Width / 20, new Color4<Rgba>(1, 1, 1, 1));
        }
        
        // Draw timing window
        var timingWindowWidth = Width / 7;
        var timingWindowHeight = timingWindowWidth / 25;
        var timingWindowX = Width / 2 - timingWindowWidth / 2;
        var timingWindowY = Height - timingWindowHeight - timingWindowHeight;
        r.DrawRectangle(timingWindowX, timingWindowY, timingWindowWidth, timingWindowHeight,
            new Color4<Rgba>(1, 1, 1, 1));

        var timingSegmentSize = timingWindowWidth / 6;
        for (int i = 0; i < 6; i++)
        {
            var hitWindowColor = new Color4<Rgba>(1, 1, 1, 1);
            if (i == 0 || i == 5)
            {
                hitWindowColor = new Color4<Rgba>(246 / 255f, 205 / 255f, 77 / 255f, 1.0f);
            }
            else if (i == 1 || i == 4)
            {
                hitWindowColor = new Color4<Rgba>(144 / 255f, 177 / 255f, 54 / 255f, 1.0f);
            }
            else
            {
                hitWindowColor = new Color4<Rgba>(128 / 255f, 201 / 255f, 249 / 255f, 1.0f);
            }

            r.DrawRectangle(
                Width / 2 - timingWindowWidth / 2 + (timingSegmentSize * i),
                Height - timingWindowHeight - timingWindowHeight,
                timingSegmentSize,
                timingWindowHeight,
                hitWindowColor);
        }

        foreach (var indicator in _hitIndicators)
        {
            var range = 200 - 10 * _beatmap.OverallDifficulty;
            var cursorX = Interpolation.Map((float)indicator.Offset, -range, range, timingWindowX,
                timingWindowX + timingWindowWidth);
            r.DrawRectangle(cursorX, timingWindowY - timingWindowHeight, 3, timingWindowHeight * 2,
                new Color4<Rgba>(1, 1, 1, (float)(indicator.Life / HitIndicatorMaxLife)));
        }
        
        // Unranked text
        if (Player.State == PlayerState.Autoplay || Player.State == PlayerState.Replay)
        {
            var unrankedWidth = Width / 10;
            var unrankedHeight = _context.Skin.PlayUnranked.Height * (unrankedWidth / _context.Skin.PlayUnranked.Width);
            r.DrawTexture(_context.Skin.PlayUnranked, Width/2 - unrankedWidth/2, Height/10, unrankedWidth, unrankedHeight, new Color4<Rgba>(1, 1, 1, 1));
        }

        if (_isPaused)
        {
            //r.DrawRectangle(0, 0, Width, Height, new Vector4(0, 0, 0, 0.85f));
        }
        
        // Game cursor
        // Update trail lifetimes first so the draw loop can use a stable count (prevents 1-frame alpha spikes).
        

        var trailSnapshot = trails.ToArray();
        var size = _baseCircleSize / 2f;
        _lastCursorPosition = new(Player.Cursor.X, Player.Cursor.Y);

        if (trailSnapshot.Length > 0)
        {
            var i = trailSnapshot.Length;
            for (var idx = trailSnapshot.Length - 1; idx >= 0; idx--)
            {
                var trail = trailSnapshot[idx];
                size -= 1;

                var alpha = (float)Math.Max(trailSnapshot.Length / 1.2f, i) / trailSnapshot.Length;
                r.DrawTexture(_context.Skin.CursorTrail,
                    trail.Position.X - size / 2,
                    trail.Position.Y - size / 2,
                    size, size, new Color4<Rgba>(1, 1, 1, 1));
                i--;
            }
        }


        // draw cursor
        size = _baseCircleSize / 2f; // _cursorTexture.Width * 1.5f;
        if (_context.Skin.HasCursorMiddle)
        {
            r.DrawTexture(_context.Skin.CursorMiddle,
                Player.Cursor.X - size / 4,
                Player.Cursor.Y - size / 4,
                size / 2, size / 2, new Color4<Rgba>(1, 1, 1, 1));
        }

        r.DrawTexture(_context.Skin.Cursor,
            Player.Cursor.X - size / 2,
            Player.Cursor.Y - size / 2,
            size, size, new Color4<Rgba>(1, 1, 1, 1));
        
        
        if (_debugEnabled)
        {
            r.DrawText(Fonts.Default,
                $"Cursor: {clock.CurrentTime:F0}ms | TrackPos: {_songTrack?.Position:F0}ms\nSongLength: {_songLength:F0}ms\nSampleTracks: {AudioManager.Instance.SampleTracks.Count}/50",
                new Vector2(10, 200), Height/45, new Color4<Rgba>(1, 1, 0, 1));
            r.FlushText(Fonts.Default);
        }
        
        r.PopScissor();
    }
    
    public override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        Width = e.Width;
        Height = e.Height;
    }

    public override void OnEnter()
    {
        base.OnEnter();
        
        for (int i = 0; i < 16; i++)
        {
            _sliderFramebuffers[i] = new()
            {
                Framebuffer = new Framebuffer(1280, 720),
                Duration = 0,
                Time = 0,
            };
        }

        _hidden = false;
        // SetBeatmap(new Beatmap(Resources.GetPath("Resources/Songs/Wakeshima Kanon/ASCA - Nisemono no Koi ni Sayounara with Wakeshima Kanon (timemon) [Kyou's Extra].osu")));
        // _player.SetReplay(Replay.ParseReplay(Resources.GetPath("Resources/Replays/kanon.osr")));
        //SetBeatmap(new Beatmap(Resources.GetPath("Resources/Songs/983942 Oomori Seiko - JUSTadICE (TV Size)/Oomori Seiko - JUSTadICE (TV Size) (fieryrage) [Extreme].osu")));
        //Player?.SetReplay(Replay.ParseReplay(Resources.GetPath("Resources/Replays/fiery.osr")));
        //Player?.SetState(PlayerState.Replay);
        //_doubleTimeEnabled = true; _songTrack.Speed = 1.5f;
        clock = new(false);
    }

    public override void OnExit()
    {
        Dispose();
        base.OnExit();
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
        //clock.CurrentTime = 0;
        clock.Seek(0);
        _songTrack.Speed = 1.00f;

        _startingTimer = WaitingTime + _beatmap.AudioLeadIn;
        _musicStarted = false;

        _beatmap.CalculatePrepass();
        Player = new Player(_beatmap, this);
        SDL_ShowCursor();
        
        ResetObjectsAfter(0);

        _sortedObjects = _beatmap.HitObjects
            .OrderByDescending(h => Math.Abs(h.Time - clock.CurrentTime));

        _backgroundTexture?.Dispose();
        _backgroundTexture = new Texture(Path.Combine(_beatmap.Folder, _beatmap.BackgroundFile));

        //Difficulty multiplier = Round((HP Drain + Circle Size + Overall Difficulty + Clamp(Hit object count / Drain time in seconds * 8, 0, 16)) / 38 * 5)
        var objs = _beatmap.HitObjects.OrderBy(t => t.Time);
        var drainTime = objs.Any() ? Math.Abs(objs.First().Time - objs.Last().Time) / 1000f : 0;
        _difficultyMultiplier = (float)Math.Round((_beatmap.HPDrainRate + _beatmap.CircleSize +
                                                   _beatmap.OverallDifficulty
                                                   + Math.Clamp(_beatmap.HitObjects.Count / drainTime * 8, 0, 16)) /
                                                  38 *
                                                  5);
        _comboCount = 0;
        _totalScore = 0;

        Logger.Instance.Info($"Beatmap set to {beatmap}");
    }
    
    public new void Dispose()
    {
        Dispose(true);
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _backgroundTexture?.Dispose();
            _songTrack?.Dispose();
            _songAudio?.Dispose();
        }
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
            Framebuffer = new Framebuffer(r, (int)Width, (int)Height),
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

    private class HitResultParticle
    {
        public Vector2 Position;
        public HitResult Result;
        public double StartTime; // When the hit happened
        public float MaxLife = 620f; // Total lifetime in ms

        // Current state
        public float CurrentScale = 1.4f;
        public float CurrentAlpha = 1f;
        public Vector2 CurrentOffset = Vector2.Zero;
    }

    private class HitIndicator
    {
        public double Offset;
        public double Life;
    }
}