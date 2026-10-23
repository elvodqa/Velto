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
    private List<HitObject> _orderedObjects = new();

    private bool _isPaused;
    private double _pauseInputCooldown;
    private RectangleF _pauseResumeButton;
    private RectangleF _pauseRetryButton;
    private RectangleF _pauseBackButton;
    private bool _pauseResumeHovered;
    private bool _pauseRetryHovered;
    private bool _pauseBackHovered;
    
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
    private float _currentScore = 0;
    private int _count300 = 0;
    private int _count100 = 0;
    private int _count50 = 0;
    private int _countMiss = 0;
    private float _difficultyMultiplier = 1.0f;
    private float _modMultiplier = 1.0f;
    private readonly List<HitIndicator> _hitIndicators = new();
    private readonly Dictionary<string, AudioChannel> _mapSamples = new();
    private readonly HashSet<string> _unloadableSamples = new();
    private readonly Dictionary<Slider, Track> _sliderSlides = new();

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
    public StopwatchClock clock;

    public GameScreen(OsuContext context) : base(context)
    {
        _context = context;
        clock = new(false);
    }

    public void ToggleMenu()
    {
        if (_isPaused)
            ResumeGame();
        else
            PauseGame();
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

        if (_isPaused)
        {
            UpdatePauseMenu(delta);
            return;
        }

        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_ESCAPE) ||
            Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_SPACE))
        {
            PauseGame();
            return;
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
            //SetBeatmap(new Beatmap(Resources.GetPath("Resources/Songs/983942 Oomori Seiko - JUSTadICE (TV Size)/Oomori Seiko - JUSTadICE (TV Size) (fieryrage) [Extreme].osu")));
            //Player.SetReplay(Replay.ParseReplay(Resources.GetPath("Resources/Replays/fiery.osr")));
            SetBeatmap(new Beatmap(Resources.GetPath("Resources/Songs/564165 ke-ji feat. Nanahira - Ange du Blanc Pur/ke-ji feat. Nanahira - Ange du Blanc Pur (BarkingMadDog) [ABSOLUTION].osu")));
            Player.SetReplay(Replay.ParseReplay(Resources.GetPath("Resources/Replays/ange.osr")));

            Player.SetState(PlayerState.Replay);
        }

        if (Input.IsKeyDown(SDL_Scancode.SDL_SCANCODE_LSHIFT))
        {
            if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_RIGHT))
            {
                _songTrack?.Speed += 0.10f;
                clock.Rate += 0.10f;
                AudioManager.Instance.SampleSpeed += 0.10f;
            }

            if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_LEFT))
            {
                _songTrack?.Speed -= 0.10f;
                clock.Rate -= 0.10f;
                AudioManager.Instance.SampleSpeed -= 0.10f;
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
        if (Player.State != PlayerState.Replay)
            Judge(clock.CurrentTime);
        UpdateHealth(delta);
        
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

    private void PauseGame()
    {
        if (_isPaused) return;

        _isPaused = true;
        _pauseInputCooldown = 250;
        clock.Stop();
        _songTrack?.Pause();
        StopAllSliderSlides();
        UpdatePauseLayout();
        AudioManager.Instance.PlaySample(_context.Skin.MenuClick);
    }

    private void ResumeGame()
    {
        if (!_isPaused) return;

        _isPaused = false;
        _pauseInputCooldown = 250;

        if (_musicStarted)
        {
            clock.Start();
            if (_songTrack != null)
                _songTrack.Position = clock.CurrentTime;
            _songTrack?.Resume();
        }

        AudioManager.Instance.PlaySample(_context.Skin.MenuClick);
    }

    private void UpdatePauseMenu(double delta)
    {
        if (_pauseInputCooldown > 0)
            _pauseInputCooldown -= delta;

        UpdatePauseLayout();

        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_ESCAPE) ||
            Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_SPACE) ||
            Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_RETURN))
        {
            ResumeGame();
            return;
        }

        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_R))
        {
            RetryGame();
            return;
        }

        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_Q) ||
            Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_BACKSPACE))
        {
            BackToSongSelect();
            return;
        }

        var mouse = new Vector2(Input.MouseX, Input.MouseY);
        _pauseResumeHovered = Contains(_pauseResumeButton, mouse);
        _pauseRetryHovered = Contains(_pauseRetryButton, mouse);
        _pauseBackHovered = Contains(_pauseBackButton, mouse);

        if (_pauseInputCooldown > 0 || !Input.IsMouseJustPressed(SDLButton.SDL_BUTTON_LEFT))
            return;

        if (_pauseResumeHovered)
        {
            ResumeGame();
            return;
        }

        if (_pauseRetryHovered)
        {
            RetryGame();
            return;
        }

        if (_pauseBackHovered)
            BackToSongSelect();
    }

    private void UpdatePauseLayout()
    {
        var width = Width * 0.28f;
        var height = Height * 0.075f;
        var x = Width / 2f - width / 2f;
        var y = Height * 0.47f;
        var gap = Height * 0.018f;

        _pauseResumeButton = new RectangleF(x, y, width, height);
        _pauseRetryButton = new RectangleF(x, y + height + gap, width, height);
        _pauseBackButton = new RectangleF(x, y + (height + gap) * 2f, width, height);
    }

    private void RetryGame()
    {
        _isPaused = false;
        StopAllSliderSlides();
        _songTrack?.Stop(0);

        var state = Player.State;
        var replay = Player.Replay;

        var game = new GameScreen(_context);
        game.SetBeatmap(_beatmap);
        game.Player.SetState(state);
        game._doubleTimeEnabled = _doubleTimeEnabled;

        if (_doubleTimeEnabled)
        {
            game._songTrack!.Speed = 1.5f;
            game.clock.Rate = 1.5f;
        }

        if (state == PlayerState.Replay && replay != null)
            game.Player.SetReplay(replay);

        AudioManager.Instance.PlaySample(_context.Skin.PauseRetryClick);
        Transition(game, 200);
    }

    private void BackToSongSelect()
    {
        _isPaused = false;
        StopAllSliderSlides();
        _songTrack?.Stop(0);
        AudioManager.Instance.PlaySample(_context.Skin.MenuBack);
        Transition(new SongSelectScreen(_context), 200);
    }

    private void DrawPauseMenu(Renderer r)
    {
        var panelWidth = Width * 0.4f;
        var panelHeight = Height * 0.55f;
        var panelX = Width / 2f - panelWidth / 2f;
        var panelY = Height / 2f - panelHeight / 2f;

        var pink = new Color4<Rgba>(1f, 0.4f, 0.67f, 1f);

        r.DrawRectangle(0, 0, Width, Height, new Color4<Rgba>(0f, 0f, 0f, 0.65f));
        r.DrawRectangle(panelX, panelY, panelWidth, panelHeight,
            new Color4<Rgba>(0.04f, 0.07f, 0.12f, 0.97f));
        r.DrawRectangle(panelX, panelY, panelWidth, Math.Max(4f, Height * 0.008f), pink);
        r.DrawRectangleBorder(panelX, panelY, panelWidth, panelHeight,
            Math.Max(1f, Height / 720f), pink with { W = 0.45f });

        r.DrawTextCentered(Fonts.Default, "Paused",
            new Vector2(Width / 2f, panelY + panelHeight * 0.18f),
            panelHeight * 0.09f, Color4.White);

        r.DrawTextCentered(Fonts.Default, $"{_beatmap.Artist} - {_beatmap.Title}",
            new Vector2(Width / 2f, panelY + panelHeight * 0.28f),
            panelHeight * 0.04f, new Color4<Rgba>(1f, 1f, 1f, 0.65f));

        r.DrawTextCentered(Fonts.Default, $"[{_beatmap.Version}]",
            new Vector2(Width / 2f, panelY + panelHeight * 0.34f),
            panelHeight * 0.035f, new Color4<Rgba>(1f, 1f, 1f, 0.5f));

        DrawPauseButton(r, _pauseResumeButton, "Continue", "ESC / SPACE", _pauseResumeHovered, true);
        DrawPauseButton(r, _pauseRetryButton, "Retry", "R", _pauseRetryHovered, false);
        DrawPauseButton(r, _pauseBackButton, "Back to Song Select", "Q", _pauseBackHovered, false);
    }

    private void DrawPauseButton(Renderer r, RectangleF rect, string label, string hint, bool hovered,
        bool accent)
    {
        var pink = new Color4<Rgba>(1f, 0.4f, 0.67f, 1f);
        var background = accent
            ? new Color4<Rgba>(1f, 0.4f, 0.67f, hovered ? 0.35f : 0.18f)
            : new Color4<Rgba>(1f, 1f, 1f, hovered ? 0.1f : 0.035f);

        r.DrawRectangle(rect.X, rect.Y, rect.Width, rect.Height, background);
        r.DrawRectangleBorder(rect.X, rect.Y, rect.Width, rect.Height, Math.Max(1f, Height / 900f),
            accent || hovered ? pink with { W = 0.75f } : new Color4<Rgba>(1f, 1f, 1f, 0.1f));

        r.DrawTextCentered(Fonts.Default, label,
            new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height * 0.4f),
            rect.Height * 0.36f, Color4.White);

        r.DrawTextCentered(Fonts.Default, hint,
            new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height * 0.74f),
            rect.Height * 0.2f, new Color4<Rgba>(1f, 1f, 1f, 0.62f));
    }

    private static bool Contains(RectangleF rect, Vector2 point)
    {
        return rect.Contains(point.X, point.Y);
    }

    public void Judge(double time)
    {
        var scale = _playfieldWidth / PlayfieldW;
        var osuRadius = 54.4f - 4.48f * _beatmap.CircleSize;
        var hitWindow300 = 80 - 6 * _beatmap.OverallDifficulty;
        var hitWindow100 = 140 - 8 * _beatmap.OverallDifficulty;
        var hitWindow50 = 200 - 10 * _beatmap.OverallDifficulty;

        foreach (var hitObject in _beatmap.HitObjects)
        {
            if (hitObject.Time > time + _beatmap.Preempt) continue;

            var playerCursor = Player.Cursor;
            var isPressed = Player.ActionPrimaryPressed || Player.ActionSecondaryPressed;

            if (hitObject is HitCircle circle)
            {
                if (circle.HitResult != HitResult.None) continue;

                var circlePosition = _playfieldTopLeft + circle.Position * scale;
                var difference = Math.Abs(time - circle.Time);
                var inRange = Vector2.Distance(circlePosition, playerCursor) <= osuRadius * scale;

                if (inRange && isPressed && difference <= hitWindow50)
                {
                    var result = difference <= hitWindow300
                        ? HitResult.Good
                        : difference <= hitWindow100
                            ? HitResult.Ok
                            : HitResult.Meh;

                    circle.HitTime = time;
                    circle.HitResult = result;
                    RegisterJudgement(result);
                    AddScore(HitValue(result));
                    AddResultParticle(circle.Position, result, time);
                    PlayHitSound(circle.HitSound, circle.EffectiveSampleSet, circle.EffectiveAdditionSampleSet,
                        circle.EffectiveSampleIndex);
                    _hitIndicators.Add(new()
                    {
                        Life = HitIndicatorMaxLife,
                        Offset = time - circle.Time,
                    });
                }
                else if (time - hitWindow50 >= circle.Time)
                {
                    circle.HitResult = HitResult.Miss;
                    circle.Failed = true;
                    RegisterJudgement(HitResult.Miss);
                    AddResultParticle(circle.Position, HitResult.Miss, time);
                    AudioManager.Instance.PlaySample(_context.Skin.ComboBreak);
                }

                if (isPressed)
                {
                    break;
                }
            }
            else if (hitObject is Slider slider)
            {
                var endTime = slider.Time + slider.Duration;

                if (!slider.JudgementDone && time >= endTime)
                {
                    var previousTime = Math.Max(slider.LastFollowUpdate, slider.Time);
                    var followedAtEnd = slider.LastHeld >= endTime - Slider.FORGIVING_TIME;

                    ProcessSliderParts(slider, endTime, previousTime,
                        followedAtEnd || slider.WasFollowingPreviousFrame);

                    slider.WasFollowedAtEnd = followedAtEnd;

                    if (followedAtEnd)
                    {
                        slider.TailHit = true;
                        slider.PartsHit++;
                        AddSliderPartScore(30);
                        AdjustHealth(0.02f);
                        PlaySliderEdgeSound(slider, slider.SlideRepeatCount);
                    }
                    else
                    {
                        AdjustHealth(-0.01f);
                    }

                    RegisterSliderCompletion(slider);
                    StopSliderSlide(slider);
                    slider.JudgementDone = true;
                }

                if (time > endTime)
                    continue;

                if (slider.HitResult == HitResult.None)
                {
                    var difference = Math.Abs(time - slider.Time);
                    var headPosition = _playfieldTopLeft + slider.Position * scale;
                    var inRange = Vector2.Distance(headPosition, playerCursor) <= osuRadius * scale;

                    if (inRange && isPressed && difference <= hitWindow50)
                    {
                        var result = difference <= hitWindow300
                            ? HitResult.Good
                            : difference <= hitWindow100
                                ? HitResult.Ok
                                : HitResult.Meh;

                        slider.HitTime = time;
                        slider.HitResult = result;
                        slider.PartsHit++;
                        ApplyHealth(result);
                        AddScore(HitValue(result));
                        AddResultParticle(slider.Position, result, time);
                        PlaySliderEdgeSound(slider, 0);
                        _hitIndicators.Add(new()
                        {
                            Life = HitIndicatorMaxLife,
                            Offset = time - slider.Time,
                        });
                    }
                    else if (time - hitWindow50 >= slider.Time)
                    {
                        slider.HitResult = HitResult.Miss;
                        slider.Failed = true;
                        _comboCount = 0;
                        ApplyHealth(HitResult.Miss);
                        AddResultParticle(slider.Position, HitResult.Miss, time);
                        AudioManager.Instance.PlaySample(_context.Skin.ComboBreak);
                    }
                }

                var followCirclePos = slider.GetPositionAt(time);
                var ballPosition = _playfieldTopLeft + followCirclePos * scale;
                var radiusFollowCircle = (_baseCircleSize * 2.5f) / 2f;

                var sliderActive = time >= slider.Time && time <= endTime;
                var isInFollowRange = sliderActive &&
                                      Vector2.Distance(ballPosition, playerCursor) <= radiusFollowCircle;
                var isHolding = Player.ActionPrimaryDown || Player.ActionSecondaryDown;
                var isFollowingNow = isInFollowRange && isHolding;

                if (sliderActive)
                {
                    var previousTime = Math.Max(slider.LastFollowUpdate, slider.Time);
                    var deltaFollow = time - previousTime;

                    ProcessSliderParts(slider, time, previousTime, isFollowingNow);
                    slider.IsCurrentlyBeingFollowed = isFollowingNow;
                    UpdateSliderSlide(slider, isFollowingNow);

                    if (isFollowingNow)
                    {
                        slider.TotalFollowTime += deltaFollow;

                        if (slider.WasFollowingPreviousFrame)
                            slider.CurrentContinuousFollow += deltaFollow;
                        else
                            slider.CurrentContinuousFollow = deltaFollow;

                        if (slider.CurrentContinuousFollow > slider.LongestContinuousFollow)
                            slider.LongestContinuousFollow = slider.CurrentContinuousFollow;

                        slider.LastHeld = time;
                    }
                    else
                    {
                        slider.CurrentContinuousFollow = 0;
                    }

                    slider.LastFollowUpdate = time;
                    slider.WasFollowingPreviousFrame = isFollowingNow;
                }

                if (isPressed)
                {
                    break;
                }
            }
        }
    }

    private float Accuracy
    {
        get
        {
            var total = _count300 + _count100 + _count50 + _countMiss;
            if (total == 0) return 1f;
            return (6f * _count300 + 2f * _count100 + _count50) / (6f * total);
        }
    }

    private static int HitValue(HitResult result)
    {
        return result switch
        {
            HitResult.Good => 300,
            HitResult.Ok => 100,
            HitResult.Meh => 50,
            _ => 0,
        };
    }

    private void AddScore(int hitValue)
    {
        if (hitValue <= 0) return;

        _comboCount++;
        _currentScore += hitValue * (1 + Math.Max(_comboCount - 1, 0) * _difficultyMultiplier *
            _modMultiplier / 25f);
    }

    private void RegisterJudgement(HitResult result)
    {
        switch (result)
        {
            case HitResult.Good:
                _count300++;
                break;
            case HitResult.Ok:
                _count100++;
                break;
            case HitResult.Meh:
                _count50++;
                break;
            case HitResult.Miss:
                _countMiss++;
                _comboCount = 0;
                break;
        }

        ApplyHealth(result);
    }

    private void ApplyHealth(HitResult result)
    {
        var hp = _beatmap.HPDrainRate;
        var change = result switch
        {
            HitResult.Good => 0.03f,
            HitResult.Ok => MapDifficultyRange(hp, 0.088f, 0.011f, 0.011f),
            HitResult.Meh => MapDifficultyRange(hp, 0.008f, 0.002f, 0.002f),
            HitResult.Miss => -MapDifficultyRange(hp, 0.03f, 0.125f, 0.2f),
            _ => 0f,
        };

        AdjustHealth(change);
    }

    private void AdjustHealth(float amount)
    {
        _health = Math.Clamp(_health + amount, 0f, 1f);
    }

    private void UpdateHealth(double delta)
    {
        if (!_musicStarted || _isPaused || _orderedObjects.Count == 0) return;

        var firstTime = _orderedObjects[0].Time;
        var lastObject = _orderedObjects[^1];
        var lastTime = lastObject switch
        {
            Slider slider => slider.Time + slider.Duration,
            Spinner spinner => spinner.EndTime,
            _ => lastObject.Time,
        };
        if (clock.CurrentTime < firstTime || clock.CurrentTime > lastTime) return;

        var rate = 0.0000025f + _beatmap.HPDrainRate * 0.0000015f;
        var drain = rate * (float)(delta * clock.Rate);
        AdjustHealth(-drain);
    }

    private static float MapDifficultyRange(float value, float min, float mid, float max)
    {
        if (value > 5f)
            return mid + (max - mid) * (value - 5f) / 5f;
        if (value < 5f)
            return min + (mid - min) * value / 5f;
        return mid;
    }

    private void PlayHitSound(int sound, int normalSet, int additionSet, int sampleIndex)
    {
        PlaySampleFrom(normalSet, "hitnormal", sampleIndex);

        if ((sound & (int)HitSoundType.Whistle) != 0)
            PlaySampleFrom(additionSet, "hitwhistle", sampleIndex);
        if ((sound & (int)HitSoundType.Finish) != 0)
            PlaySampleFrom(additionSet, "hitfinish", sampleIndex);
        if ((sound & (int)HitSoundType.Clap) != 0)
            PlaySampleFrom(additionSet, "hitclap", sampleIndex);
    }

    private void PlaySliderEdgeSound(Slider slider, int edge)
    {
        var sound = slider.HitSound;

        if (slider.EdgeSounds.Count > 0)
            sound = slider.EdgeSounds[Math.Min(edge, slider.EdgeSounds.Count - 1)];

        var normalSet = slider.EffectiveSampleSet;
        var additionSet = slider.EffectiveAdditionSampleSet;

        if (slider.EdgeSets.Count > 0)
        {
            var sets = slider.EdgeSets[Math.Min(edge, slider.EdgeSets.Count - 1)];
            if (sets.Normal > 0)
                normalSet = sets.Normal;
            if (sets.Addition > 0)
                additionSet = sets.Addition;
        }

        PlayHitSound(sound, normalSet, additionSet, slider.EffectiveSampleIndex);
    }

    private void ProcessSliderParts(Slider slider, double time, double previousTime, bool following)
    {
        if (slider.SpanDuration <= 0) return;

        var spanIndex = (int)Math.Floor((time - slider.Time) / slider.SpanDuration);
        spanIndex = Math.Clamp(spanIndex, 0, Math.Max(0, slider.SlideRepeatCount - 1));

        while (slider.NextRepeatIndex <= spanIndex &&
               slider.NextRepeatIndex < slider.SlideRepeatCount)
        {
            var edge = slider.NextRepeatIndex;
            var repeatTime = slider.Time + edge * slider.SpanDuration;

            if (following && repeatTime > previousTime - 0.5)
            {
                slider.RepeatHits++;
                slider.PartsHit++;
                AddSliderPartScore(30);
                AdjustHealth(0.02f);
                PlaySliderEdgeSound(slider, edge);
            }
            else
            {
                slider.RepeatMisses++;
                _comboCount = 0;
                AdjustHealth(-0.01f);
            }

            slider.NextRepeatIndex++;
        }

        while (slider.NextTickIndex < slider.TickTimes.Count &&
               slider.TickTimes[slider.NextTickIndex] <= time)
        {
            var tickTime = slider.TickTimes[slider.NextTickIndex++];

            if (following && tickTime > previousTime && tickTime <= time)
            {
                slider.TickHits++;
                slider.PartsHit++;
                AddSliderPartScore(10);
                AdjustHealth(0.005f);
                PlaySampleFrom(slider.EffectiveSampleSet, "slidertick", slider.EffectiveSampleIndex);
            }
            else
            {
                slider.TickMisses++;
                _comboCount = 0;
                AdjustHealth(-0.002f);
            }
        }
    }

    private void AddSliderPartScore(int value)
    {
        _comboCount++;
        _currentScore += value;
    }

    private void RegisterSliderCompletion(Slider slider)
    {
        var total = Math.Max(1, slider.PartsTotal);
        var ratio = slider.PartsHit / (float)total;

        var result = ratio >= 0.999f
            ? HitResult.Good
            : ratio >= 0.5f
                ? HitResult.Ok
                : ratio > 0f
                    ? HitResult.Meh
                    : HitResult.Miss;

        switch (result)
        {
            case HitResult.Good:
                _count300++;
                break;
            case HitResult.Ok:
                _count100++;
                break;
            case HitResult.Meh:
                _count50++;
                break;
            case HitResult.Miss:
                _countMiss++;
                _comboCount = 0;
                break;
        }
    }

    private void UpdateSliderSlide(Slider slider, bool following)
    {
        if (following)
        {
            if (_sliderSlides.ContainsKey(slider)) return;

            var sample = GetGameplaySample(slider.EffectiveSampleSet, "sliderslide",
                slider.EffectiveSampleIndex);
            if (sample?.IsLoaded != true) return;

            var track = AudioManager.Instance.CreateTrack();
            track.Audio = sample;
            track.Volume = AudioManager.Instance.SampleVolume;
            track.Speed = AudioManager.Instance.SampleSpeed;
            track.Loops = -1;
            track.Play();
            _sliderSlides[slider] = track;
        }
        else
        {
            StopSliderSlide(slider);
        }
    }

    private void StopSliderSlide(Slider slider)
    {
        if (_sliderSlides.Remove(slider, out var track))
            track.Dispose();
    }

    private void StopAllSliderSlides()
    {
        foreach (var track in _sliderSlides.Values)
            track.Dispose();

        _sliderSlides.Clear();
    }

    private void PlaySampleFrom(int sampleSet, string name, int sampleIndex)
    {
        var sample = GetGameplaySample(sampleSet, name, sampleIndex);
        if (sample?.IsLoaded == true)
            AudioManager.Instance.PlaySample(sample);
    }

    private AudioChannel? GetGameplaySample(int sampleSet, string name, int sampleIndex)
    {
        if (sampleSet is < 1 or > 3)
            sampleSet = 1;

        var type = sampleSet switch
        {
            2 => "soft",
            3 => "drum",
            _ => "normal",
        };

        var key = $"{type}:{name}:{sampleIndex}";
        if (_mapSamples.TryGetValue(key, out var cached))
            return cached;

        if (!_unloadableSamples.Contains(key))
        {
            var names = sampleIndex > 0
                ? new[] { $"{type}-{name}{sampleIndex}", $"{type}-{name}" }
                : new[] { $"{type}-{name}" };

            var folders = sampleIndex > 0
                ? new[] { _beatmap.Folder, _context.Skin.Folder, Resources.DefaultSkinPath }
                : new[] { _beatmap.Folder };

            foreach (var folder in folders)
            {
                foreach (var candidate in names)
                {
                    foreach (var extension in new[] { ".wav", ".ogg", ".mp3" })
                    {
                        var path = Path.Combine(folder, candidate + extension);
                        if (!File.Exists(path)) continue;

                        var loaded = AudioManager.Instance.TryLoadAudio(path);
                        if (loaded == null) continue;

                        _mapSamples[key] = loaded;
                        return loaded;
                    }
                }
            }

            _unloadableSamples.Add(key);
        }

        var bank = sampleSet switch
        {
            2 => _context.Skin.Soft,
            3 => _context.Skin.Drum,
            _ => _context.Skin.Normal,
        };

        return bank.GetChannel(name);
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
        
        DrawFollowPoints(r, playfieldScale);

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
                            drawSize, circle.Color with { W = Math.Min(fadein, fadeout) });

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

                    DrawSliderEndCircle(r, slider, playfieldScale, drawSize, Math.Min(fadein, fadeout));
                    DrawSliderTicks(r, slider, playfieldScale, sliderOpacity);
                    DrawSliderRepeatArrows(r, slider, playfieldScale, sliderOpacity);

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

            float baseSize = _baseCircleSize * 0.7f;
            float w = baseSize * scale;
            float h = texture.Height * (w / texture.Width);

            Vector2 drawPos = _playfieldTopLeft + particle.Position * playfieldScale;
            drawPos.Y += yOffset;

            r.DrawCenteredTexture(texture, drawPos, w, h,
                new Color4<Rgba>(1, 1, 1, alpha));
        }


        var scoreboardBgWidth = Width / 2.5f;

        var bgScale = scoreboardBgWidth / _context.Skin.ScorebarBg.Width;
        var scoreboardBgHeight = _context.Skin.ScorebarBg.Height * bgScale;

        r.DrawTexture(_context.Skin.ScorebarBg, 0, 0, scoreboardBgWidth, scoreboardBgHeight, new Color4<Rgba>(1, 1, 1, 1));


        var scoreboardColourWidth = Width / 2.5f;

        var colourScale = scoreboardColourWidth / _context.Skin.ScorebarColour.Width;
        var scoreboardColourHeight = _context.Skin.ScorebarColour.Height * colourScale;

        var healthWidth = Math.Clamp(scoreboardColourWidth * _health, 0f, scoreboardColourWidth);
        r.PushScissor(20, 22, (int)healthWidth, (int)scoreboardColourHeight);
        r.DrawTexture(_context.Skin.ScorebarColour, 20, 22, scoreboardColourWidth, scoreboardColourHeight,
            new Color4<Rgba>(1, 1, 1, 1));
        r.PopScissor();


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
        var scoreNum = ((int)_currentScore).ToString();

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
        
        
        DrawScoreString(r, $"{Accuracy * 100f:0.00}%", Width - 30, 30 + Height / 15f + 10,
            scoreDigitScale * 0.55f, true);


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
            DrawPauseMenu(r);
            r.FlushText(Fonts.Default);
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

    private void DrawFollowPoints(Renderer r, float playfieldScale)
    {
        if (_orderedObjects.Count < 2) return;
        if (!_context.Skin.HasAnimatedFollowPoints && _context.Skin.FollowPoint == null) return;

        var baseScale = _baseCircleSize / 128f;
        var preempt = 800.0 * Math.Min(1.0, _beatmap.Preempt / 450.0);

        for (var i = 0; i < _orderedObjects.Count - 1; i++)
        {
            var start = _orderedObjects[i];
            var end = _orderedObjects[i + 1];

            if (start is Spinner || end is Spinner) continue;

            var startTime = start is Slider startSlider ? startSlider.Time + startSlider.Duration : start.Time;
            var startPosition = start is Slider slider ? slider.GetPositionAt(startTime) : start.Position;
            var endPosition = end.Position;
            var distanceVector = endPosition - startPosition;
            var distance = distanceVector.Length;

            if (distance <= 48f) continue;

            var duration = end.Time - startTime;
            if (duration <= 0) continue;

            var rotation = -MathHelper.RadiansToDegrees(MathF.Atan2(distanceVector.Y, distanceVector.X));

            for (var d = 48f; d < distance - 32f; d += 32f)
            {
                var fraction = d / distance;
                var fadeOutTime = startTime + fraction * duration;
                var fadeInTime = fadeOutTime - preempt;

                const float fadeDuration = 400f;
                if (clock.CurrentTime < fadeInTime || clock.CurrentTime > fadeOutTime + fadeDuration) continue;

                float alpha;
                float pointScale;
                Vector2 position;

                if (clock.CurrentTime < fadeInTime + fadeDuration)
                {
                    var t = Math.Clamp((float)((clock.CurrentTime - fadeInTime) / fadeDuration), 0f, 1f);
                    t = 1f - (1f - t) * (1f - t);
                    alpha = t;
                    pointScale = MathHelper.Lerp(1.5f, 1f, t);
                    position = Vector2.Lerp(startPosition + distanceVector * (fraction - 0.1f),
                        startPosition + distanceVector * fraction, t);
                }
                else
                {
                    var t = Math.Clamp((float)((clock.CurrentTime - fadeOutTime) / fadeDuration), 0f, 1f);
                    alpha = 1f - t;
                    pointScale = 1f;
                    position = startPosition + distanceVector * fraction;
                }

                if (alpha <= 0f) continue;

                var texture = GetFollowPointTexture(fadeInTime);
                if (texture == null) continue;

                var sizeScale = baseScale * texture.Scale * pointScale;
                var width = texture.Width * sizeScale;
                var height = texture.Height * sizeScale;
                var screenPosition = _playfieldTopLeft + position * playfieldScale;

                r.DrawTexture(texture, screenPosition.X - width / 2f, screenPosition.Y - height / 2f,
                    width, height, new Color4<Rgba>(1, 1, 1, alpha), rotation);
            }
        }
    }

    private Texture? GetFollowPointTexture(double fadeInTime)
    {
        if (!_context.Skin.HasAnimatedFollowPoints) return _context.Skin.FollowPoint;
        if (_context.Skin.FollowPoints.Count == 0) return null;

        var elapsed = clock.CurrentTime - fadeInTime;
        var frame = (int)(elapsed / 40.0) % _context.Skin.FollowPoints.Count;
        if (frame < 0) frame += _context.Skin.FollowPoints.Count;
        return _context.Skin.FollowPoints[frame];
    }

    private void DrawSliderTicks(Renderer r, Slider slider, float playfieldScale, float opacity)
    {
        if (slider.TickTimes.Count == 0) return;

        var texture = _context.Skin.SliderScorePoint;
        var scale = _baseCircleSize / 128f * texture.Scale;

        foreach (var tickTime in slider.TickTimes)
        {
            var position = slider.GetPositionAt(tickTime);
            var screenPosition = _playfieldTopLeft + position * playfieldScale;
            var width = texture.Width * scale;
            var height = texture.Height * scale;

            r.DrawTexture(texture, screenPosition.X - width / 2f, screenPosition.Y - height / 2f,
                width, height, new Color4<Rgba>(1, 1, 1, 1) with { W = opacity });
        }
    }

    private void DrawSliderEndCircle(Renderer r, Slider slider, float playfieldScale, float drawSize, float opacity)
    {
        var position = slider.GetPositionAt(slider.Time + slider.Duration);
        var screenPosition = _playfieldTopLeft + position * playfieldScale;

        r.DrawTexture(_context.Skin.SliderEndCircle,
            screenPosition.X - drawSize / 2f, screenPosition.Y - drawSize / 2f,
            drawSize, drawSize, slider.Color with { W = opacity });

        r.DrawTexture(_context.Skin.SliderEndCircleOverlay,
            screenPosition.X - drawSize / 2f, screenPosition.Y - drawSize / 2f,
            drawSize, drawSize, new Color4<Rgba>(1, 1, 1, 1) with { W = opacity });
    }

    private void DrawSliderRepeatArrows(Renderer r, Slider slider, float playfieldScale, float opacity)
    {
        if (slider.SlideRepeatCount <= 1 || slider.SpanDuration <= 0 || slider.Points.Count < 2) return;

        var texture = _context.Skin.ReverseArrow;
        var scale = _baseCircleSize / 128f * texture.Scale;

        for (var i = 1; i < slider.SlideRepeatCount; i++)
        {
            var repeatTime = slider.Time + i * slider.SpanDuration;
            if (repeatTime >= slider.Time + slider.Duration) continue;

            var repeatPosition = slider.GetPositionAt(repeatTime);
            var sample = Math.Min(1.0, slider.SpanDuration * 0.05);
            var nextPosition = slider.GetPositionAt(repeatTime + sample);
            var direction = nextPosition - repeatPosition;

            if (direction.LengthSquared < 0.0001f)
            {
                var previousPosition = slider.GetPositionAt(repeatTime - sample);
                direction = repeatPosition - previousPosition;
            }

            if (direction.LengthSquared < 0.0001f) continue;

            var rotation = -MathHelper.RadiansToDegrees(MathF.Atan2(direction.Y, direction.X));
            var width = texture.Width * scale;
            var height = texture.Height * scale;
            var screenPosition = _playfieldTopLeft + repeatPosition * playfieldScale;

            r.DrawTexture(texture, screenPosition.X - width / 2f, screenPosition.Y - height / 2f,
                width, height, new Color4<Rgba>(1, 1, 1, 1) with { W = opacity }, rotation);
        }
    }

    private void DrawScoreString(Renderer r, string text, float x, float y, float scale, bool rightAlign)
    {
        var width = 0f;
        foreach (var c in text)
        {
            var texture = GetScoreTexture(c);
            if (texture != null)
                width += texture.Width * scale;
        }

        var cursorX = rightAlign ? x - width : x;

        foreach (var c in text)
        {
            var texture = GetScoreTexture(c);
            if (texture == null) continue;

            var w = texture.Width * scale;
            var h = texture.Height * scale;
            r.DrawTexture(texture, cursorX, y, w, h, new Color4<Rgba>(1, 1, 1, 1));
            cursorX += w;
        }
    }

    private Texture? GetScoreTexture(char c)
    {
        if (c is >= '0' and <= '9') return _context.Skin.ScoreNumbers[c - '0'];

        return c switch
        {
            '.' => _context.Skin.ScoreDot,
            '%' => _context.Skin.ScorePercent,
            'x' => _context.Skin.ScoreX,
            _ => null,
        };
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
        StopAllSliderSlides();

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
                    s.LastHeld = 0.0;
                    s.JudgementDone = false;
                    s.Sliding = false;
                    s.NextTickIndex = 0;
                    s.NextRepeatIndex = 1;
                    s.PartsHit = 0;
                    s.TickHits = 0;
                    s.TickMisses = 0;
                    s.RepeatHits = 0;
                    s.RepeatMisses = 0;
                    s.TailHit = false;
                }
            }
        }
    }

    public void SetBeatmap(Beatmap beatmap)
    {
        _beatmap = beatmap;
        StopAllSliderSlides();
        DisposeMapSamples();

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
        AudioManager.Instance.SampleSpeed = 1.00f;
        clock.Rate = 1.00f;

        _startingTimer = WaitingTime + _beatmap.AudioLeadIn;
        _musicStarted = false;

        _beatmap.CalculatePrepass();
        Player = new Player(_beatmap, this);
        SDL_ShowCursor();
        
        ResetObjectsAfter(0);

        _orderedObjects = _beatmap.HitObjects.OrderBy(h => h.Time).ToList();
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
        _currentScore = 0;
        _count300 = 0;
        _count100 = 0;
        _count50 = 0;
        _countMiss = 0;
        _health = 1.0f;

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
            StopAllSliderSlides();
            DisposeMapSamples();
        }
    }

    private void DisposeMapSamples()
    {
        foreach (var sample in _mapSamples.Values.Distinct())
            sample.Dispose();

        _mapSamples.Clear();
        _unloadableSamples.Clear();
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