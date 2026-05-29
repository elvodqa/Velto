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
    private readonly Texture _backgroundTexture;
    private float _baseCircleSize;

    private readonly Beatmap _beatmap;
    private readonly Texture _circleTexture;
    private int _colorCounter = 0;

    private readonly Texture _cursorTexture;
    private readonly Texture _cursorTrailTexture;
    private readonly Texture _hit0Texture;
    private readonly Texture _hitcircleOverlayTexture;
    private readonly Texture _hitcircleTexture;
    private bool _isPaused;
    private readonly MSDFFont _msdfFont;
    private readonly int _musicChannel;
    private bool _musicStarted;
    private float _musicVolume;
    private readonly Dictionary<int, Texture> _numberTextures = new();
    private int _objectComboNumber = 1;

    private readonly Renderer _renderer;
    private readonly string _skinName = "rafis";
    private readonly Texture _sliderballTexture;
    private double _songCursor;
    private readonly double _songLength;
    private IOrderedEnumerable<HitObject> _sortedObjects;
    private double _startingTimer;
    private readonly Texture _whiteTexture;

    private SDL_Window* _window;

    private int _windowWidth, _windowHeight;
    private readonly BufferObject<uint> indexBuffer;

    private readonly uint[] indices =
    [
        0, 2, 1, // first triangle
        1, 2, 3 // second triangle
    ];

    private Vector2 lastPosition = Vector2.Zero;
    private Shader shader;

    private readonly Queue<TrailInfo> trails = new();
    private readonly VertexArrayObject<float, uint> vao;
    private readonly BufferObject<float> vertexBuffer;

    private readonly float[] vertices =
    [
        // position         // uv
        0f, 0f, 0f, 0f, 1f, // top-left
        1f, 0f, 0f, 1f, 1f, // top-right
        0f, 1f, 0f, 0f, 0f, // bottom-left
        1f, 1f, 0f, 1f, 0f // bottom-right
    ];


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

        //Console.WriteLine(_beatmap.AudioFilename);
        _musicChannel = Bass.CreateStream(Path.Combine(_beatmap.Folder, _beatmap.AudioFilename));
        Bass.ChannelSetAttribute(_musicChannel, ChannelAttribute.Volume, 0.02f);
        _musicVolume = 0.02f;
        _songLength = 1000 * Bass.ChannelBytes2Seconds(_musicChannel, Bass.ChannelGetLength(_musicChannel));

        _startingTimer = WAITINGTIME + _beatmap.AudioLeadIn;
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

    public void Update(double delta)
    {
        var windowSizes = _renderer.WindowSizeInPixels;
        _windowWidth = (int)windowSizes.X;
        _windowHeight = (int)windowSizes.Y;

        _startingTimer -= delta;
        if (_startingTimer <= 0 && !_musicStarted)
        {
            Bass.ChannelPlay(_musicChannel);
            Bass.ChannelSetPosition(_musicChannel, Bass.ChannelSeconds2Bytes(_musicChannel, 60));
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

        RectangleF hitbox = new(0, _windowHeight - 40, _windowWidth, 40);
        if (Input.IsMouseDown(SDLButton.SDL_BUTTON_LEFT))
            // Console.WriteLine(hitbox);
            // Console.WriteLine($"{Input.MouseX} {Input.MouseY}");
            if (hitbox.Contains(Input.MouseX, Input.MouseY))
            {
                // Console.WriteLine("Mouse inside");
                var songPointer = Util.MapRange(Input.MouseX, 0, _windowWidth, 0, (float)_songLength);
                Bass.ChannelSetPosition(_musicChannel, Bass.ChannelSeconds2Bytes(_musicChannel, songPointer / 1000));
            }

        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_SPACE))
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

        // Handle objects bkz: https://osu.ppy.sh/wiki/en/Gameplay/Judgement/osu%21 
        foreach (var hitObject in _sortedObjects)
            if (hitObject is HitCircle circle)
            {
                if (_songCursor - 150 >= circle.Time)
                {
                    circle.HitResult = HitResult.Miss;
                    circle.Failed = true;
                }

                if (Vector2.Distance(circle.Position, new Vector2(Input.MouseX, Input.MouseY)) <= _baseCircleSize / 2)
                    if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_Z) ||
                        Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_X))
                    {
                    }
            }
    }

    public void Draw(double delta)
    {
        //Bass.ChannelSetAttribute(_musicChannel, ChannelAttribute.Volume, 0.02f);
        Bass.ChannelGetAttribute(_musicChannel, ChannelAttribute.Volume, out var volume);


        if (Vector2.Distance(new Vector2(Input.MouseX, Input.MouseY), lastPosition) > 5)
            trails.Enqueue(new TrailInfo
            {
                Position = lastPosition
            });

        if (trails.Count > 48) trails.Dequeue();

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
        _renderer.DrawRectangle(0, 0, _windowWidth, _windowHeight, new Vector4(0, 0, 0, 0.75f));

        // hitobjects
        var playfieldAspect = PLAYFIELD_W / (float)PLAYFIELD_H;
        var windowAspect = _windowWidth / (float)_windowHeight;

        float playfieldWidth, playfieldHeight;

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

        Vector2 playfieldTopLeft = new(
            (_windowWidth - playfieldWidth) / 2f,
            (_windowHeight - playfieldHeight) / 2f
        );

        // draw playfield
        _renderer.DrawRectangle(
            playfieldTopLeft.X,
            playfieldTopLeft.Y,
            playfieldWidth,
            playfieldHeight,
            new Vector4(1, 1, 1, 0.6f)
        );

        var startX = (float)_windowWidth / 2 - playfieldWidth / 2;
        var startY = (float)_windowHeight / 2 - playfieldHeight / 2;
        // float osuRadius = 54.4f - 4.48f * _beatmap.CircleSize;
        // float scale = playfieldWidth / PLAYFIELD_W;
        // float circleDiameter = osuRadius * 2f * scale;
        // float circleSize = circleDiameter;
        var scale = playfieldWidth / PLAYFIELD_W;
        var osuRadius = 54.4f - 4.48f * _beatmap.CircleSize;
        _baseCircleSize = osuRadius * 2f * scale;


        foreach (var hitObject in _sortedObjects)
        {
            float preempt;
            if (_beatmap.ApproachRate < 5)
                preempt = 1200 + 600 * (5 - _beatmap.ApproachRate) / 5;
            else
                preempt = 1200 - 750 * (_beatmap.ApproachRate - 5) / 5;

            float pretime = 500;
            float posttime = 150;
            if (hitObject is HitCircle)
            {
                var posX = startX + hitObject.Position.X * scale;
                var posY = startY + hitObject.Position.Y * scale;

                if (hitObject.Time + _startingTimer - (int)_songCursor > -posttime &&
                    hitObject.Time + _startingTimer - (int)_songCursor < pretime)
                {
                    var _fadein = 400 * Math.Min(1, preempt / 450);
                    var objectCircleSize = _baseCircleSize;
                    var fadein =
                        Math.Clamp(
                            Util.MapRange((float)_songCursor, hitObject.Time - _fadein, hitObject.Time - pretime / 2,
                                0, 1), 0, 1);
                    var fadeout =
                        Math.Clamp(Util.MapRange((float)_songCursor, hitObject.Time, hitObject.Time + posttime, 1, 0),
                            0, 1);
                    var drawSize = (float)(objectCircleSize + objectCircleSize * (1 - fadeout) * 0.1);
                    var approachCircleSize =
                        Math.Max(
                            Util.MapRange((float)_songCursor, hitObject.Time - pretime, hitObject.Time + 0,
                                drawSize * 4, drawSize), drawSize);


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

                    // number handling
                    // Texture texture = _numberTextures[hitObject.ComboNumber];
                    // var numHeight = drawSize * 0.3f;
                    // var numRatio = numHeight / texture.Height;
                    // var numWidth = texture.Width * numRatio;

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
                var posX = startX + hitObject.Position.X * scale;
                var posY = startY + hitObject.Position.Y * scale;

                if (hitObject.Time + _startingTimer - (int)_songCursor > -posttime &&
                    hitObject.Time + _startingTimer - (int)_songCursor < pretime)
                {
                    var _fadein = 400 * Math.Min(1, preempt / 450);
                    var objectCircleSize = _baseCircleSize;
                    var fadein =
                        Math.Clamp(
                            Util.MapRange((float)_songCursor, hitObject.Time - _fadein, hitObject.Time - pretime / 2,
                                0, 1), 0, 1);
                    var fadeout =
                        Math.Clamp(Util.MapRange((float)_songCursor, hitObject.Time, hitObject.Time + posttime, 1, 0),
                            0, 1);
                    var drawSize = (float)(objectCircleSize + objectCircleSize * (1 - fadeout) * 0.1);
                    var approachCircleSize =
                        Math.Max(
                            Util.MapRange((float)_songCursor, hitObject.Time - pretime, hitObject.Time + 0,
                                drawSize * 4, drawSize), drawSize);

                    foreach (var point in slider.Points)
                    {
                        var scaledX = startX + point.X * scale;
                        var scaledY = startY + point.Y * scale;

                        _renderer.DrawTexture(_circleTexture,
                            scaledX - drawSize / 2,
                            scaledY - drawSize / 2,
                            drawSize,
                            drawSize,
                            hitObject.Color with { W = Math.Min(fadein, fadeout) });
                    }

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
        }
        //Console.Write($"\n");


        var yellow = new Vector4(242 / 255f, 191 / 255f, 36 / 255f, 1);
        // timeline
        var songPointer = Util.MapRange((float)_songCursor, 0, (float)_songLength, 0, _windowWidth);
        _renderer.DrawRectangle(0, _windowHeight - 20, songPointer, 20, yellow);

        // volume control

        _renderer.DrawRectangle(0, (float)_windowHeight / 2 - 150, 40, 300, new Vector4(0.1f, 0.1f, 0.1f, 1));

        var length = Util.MapRange(_musicVolume, 0, 1, 0, 300);
        _renderer.DrawRectangle(10, (float)_windowHeight / 2 - 150, 20, length, yellow);

        // TODO: input overlay
        

        // cursor
        var size = _baseCircleSize / 2f;
        lastPosition = new Vector2(Input.MouseX, Input.MouseY);
        var i = trails.Count;
        foreach (var trail in trails.Reverse())
        {
            trail.Life -= (float)delta;
            if (trail.Life <= 0) trails.Dequeue();

            size -= 1;
            _renderer.DrawTexture(_cursorTrailTexture,
                trail.Position.X - size / 2,
                trail.Position.Y - size / 2,
                size, size, new Vector4(1, 1, 1, (float)i / trails.Count));
            i--;
        }

        size = _baseCircleSize / 2f; // _cursorTexture.Width * 1.5f;
        _renderer.DrawTexture(_cursorTexture,
            Input.MouseX - size / 2,
            Input.MouseY - size / 2,
            size, size, new Vector4(1, 1, 1, 1));

        _renderer.DrawText(_msdfFont, "The quick brown fox jumps over the lazy dog\n" +
                                      "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. ", new Vector2(0, 0), 0.5f,
            new Vector4(1, 1, 1, 1));
    }

    public class TrailInfo
    {
        public float Life = 50;
        public Vector2 Position;
    }
}