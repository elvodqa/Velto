using System.Drawing;
using ManagedBass;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using SDL;
using Velto.Gameplay;

namespace Velto.Graphics;

public unsafe class GameDisplay : IDisposable
{
    private const int PLAYFIELD_W = 512;
    private const int PLAYFIELD_H = 384;
    private const double WAITINGTIME = 0f;
    private readonly Texture _approachCircleTexture;
    private Texture _backgroundTexture;
    private float _baseCircleSize;

    private Beatmap _beatmap;
    private readonly Texture _circleTexture;
    private int _colorCounter = 0;

    private readonly Texture _cursorTexture;
    private readonly Texture _cursorTrailTexture;
    private readonly Texture _hit0Texture;
    private readonly Texture _hitcircleOverlayTexture;
    private readonly Texture _hitcircleTexture;
    private bool _isPaused;
    private readonly MSDFFont _msdfFont;
    private int _musicChannel;
    private bool _musicStarted;
    private float _musicVolume;
    private readonly Dictionary<int, Texture> _numberTextures = new();
    private int _objectComboNumber = 1;

    private readonly Renderer _renderer;
    private readonly string _skinName = "rafis";
    private readonly Texture _sliderballTexture;
    private double _songCursor;
    private double _songLength;
    private IOrderedEnumerable<HitObject> _sortedObjects;
    private double _startingTimer;
    private readonly Texture _whiteTexture;
    private bool _isMenuOpen = false;

    private int _windowWidth, _windowHeight;
    private readonly BufferObject<uint> indexBuffer;

    private Vector2 lastPosition = Vector2.Zero;
    private Shader shader;

    private readonly Queue<TrailInfo> trails = new();
    private readonly VertexArrayObject<float, uint> vao;
    private readonly BufferObject<float> vertexBuffer;

    private List<BeatmapBox> _beatmapBoxes = new();
    private SliderPool _sliderPool;
    private int _hitSound;

    public GameDisplay(Renderer renderer)
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

        _whiteTexture = new Texture(Resources.GetPath("Resources/Textures/white.png"));
        _hitcircleTexture = new Texture(Resources.GetPath($"Resources/Textures/{_skinName}/hitcircle.png"));
        _hitcircleOverlayTexture =
            new Texture(Resources.GetPath($"Resources/Textures/{_skinName}/hitcircleoverlay.png"));
        _cursorTexture = new Texture(Resources.GetPath($"Resources/Textures/{_skinName}/cursor.png"));
        _cursorTrailTexture = new Texture(Resources.GetPath($"Resources/Textures/{_skinName}/cursortrail.png"));
        _approachCircleTexture = new Texture(Resources.GetPath($"Resources/Textures/{_skinName}/approachcircle.png"));
        _sliderballTexture = new Texture(Resources.GetPath($"Resources/Textures/{_skinName}/sliderb.png"));
        _hit0Texture = new Texture(Resources.GetPath($"Resources/Textures/{_skinName}/hit0.png"));
        _circleTexture = new Texture(Resources.GetPath("Resources/Textures/circle.png"));

        _msdfFont = MSDFFont.Load(Resources.GetPath("Resources/Fonts/arial/arial"));

        for (var i = 0; i < 10; i++)
            _numberTextures.Add(i, new Texture(Resources.GetPath($"Resources/Textures/{_skinName}/default-{i}.png")));

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
    }

    float playfieldWidth, playfieldHeight;
    private Vector2 playfieldTopLeft;

    public void Update(double delta)
    {
        var windowSizes = _renderer.WindowSizeInPixels;
        _windowWidth = (int)windowSizes.X;
        _windowHeight = (int)windowSizes.Y;

        var scale = playfieldWidth / PLAYFIELD_W;
        var osuRadius = 54.4f - 4.48f * _beatmap.CircleSize;
        _baseCircleSize = osuRadius * 2f * scale;

        // hitobjects
        var playfieldAspect = PLAYFIELD_W / (float)PLAYFIELD_H;
        var windowAspect = _windowWidth / (float)_windowHeight;

        if (windowAspect > playfieldAspect)
        {
            // window is wider → height is limiting (letterbox left/right)
            playfieldHeight = _windowHeight * 0.82f;
            playfieldWidth = playfieldHeight * playfieldAspect;
        }
        else
        {
            // window is taller → width is limiting (letterbox top/bottom)
            playfieldWidth = _windowWidth * 0.82f;
            playfieldHeight = playfieldWidth / playfieldAspect;
        }

        playfieldTopLeft = new(
            (_windowWidth - playfieldWidth) / 2f,
            (_windowHeight - playfieldHeight) / 2f
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

      

       


        RectangleF hitbox = new(0, _windowHeight - 40, _windowWidth, 40);
        if (Input.IsMouseDown(SDLButton.SDL_BUTTON_LEFT))
            // Console.WriteLine(hitbox);
            // Console.WriteLine($"{Input.MouseX} {Input.MouseY}");
            if (hitbox.Contains(Input.MouseX, Input.MouseY))
            {
                // Console.WriteLine("Mouse inside");
                var songPointer = Util.MapRange(Input.MouseX, 0, _windowWidth, 0, (float)_songLength);
                Bass.ChannelSetPosition(_musicChannel, Bass.ChannelSeconds2Bytes(_musicChannel, songPointer / 1000));

                foreach (var objects in _beatmap.HitObjects)
                {
                    objects.HitTime = 0;
                    objects.HitResult = HitResult.None;
                }
                
            }

        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_ESCAPE))
        {
            if (_isMenuOpen)
            {
                Bass.ChannelPlay(_musicChannel);
                _isMenuOpen = false;
                _isPaused = false;
                _beatmapBoxes.Clear();
            }
            else
            {
                if (_isPaused)
                    Bass.ChannelPlay(_musicChannel);
                else
                    Bass.ChannelPause(_musicChannel);
                Bass.ChannelPause(_musicChannel);
                _isPaused = true;
                _isMenuOpen = true;

                var height = 150;
                var gap = 2;
                var i = 0;

                var songDirs = Directory.GetDirectories(Resources.GetPath("Resources/Songs"));
                foreach (var dir in songDirs)
                {
                    var files = Directory.GetFiles(dir);
                    foreach (var file in files)
                    {
                        if (Path.GetExtension(file) == ".osu")
                        {
                            var box = new BeatmapBox();
                            box.Beatmap = new Beatmap(file);

                            box.Position = new Vector2(0, i * (height + gap));
                            box.Size = new Vector2(_windowWidth / 2f, height);

                            _beatmapBoxes.Add(box);

                            i++;
                        }
                    }
                }
            }
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
        }

        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_RIGHT))
        {
            _songCursor += 1000;
            Bass.ChannelSetPosition(_musicChannel, Bass.ChannelSeconds2Bytes(_musicChannel, _songCursor / 1000));
        }

        if (Math.Abs(Input.WheelY) > 0.01f)
        {
            _musicVolume += Input.WheelY * 0.01f;
            _musicVolume = Math.Clamp(_musicVolume, 0, 1);
            Bass.ChannelSetAttribute(_musicChannel, ChannelAttribute.Volume, _musicVolume);
        }

        if (_isMenuOpen)
        {
            foreach (var box in _beatmapBoxes)
            {
                box.IsHovered = false;
                RectangleF collision = new(box.Position.X, box.Position.Y, box.Size.X, box.Size.Y);
                if (collision.Contains(Input.MouseX, Input.MouseY))
                {
                    box.IsHovered = true;
                    if (Input.IsMouseJustPressed(SDLButton.SDL_BUTTON_LEFT))
                    {
                        _beatmap = box.Beatmap;

                        Bass.StreamFree(_musicChannel);
                        _musicChannel = Bass.CreateStream(Path.Combine(_beatmap.Folder, _beatmap.AudioFilename));
                        Bass.ChannelSetAttribute(_musicChannel, ChannelAttribute.Volume, _musicVolume);

                        _songLength = 1000 *
                                      Bass.ChannelBytes2Seconds(_musicChannel, Bass.ChannelGetLength(_musicChannel));

                        _startingTimer = WAITINGTIME + _beatmap.AudioLeadIn;

                        _beatmap.CalculatePrepass(_renderer.Window);

                        _isPaused = false;
                        _isMenuOpen = false;

                        _backgroundTexture.Dispose();
                        _backgroundTexture = new Texture(Path.Combine(_beatmap.Folder, _beatmap.BackgroundFile));
                        Bass.ChannelPlay(_musicChannel);
                    }
                }
            }
        }

        // Handle objects bkz: https://osu.ppy.sh/wiki/en/Gameplay/Judgement/osu%21 
        foreach (var hitObject in _beatmap.HitObjects)
        {
            if (hitObject is HitCircle circle)
            {
                if (hitObject.HitResult == HitResult.None)
                {
                    if (_songCursor - 150 >= circle.Time)
                    {
                        circle.HitResult = HitResult.Miss;
                        circle.Failed = true;
                    }

                    var mouse = new Vector2(Input.MouseX, Input.MouseY);
                    // convert screen → playfield
                    mouse.X = (mouse.X - playfieldTopLeft.X) / scale;
                    mouse.Y = (mouse.Y - playfieldTopLeft.Y) / scale;
                    var radiusPlayfield = osuRadius;
                    if (Vector2.Distance(circle.Position, mouse) <= radiusPlayfield)
                        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_Z) ||
                            Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_X))
                        {
                            //Bass.ChannelPlay(Bass.SampleGetChannel(_hitSound));
                            circle.Color = Vector4.Zero;
                            circle.HitResult = HitResult.Ok;
                            circle.HitTime = _songCursor;
                        }

                    if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_Z) ||
                        Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_X))
                    {
                        break; // object block
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
    }
    
    float GetSliderVelocityMultiplier(double time)
    {
        var tp = _beatmap.TimingPoints
            .Where(t => t.Time <= time)
            .LastOrDefault();

        if (tp.Uninherited == 1)
            return 1f;

        return (float)(-100.0 / tp.BeatLength);
    }

    public void Draw(double delta)
    {
        
        
        //Bass.ChannelSetAttribute(_musicChannel, ChannelAttribute.Volume, 0.02f);
        Bass.ChannelGetAttribute(_musicChannel, ChannelAttribute.Volume, out var volume);

        var mousePos = new Vector2(Input.MouseX, Input.MouseY);

        if (lastPosition != Vector2.Zero && Vector2.Distance(mousePos, lastPosition) > 5)
            trails.Enqueue(new TrailInfo
            {
                Position = lastPosition,
                Life = 50
            });

        if (trails.Count > 48*2)
            trails.Dequeue();

        _renderer.Clear(new Vector4(0, 0, 0, 1));

        // bg
        if (_windowWidth * ((float)_backgroundTexture.Height / _backgroundTexture.Width) > _windowHeight)
        {
            // sağ sol bar
            var padding = _windowWidth -
                          _windowHeight * (_backgroundTexture.Width / (float)_backgroundTexture.Height);
            _renderer.DrawTexture(_backgroundTexture, padding / 2, 0,
                _windowHeight * ((float)_backgroundTexture.Width / _backgroundTexture.Height), _windowHeight,
                new Vector4(1, 1, 1, 1));
        }
        else
        {
            // üst alt bar
            var padding = _windowHeight - _windowWidth * (_backgroundTexture.Height /
                                                          (float)_backgroundTexture.Width);
            _renderer.DrawTexture(_backgroundTexture, 0, padding / 2, _windowWidth,
                _windowWidth * ((float)_backgroundTexture.Height / _backgroundTexture.Width), new Vector4(1, 1, 1, 1));
        }

        // bg dim
        _renderer.DrawRectangle(0, 0, _windowWidth, _windowHeight, new Vector4(0, 0, 0, 0.55f));


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
                drawSize = (float)(objectCircleSize + objectCircleSize * (1 - fadeout) * 0.1);
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
            
            

            if (hitObject is HitCircle circle)
            {
                if (hitObject.Time + _startingTimer - (int)_songCursor > -_beatmap.Posttime &&
                    hitObject.Time + _startingTimer - (int)_songCursor < _beatmap.Preempt)
                {
                    if (hitObject.HitResult != HitResult.None) continue;
                    _renderer.DrawTexture(_approachCircleTexture,
                        posX - approachCircleSize / 2,
                        posY - approachCircleSize / 2,
                        approachCircleSize,
                        approachCircleSize, hitObject.Color with
                        {
                            W = Math.Min(fadein, fadeout)
                        });

                    _renderer.DrawTexture(_hitcircleTexture,
                        posX - drawSize / 2,
                        posY - drawSize / 2,
                        drawSize,
                        drawSize, hitObject.Color with { W = Math.Min(fadein, fadeout) });

                    _renderer.DrawTexture(_hitcircleOverlayTexture,
                        posX - drawSize / 2,
                        posY - drawSize / 2,
                        drawSize,
                        drawSize, new Vector4(1, 1, 1, 1) with { W = Math.Min(fadein, fadeout) });

                    var num = hitObject.ComboNumber.ToString();

                    var digitScale = drawSize / 128f; // adjust baseline size
                    float digitWidthTotal = 0;

                    // first pass: compute total width
                    foreach (var c in num)
                    {
                        var digit = c - '0';
                        var tex = _numberTextures[digit];
                        digitWidthTotal += tex.Width * digitScale;
                    }

                    // start centered
                    var cursorX = posX - digitWidthTotal / 2;

                    foreach (var c in num)
                    {
                        var digit = c - '0';
                        var tex = _numberTextures[digit];

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
                    
                    var _drawSize = drawSize * 0.92f;
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
                    var _drawSize = drawSize * 0.8f;
                    
                    _renderer.DrawTexture(_circleTexture,
                        scaledX - _drawSize / 2,
                        scaledY - _drawSize / 2,
                        _drawSize,
                        _drawSize,
                        hitObject.Color with { W = Math.Min(sliderFadein, sliderFadeout) });
                }
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

                //_renderer.DrawSlider(slider, posX, posY, 1, _baseCircleSize, fadein, fadeout);

                _renderer.DrawTexture(_approachCircleTexture,
                    posX - approachCircleSize / 2,
                    posY - approachCircleSize / 2,
                    approachCircleSize,
                    approachCircleSize, hitObject.Color with { W = Math.Min(fadein, fadeout) });

                _renderer.DrawTexture(_hitcircleTexture,
                    posX - drawSize / 2,
                    posY - drawSize / 2,
                    drawSize,
                    drawSize, hitObject.Color with { W = Math.Min(fadein, fadeout) });

                _renderer.DrawTexture(_hitcircleOverlayTexture,
                    posX - drawSize / 2,
                    posY - drawSize / 2,
                    drawSize,
                    drawSize, new Vector4(1, 1, 1, 1) with { W = Math.Min(fadein, fadeout) });

                var num = hitObject.ComboNumber.ToString();

                var digitScale = drawSize / 128f; // adjust baseline size
                float digitWidthTotal = 0;

                // first pass: compute total width
                foreach (var c in num)
                {
                    var digit = c - '0';
                    var tex = _numberTextures[digit];
                    digitWidthTotal += tex.Width * digitScale;
                }

                // start centered
                var cursorX = posX - digitWidthTotal / 2;

                foreach (var c in num)
                {
                    var digit = c - '0';
                    var tex = _numberTextures[digit];

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
        //Console.Write($"\n");


        var yellow = new Vector4(242 / 255f, 191 / 255f, 36 / 255f, 1);

        // volume control
        _renderer.DrawRectangle(0, (float)_windowHeight / 2 - 150, 40, 300, new Vector4(0.1f, 0.1f, 0.1f, 1));

        var length = Util.MapRange(_musicVolume, 0, 1, 0, 300);
        _renderer.DrawRectangle(10, (float)_windowHeight / 2 - 150, 20, length, yellow);
        
        
        // timeline
        var songPointer = Util.MapRange((float)_songCursor, 0, (float)_songLength, 0, _windowWidth);
        _renderer.DrawRectangle(0, _windowHeight - 20, songPointer, 20, yellow);
        var cursorSize = new Vector2(30, 60);
        _renderer.DrawRectangle(songPointer - cursorSize.X/ 2, _windowHeight-cursorSize.Y, cursorSize.X, cursorSize.Y, new Vector4(1, 1, 1, 1));
        

        // TODO: input overlay

        

        // menu
        if (_isMenuOpen)
        {
            _renderer.DrawRectangle(0, 0, _windowWidth / 2f, _windowHeight, new Vector4(0.1f, 0.1f, 0.1f, 0.8f));
            foreach (var box in _beatmapBoxes)
            {
                var color = new Vector4(0.3f, 0.3f, 0.3f, 0.5f);
                if (box.IsHovered)
                {
                    color = new Vector4(0.6f, 0.6f, 0.6f, 0.5f);
                }

                _renderer.DrawRectangle(box.Position.X, box.Position.Y, box.Size.X, box.Size.Y, color);
                _renderer.DrawText(_msdfFont, $"{box.Beatmap.Artist} - {box.Beatmap.Title}",
                    new Vector2(box.Position.X + 25, box.Position.Y + 25),
                    0.5f, new Vector4(1, 1, 1, 1));
                _renderer.DrawText(_msdfFont, $"By: {box.Beatmap.Creator} | Difficulty: {box.Beatmap.Version}",
                    new Vector2(box.Position.X + 25, box.Position.Y + 65),
                    0.5f, new Vector4(1, 1, 1, 1));
            }
        }
        
        
        // cursor
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
        lastPosition = mousePos;

        if (trailSnapshot.Length > 0)
        {
            var i = trailSnapshot.Length;
            for (var idx = trailSnapshot.Length - 1; idx >= 0; idx--)
            {
                var trail = trailSnapshot[idx];
                size -= 1;

                var alpha = (float)i / trailSnapshot.Length;
                _renderer.DrawTexture(_cursorTrailTexture,
                    trail.Position.X - size / 2,
                    trail.Position.Y - size / 2,
                    size, size, new Vector4(1, 1, 1, 1) * alpha);
                i--;
            }
        }
        
        size = _baseCircleSize / 2f; // _cursorTexture.Width * 1.5f;
        _renderer.DrawTexture(_cursorTexture,
            Input.MouseX - size / 2,
            Input.MouseY - size / 2,
            size, size, new Vector4(1, 1, 1, 1));
    }
    
    public TimingPoint GetCurrentTimingPoint(double time)
    {
        var list = _beatmap.TimingPoints;

        int index = list.BinarySearch(
            new TimingPoint { Time = time },
            Comparer<TimingPoint>.Create((a, b) => a.Time.CompareTo(b.Time))
        );

        if (index < 0) index = ~index;

        return index == 0 ? list[0] : list[index - 1];
    }
    
    public TimingPoint GetNextTimingPoint(double time)
    {
        var list = _beatmap.TimingPoints;

        int index = list.BinarySearch(
            new TimingPoint { Time = time },
            Comparer<TimingPoint>.Create((a, b) => a.Time.CompareTo(b.Time))
        );

        if (index < 0) index = ~index;

        return index >= list.Count ? list[^1] : list[index];
    }

    public void Dispose()
    {
        //Bass.Stop();
        Bass.ChannelStop(_musicChannel);
        Bass.StreamFree(_musicChannel);

        vertexBuffer.Dispose();
        indexBuffer.Dispose();
        vao.Dispose();
        _cursorTexture.Dispose();
        _cursorTrailTexture.Dispose();
        _whiteTexture.Dispose();
        _backgroundTexture.Dispose();
        _hitcircleTexture.Dispose();
        _hitcircleOverlayTexture.Dispose();
        _approachCircleTexture.Dispose();
        _sliderballTexture.Dispose();
        _circleTexture.Dispose();
        _hit0Texture.Dispose();
        foreach (var pair in _numberTextures) pair.Value.Dispose();
    }

    public class SliderPool
    {
        public struct SliderVertex
        {
            public Vector3 Position;
            public Vector2 UV;
            public float Progress; // 0..1 along slider
        }


        private uint _capacity;
        private Renderer _renderer;
        private Queue<Slider> _sliderQueue = new();

        public SliderPool(Renderer renderer, uint capacity = 100)
        {
            _renderer = renderer;
            _capacity = 100;
        }

        public void QueueSlider(Slider slider)
        {
            if (!_sliderQueue.Contains(slider)) _sliderQueue.Enqueue(slider);
        }


        // ball Vector2 pos = slider.GetPointAt(progress);
        public void Update(double songCursor, float osuRadius)
        {
            /*foreach (var pair in _framebuffers)
            {
                if (pair.Key.Time < songCursor - 15000)
                {
                    _framebuffers[pair.Key].Dispose();
                    _framebuffers.Remove(pair.Key);
                }
            }*/

            if (_sliderQueue.TryPeek(out Slider slider))
            {
                float minX = slider.Points.Min(p => p.X);
                float maxX = slider.Points.Max(p => p.X);
                float minY = slider.Points.Min(p => p.Y);
                float maxY = slider.Points.Max(p => p.Y);

                float radius = osuRadius;
                minX -= radius;
                maxX += radius;
                minY -= radius;
                maxY += radius;
                maxY += radius;

                slider.CacheOffset = new Vector2(minX, minY);
                float w = (float)Math.Ceiling(maxX - minX);
                float h = (float)Math.Ceiling(maxY - minY);

                var (vboData, iboData) = BuildSliderMesh(slider.Points, radius);
                slider.Vbo = new BufferObject<float>(vboData, BufferTarget.ArrayBuffer, BufferUsage.DynamicDraw);
                slider.Ebo = new BufferObject<uint>(iboData, BufferTarget.ElementArrayBuffer, BufferUsage.DynamicDraw);
                slider.Vao = new VertexArrayObject<float, uint>(slider.Vbo, slider.Ebo);

                slider.IndexCount = iboData.Length;

                slider.Vao.VertexAttributePointer(0, 3, VertexAttribPointerType.Float, 6 * sizeof(float),
                    0); // position
                slider.Vao.VertexAttributePointer(1, 2, VertexAttribPointerType.Float, 6 * sizeof(float),
                    3 * sizeof(float)); // uv
                slider.Vao.VertexAttributePointer(2, 1, VertexAttribPointerType.Float, 6 * sizeof(float),
                    5 * sizeof(float)); // progress

                slider.SliderFramebuffer = new(_renderer, (int)w, (int)h);

                _renderer.FixFramebuffer();
                _sliderQueue.Dequeue();
            }
        }

        public static (float[] vbo, uint[] ibo) BuildSliderMesh(
            List<Vector2> points,
            float radius)
        {
            var vertices = new List<SliderVertex>();
            var indices = new List<uint>();

            float totalLength = 0f;
            float[] segLen = new float[points.Count];

            for (int i = 1; i < points.Count; i++)
            {
                totalLength += Vector2.Distance(points[i - 1], points[i]);
                segLen[i] = totalLength;
            }

            for (int i = 0; i < points.Count; i++)
            {
                Vector2 prev = points[Math.Max(0, i - 1)];
                Vector2 curr = points[i];
                Vector2 next = points[Math.Min(points.Count - 1, i + 1)];

                var d1 = curr - prev;
                var d2 = next - curr;

                if (d1.LengthSquared < 0.0001f) d1 = d2;
                if (d2.LengthSquared < 0.0001f) d2 = d1;

                d1 = d1.LengthSquared < 0.0001f ? Vector2.UnitX : Vector2.Normalize(d1);
                d2 = d2.LengthSquared < 0.0001f ? Vector2.UnitX : Vector2.Normalize(d2);

                var dir = d1 + d2;
                dir = dir.LengthSquared < 0.0001f ? d2 : Vector2.Normalize(dir);

                Vector2 normal = new Vector2(-dir.Y, dir.X);

                float t = segLen[i] / Math.Max(1, totalLength);

                Vector2 left = curr + normal * radius;
                Vector2 right = curr - normal * radius;

                vertices.Add(new SliderVertex
                {
                    Position = new Vector3(left.X, left.Y, 0),
                    UV = new Vector2(0, t),
                    Progress = t
                });

                vertices.Add(new SliderVertex
                {
                    Position = new Vector3(right.X, right.Y, 0),
                    UV = new Vector2(1, t),
                    Progress = t
                });
            }

            for (int i = 0; i < points.Count - 1; i++)
            {
                uint i0 = (uint)(i * 2);
                uint i1 = i0 + 1;
                uint i2 = i0 + 2;
                uint i3 = i0 + 3;

                indices.Add(i0);
                indices.Add(i2);
                indices.Add(i1);

                indices.Add(i1);
                indices.Add(i2);
                indices.Add(i3);
            }

            return (
                vertices.SelectMany(v => new float[]
                {
                    v.Position.X, v.Position.Y, v.Position.Z,
                    v.UV.X, v.UV.Y,
                    v.Progress
                }).ToArray(),
                indices.ToArray()
            );
        }

        public void Drain()
        {
            /*foreach (var pair in _framebuffers)
            {
                _framebuffers[pair.Key].Dispose();
                _framebuffers.Remove(pair.Key);
            }*/
            _sliderQueue.Clear();
        }
    }

    public class TrailInfo
    {
        public float Life = 50;
        public Vector2 Position;
    }

    private readonly float[] vertices =
    [
        // position         // uv
        0f, 0f, 0f, 0f, 1f, // top-left
        1f, 0f, 0f, 1f, 1f, // top-right
        0f, 1f, 0f, 0f, 0f, // bottom-left
        1f, 1f, 0f, 1f, 0f // bottom-right
    ];

    public class BeatmapBox
    {
        public Beatmap Beatmap;
        public Vector2 Position;
        public Vector2 Size;
        public bool IsHovered = false;
    }

    private readonly uint[] indices =
    [
        0, 2, 1, // first triangle
        1, 2, 3 // second triangle
    ];
}