using System.Drawing;
using ManagedBass;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using Velto.Gameplay;
using SDL;
using Velto.Core;
using Velto.Graphics;
using static SDL.SDL3;

namespace Velto.Views;

public unsafe class GameView : View
{
    private const int PLAYFIELD_W = 512;
    private const int PLAYFIELD_H = 384;
    private const double WAITINGTIME = 0f;
    
    private Texture _backgroundTexture;
    private float _baseCircleSize;

    private Beatmap _beatmap;
    private readonly Texture _circleTexture;
    private int _colorCounter = 0;

    
    private bool _isPaused;
    private readonly MSDFFont _msdfFont;
    private int _musicChannel;
    private bool _musicStarted;
    private float _musicVolume;
  
    private int _objectComboNumber = 1;

    private readonly Renderer _renderer;
    private readonly string _skinName = "rafis";
    
    private double _songCursor;
    private double _songLength;
    private IOrderedEnumerable<HitObject> _sortedObjects;
    private double _startingTimer;
    private bool _isMenuOpen = false;
    
    private readonly BufferObject<uint> indexBuffer;

    private Vector2 lastCursorPosition = Vector2.Zero;
    private Shader shader;

    private readonly Queue<TrailInfo> trails = new();
    private readonly VertexArrayObject<float, uint> vao;
    private readonly BufferObject<float> vertexBuffer;

    
    private SliderPool _sliderPool;
    private int _hitSound;

    private Player _player;
    private SettingsView _settingsView;
    private SongSelectorView _songSelectorView;
    private InputOverlayView _inputOverlayView;
    public Skin Skin;

    public GameView(Renderer renderer)
    {
        _renderer = renderer;

        vertexBuffer = new BufferObject<float>(vertices, BufferTarget.ArrayBuffer, BufferUsage.StaticDraw);
        indexBuffer = new BufferObject<uint>(indices, BufferTarget.ElementArrayBuffer, BufferUsage.StaticDraw);
        vao = new VertexArrayObject<float, uint>(vertexBuffer, indexBuffer);
        vao.VertexAttributePointer(0, 3, VertexAttribPointerType.Float, 5 * sizeof(float), 0);
        vao.VertexAttributePointer(1, 2, VertexAttribPointerType.Float, 5 * sizeof(float), 3 * sizeof(float));
        shader = new Shader("sprite"); // pippidonclear0

        //_beatmap = new(Resources.GetPath("Resources/Songs/lotus/Susumu Hirasawa - SWITCHED-ON LOTUS (Starrodkirby86) [KIRBY Mix Deluxe].osu"));
        _beatmap = new Beatmap(Resources.GetPath(
            "Resources/Songs/Wakeshima Kanon/ASCA - Nisemono no Koi ni Sayounara with Wakeshima Kanon (timemon) [Kyou's Extra].osu"));
        //_beatmap = new(Resources.GetPath("Resources/Songs/Centipede/Knife Party - Centipede (Sugoi-_-Desu) [This isn't a map, just a simple visualisation].osu"));
        //_beatmap = new(Resources.GetPath("Resources/Songs/exit/Camellia - Exit This Earth's Atomosphere (Camellia's ''PLANETARY200STEP'' Remix) (ProfessionalBox) [Primordial Nucleosynthesis].osu"));

        _backgroundTexture = new Texture(Path.Combine(_beatmap.Folder, _beatmap.BackgroundFile));
        _circleTexture = new Texture(Resources.GetPath("Resources/Textures/circle.png"));
        _msdfFont = MSDFFont.Load(Resources.GetPath("Resources/Fonts/arial/arial"));
        
        Bass.Init();
        
        _hitSound = Bass.SampleLoad($"Resources/Textures/{_skinName}/normal-hitnormal.ogg", 0, 0, 16, BassFlags.Default);
        
        //Console.WriteLine(_beatmap.AudioFilename);
        _musicChannel = Bass.CreateStream(Path.Combine(_beatmap.Folder, _beatmap.AudioFilename));
        Bass.ChannelSetAttribute(_musicChannel, ChannelAttribute.Volume, 0.02f);
        _musicVolume = 0.02f;
        _songLength = 1000 * Bass.ChannelBytes2Seconds(_musicChannel, Bass.ChannelGetLength(_musicChannel));

        _startingTimer = WAITINGTIME + _beatmap.AudioLeadIn;

        _beatmap.CalculatePrepass(_renderer.Window);

        _sliderPool = new SliderPool(_renderer);

        _player = new Player(_beatmap);

        _settingsView = new(_renderer);
        _songSelectorView = new(_renderer, this);
        _inputOverlayView = new(_renderer, this, _msdfFont);
        _inputOverlayView.SetPlayer(_player);

        Skin = new Skin(Resources.GetPath("Resources/Textures/default"));
    }

    float playfieldWidth, playfieldHeight;
    private Vector2 playfieldTopLeft;

    public override void Update(double delta)
    {
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


        _startingTimer -= delta;
        if (_startingTimer <= 0 && !_musicStarted)
        {
            Bass.ChannelPlay(_musicChannel);
            //Bass.ChannelSetPosition(_musicChannel, Bass.ChannelSeconds2Bytes(_musicChannel, 60));
            _musicStarted = true;
        }

        if (_musicStarted) _startingTimer = 0;

        var pos = Bass.ChannelBytes2Seconds(_musicChannel, Bass.ChannelGetPosition(_musicChannel));
        // var err = Bass.LastError;
        // if (err != Errors.OK)
        // {
        //     Console.WriteLine(err);
        // }
        _songCursor = (ulong)(pos * 1000);

        _sortedObjects = _beatmap.HitObjects
            .OrderByDescending(h => Math.Abs(h.Time - _songCursor));
        
        double cursor = _songCursor;

        
        RectangleF hitbox = new(0, Height - 40, Width, 40);
        if (Input.IsMouseDown(SDLButton.SDL_BUTTON_LEFT))
            // Console.WriteLine(hitbox);
            // Console.WriteLine($"{Input.MouseX} {Input.MouseY}");
            if (hitbox.Contains(Input.MouseX, Input.MouseY))
            {
                // Console.WriteLine("Mouse inside");
                var songPointer = Util.MapRange(Input.MouseX, 0, Width, 0, (float)_songLength);
                Bass.ChannelSetPosition(_musicChannel, Bass.ChannelSeconds2Bytes(_musicChannel, songPointer / 1000));
                
                ResetObjectsAfter(_songCursor);
                _sortedObjects = _beatmap.HitObjects
                    .OrderByDescending(h => Math.Abs(h.Time - _songCursor));
                
            }
        
        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_SPACE) && !_isMenuOpen)
        {
            if (_isPaused)
                Bass.ChannelPlay(_musicChannel);
            else
                Bass.ChannelPause(_musicChannel);

            _isPaused = !_isPaused;
        }

        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_LEFT))
        {
            _songCursor -= 1000;
            Bass.ChannelSetPosition(_musicChannel, Bass.ChannelSeconds2Bytes(_musicChannel, _songCursor / 1000));
            
            ResetObjectsAfter(_songCursor);

            // _sortedObjects = _beatmap.HitObjects
            //     .OrderByDescending(h => Math.Abs(h.Time - _songCursor));
        }

        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_RIGHT))
        {
            _songCursor += 1000;
            Bass.ChannelSetPosition(_musicChannel, Bass.ChannelSeconds2Bytes(_musicChannel, _songCursor / 1000));
            
            ResetObjectsAfter(_songCursor);

            // _sortedObjects = _beatmap.HitObjects
            //     .OrderByDescending(h => Math.Abs(h.Time - _songCursor));
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
                Bass.ChannelSetAttribute(_musicChannel, ChannelAttribute.Volume, _musicVolume);
            }

            if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_O))
            {
                _settingsView.Toggle();
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

                    // var mouse = new Vector2(Input.MouseX, Input.MouseY);
                    // // convert screen → playfield
                    // mouse.X = (mouse.X - playfieldTopLeft.X) / scale;
                    // mouse.Y = (mouse.Y - playfieldTopLeft.Y) / scale;
                    var playerCursor = _player.Cursor;
                    var radiusPlayfield = osuRadius;
                    var circlePosition = playfieldTopLeft + circle.Position * scale;
                    if (Vector2.Distance(circlePosition, playerCursor) <= radiusPlayfield)
                        if (_player.ActionPrimaryPressed || _player.ActionSecondaryPressed)
                        {
                            // bkz: https://osu.ppy.sh/wiki/en/Gameplay/Judgement/osu%21
                            //Bass.ChannelPlay(Bass.SampleGetChannel(_hitSound));
                            //circle.Color = Vector4.Zero;
                            hitObject.HitTime = _songCursor;
                            var difference = Math.Abs(_songCursor - hitObject.HitTime);
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
                        }
                    
                    // Noteblock
                    if (_player.ActionPrimaryPressed || _player.ActionSecondaryPressed)
                    {
                        break; 
                    }
                }
                else
                {
                }
            }
            if (hitObject is Slider slider)
            {
                // Queue before it becomes visible so the cached texture is ready.
                if (slider.SliderFramebuffer == null && _songCursor >= slider.Time - _beatmap.Preempt)
                    _sliderPool.QueueSlider(slider);
            }
        }

    
        // Slider points are in osu! playfield coordinates, so cache/build using osu-radius (unscaled).
        _sliderPool.Update(_songCursor, osuRadius);
        
        if (lastCursorPosition != Vector2.Zero && Vector2.Distance(_player.Cursor, lastCursorPosition) > 5)
            trails.Enqueue(new TrailInfo
            {
                Position = lastCursorPosition,
                Life = 50
            });

        if (trails.Count > 48*2)
            trails.Dequeue();

        foreach (var particle in _hitResultParticles.ToList())
        {
            //particle.Life -= (float)delta;
            if (_songCursor > particle.Begin + particle.Duration)
            {
                _hitResultParticles.Remove(particle);
            }
        }
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
                fadein = 0;
                fadeout = 0;
                approachCircleSize = 0;
                drawSize = 0;
            }
            
            
            // Gameplay drawing
            if (hitObject is HitCircle circle)
            {
                if (hitObject.Time + _startingTimer - (int)_songCursor > -_beatmap.Posttime &&
                    hitObject.Time + _startingTimer - (int)_songCursor < _beatmap.Preempt)
                {
                    if (hitObject.HitResult != HitResult.None) continue;
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
                if (!(hitObject.Time + _startingTimer - (int)_songCursor > - slider.Duration) ||
                    !(hitObject.Time + _startingTimer - (int)_songCursor < _beatmap.Preempt)) continue;

                slider.Sliding =
                    _songCursor >= slider.Time &&
                    _songCursor <= slider.Time + slider.Duration;
                
                var sliderFadein =
                    Math.Clamp(
                        Util.MapRange((float)_songCursor, hitObject.Time - _beatmap.Preempt,
                            hitObject.Time - _beatmap.Preempt / 3,
                            0, 1), 0, 1);
                var sliderFadeout =
                    Math.Clamp(Util.MapRange((float)_songCursor, hitObject.Time, (float)(hitObject.Time + slider.Duration), 1, 0),
                        0, 1);
                
                GL.BlendFunc(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
                foreach (var point in slider.Points)
                {
                    var scaledX = playfieldTopLeft.X + point.X * scale;
                    var scaledY = playfieldTopLeft.Y + point.Y * scale;
                    
                    var _drawSize = objectCircleSize * 0.92f;
                    _renderer.DrawTexture(_circleTexture,
                        scaledX - _drawSize / 2,
                        scaledY - _drawSize / 2,
                        _drawSize,
                        _drawSize,
                        new Vector4(1, 1, 1, 1) with { W = Math.Min(sliderFadein, sliderFadeout) });
                }
                foreach (var point in slider.Points)
                {
                    var scaledX = playfieldTopLeft.X + point.X * scale;
                    var scaledY = playfieldTopLeft.Y + point.Y * scale;
                    var _drawSize = objectCircleSize * 0.8f;
                    
                    _renderer.DrawTexture(_circleTexture,
                        scaledX - _drawSize / 2,
                        scaledY - _drawSize / 2,
                        _drawSize,
                        _drawSize,
                        // hitObject.Color with { W = Math.Min(sliderFadein, sliderFadeout) });
                        new Vector4(0.1f,0.1f, 0.1f, 1) with { W = Math.Min(sliderFadein, sliderFadeout) });
                }
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

                if (slider.Sliding)
                {
                    var position = slider.GetPositionAt(_songCursor);

                    var scaledX = playfieldTopLeft.X + position.X * scale;
                    var scaledY = playfieldTopLeft.Y + position.Y * scale;
                    var _drawSize = objectCircleSize * 1f;

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
                            hitObject.Color with { W = 1 });
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
                    
                    _drawSize = objectCircleSize * 2;
                    _renderer.DrawTexture(Skin.SliderFollowCircle,
                        scaledX - _drawSize /2,
                        scaledY - _drawSize /2,
                        _drawSize,
                        _drawSize,
                        new Vector4(1, 1, 1, 1) with { W = 1});
                    
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
            var h = texture.Height * (w/texture.Width);
            
            _renderer.DrawCenteredTexture(texture,
                new Vector2(posX, posY),
                w,
                h, new Vector4(1, 1, 1, 1) with { W = alpha });
        }
       
        _renderer.DrawText(_msdfFont, $"{_player.Cursor.X.ToString("0000")}x{_player.Cursor.Y.ToString("0000")}", new Vector2(10, 600), 1, new Vector4(1, 1, 1, 1));
        
        var yellow = new Vector4(242 / 255f, 191 / 255f, 36 / 255f, 1);

        // volume control
        _renderer.DrawRectangle(0, (float)Height / 2 - 150, 40, 300, new Vector4(0.1f, 0.1f, 0.1f, 1));

        var length = Util.MapRange(_musicVolume, 0, 1, 0, 300);
        _renderer.DrawRectangle(10, (float)Height / 2 - 150, 20, length, yellow);
        
        
        // timeline
        var songPointer = Util.MapRange((float)_songCursor, 0, (float)_songLength, 0, Width);
        _renderer.DrawRectangle(0, Height - 20, songPointer, 20, yellow);
        var cursorSize = new Vector2(30, 60);
        _renderer.DrawRectangle(songPointer - cursorSize.X/ 2, Height-cursorSize.Y, cursorSize.X, cursorSize.Y, new Vector4(1, 1, 1, 1));

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
        
        _songSelectorView.Draw(delta);
        _settingsView.Draw(delta);
        _renderer.SetScissor(0, 0, (int)Width, (int)Height);
    }

    public void SetBeatmap(Beatmap beatmap)
    {
        _beatmap = beatmap;

        Bass.StreamFree(_musicChannel);
        _musicChannel = Bass.CreateStream(Path.Combine(_beatmap.Folder, _beatmap.AudioFilename));
        Bass.ChannelSetAttribute(_musicChannel, ChannelAttribute.Volume, _musicVolume);

        _songLength = 1000 *
                      Bass.ChannelBytes2Seconds(_musicChannel, Bass.ChannelGetLength(_musicChannel));

        _startingTimer = WAITINGTIME + _beatmap.AudioLeadIn;

        _beatmap.CalculatePrepass(_renderer.Window);
        _player = new Player(_beatmap);
        _inputOverlayView.SetPlayer(_player);
        _inputOverlayView.Reset();
        
        ResetObjectsAfter(0);
        
        _sortedObjects = _beatmap.HitObjects
            .OrderByDescending(h => Math.Abs(h.Time - _songCursor));

        _backgroundTexture.Dispose();
        _backgroundTexture = new Texture(Path.Combine(_beatmap.Folder, _beatmap.BackgroundFile));
        Bass.ChannelPlay(_musicChannel);
        Logger.Instance.Info($"Beatmap set to {beatmap}");
    }
    
    public void Dispose()
    {
        //Bass.Stop();
        Bass.ChannelStop(_musicChannel);
        Bass.StreamFree(_musicChannel);

        vertexBuffer.Dispose();
        indexBuffer.Dispose();
        vao.Dispose();
        _backgroundTexture.Dispose();
        
        _circleTexture.Dispose();
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
   
    
    public class HitResultParticle
    {
        public Vector2 Position;
        public HitResult Result;
        public double Begin; // starting offset in ms
        public double Appear; // time it takes to fully be opaque
        public double Duration; // the time it takes to die
    }

    private readonly float[] vertices =
    [
        // position         // uv
        0f, 0f, 0f, 0f, 1f, // top-left
        1f, 0f, 0f, 1f, 1f, // top-right
        0f, 1f, 0f, 0f, 0f, // bottom-left
        1f, 1f, 0f, 1f, 0f // bottom-right
    ];
    
    private readonly uint[] indices =
    [
        0, 2, 1, // first triangle
        1, 2, 3 // second triangle
    ];
}
