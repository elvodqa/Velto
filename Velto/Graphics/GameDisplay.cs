using System.Drawing;
using ManagedBass;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using SDL;
using Velto.Gameplay;
using static SDL.SDL3;

namespace Velto.Graphics;

public unsafe class GameDisplay : IDisposable
{
    private float[] vertices =
    [
        // position         // uv
        0f, 0f, 0f, 0f, 1f, // top-left
        1f, 0f, 0f, 1f, 1f, // top-right
        0f, 1f, 0f, 0f, 0f, // bottom-left
        1f, 1f, 0f, 1f, 0f // bottom-right
    ];

    private uint[] indices =
    [
        0, 2, 1, // first triangle
        1, 2, 3 // second triangle
    ];

    private const int PLAYFIELD_W = 512;
    private const int PLAYFIELD_H = 384;

    private SDL_Window* _window;
    private BufferObject<float> vertexBuffer;
    private BufferObject<uint> indexBuffer;
    private VertexArrayObject<float, uint> vao;
    private Shader shader;

    private Texture _cursorTexture;
    private Texture _cursorTrailTexture;
    private Texture _whiteTexture;
    private Texture _backgroundTexture;
    private Texture _hitcircleTexture;
    private Texture _hitcircleOverlayTexture;
    private Texture _approachCircleTexture;
    private Texture _sliderballTexture;
    private Texture _circleTexture;
    private Dictionary<int, Texture> _numberTextures = new();

    private int _windowWidth, _windowHeight;

    private Beatmap _beatmap;
    private int _musicChannel;
    private double _songCursor;
    private double _songLength;
    private int _objectComboNumber = 1;
    private int _colorCounter = 0;
    private const double WAITINGTIME = 0f;
    private double _startingTimer = 0;
    private bool _musicStarted = false;
    private string _skinName = "rafis";


    public GameDisplay(SDL_Window* window)
    {
        _window = window;
        vertexBuffer = new(vertices, BufferTarget.ArrayBuffer, BufferUsage.StaticDraw);
        indexBuffer = new(indices, BufferTarget.ElementArrayBuffer, BufferUsage.StaticDraw);
        vao = new VertexArrayObject<float, uint>(vertexBuffer, indexBuffer);
        vao.VertexAttributePointer(0, 3, VertexAttribPointerType.Float, 5 * sizeof(float), 0);
        vao.VertexAttributePointer(1, 2, VertexAttribPointerType.Float, 5 * sizeof(float), 3 * sizeof(float));
        shader = new("sprite"); // pippidonclear0

        //_beatmap = new(Resources.GetPath("Resources/Songs/lotus/Susumu Hirasawa - SWITCHED-ON LOTUS (Starrodkirby86) [KIRBY Mix Deluxe].osu"));
         _beatmap = new(Resources.GetPath("Resources/Songs/Wakeshima Kanon/ASCA - Nisemono no Koi ni Sayounara with Wakeshima Kanon (timemon) [Kyou's Extra].osu"));
        //_beatmap = new(Resources.GetPath("Resources/Songs/Centipede/Knife Party - Centipede (Sugoi-_-Desu) [This isn't a map, just a simple visualisation].osu"));
        // _beatmap = new(Resources.GetPath("Resources/Songs/exit/Camellia - Exit This Earth's Atomosphere (Camellia's ''PLANETARY200STEP'' Remix) (ProfessionalBox) [Primordial Nucleosynthesis].osu"));

        _backgroundTexture = new(Path.Combine(_beatmap.Folder, _beatmap.BackgroundFile));

        _whiteTexture = new(Resources.GetPath("Resources/Textures/white.png"));
        _hitcircleTexture = new(Resources.GetPath($"Resources/Textures/{_skinName}/hitcircle.png"));
        _hitcircleOverlayTexture = new(Resources.GetPath($"Resources/Textures/{_skinName}/hitcircleoverlay.png"));
        _cursorTexture = new(Resources.GetPath($"Resources/Textures/{_skinName}/cursor.png"));
        _cursorTrailTexture = new(Resources.GetPath($"Resources/Textures/{_skinName}/cursortrail.png"));
        _approachCircleTexture = new(Resources.GetPath($"Resources/Textures/{_skinName}/approachcircle.png"));
        _sliderballTexture = new(Resources.GetPath($"Resources/Textures/{_skinName}/sliderb.png"));
        _circleTexture = new(Resources.GetPath($"Resources/Textures/circle.png"));

        for (int i = 0; i < 10; i++)
        {
            _numberTextures.Add(i, new Texture(Resources.GetPath($"Resources/Textures/{_skinName}/default-{i}.png")));
        }

        Bass.Init();

        Console.WriteLine(_beatmap.AudioFilename);
        _musicChannel = Bass.CreateStream(Path.Combine(_beatmap.Folder, _beatmap.AudioFilename));
        Bass.ChannelSetAttribute(_musicChannel, ChannelAttribute.Volume, 0.02f);
        _songLength = 1000 * Bass.ChannelBytes2Seconds(_musicChannel, Bass.ChannelGetLength(_musicChannel));

        _startingTimer = WAITINGTIME + _beatmap.AudioLeadIn;
    }

    public void Update(double delta)
    {
        int windowWidth, windowHeight;
        SDL_GetWindowSizeInPixels(_window, &windowWidth, &windowHeight);
        _windowWidth = windowWidth;
        _windowHeight = windowHeight;

        _startingTimer -= delta;
        if (_startingTimer <= 0 && !_musicStarted)
        {
            Bass.ChannelPlay(_musicChannel);
            Bass.ChannelSetPosition(_musicChannel, Bass.ChannelSeconds2Bytes(_musicChannel, 60));
            _musicStarted = true;
        }

        if (_musicStarted)
        {
            _startingTimer = 0;
        }

        var posByte = Bass.ChannelGetPosition(_musicChannel, PositionFlags.Bytes);
        var pos = Bass.ChannelBytes2Seconds(_musicChannel, posByte);
        var err = Bass.LastError;
        if (err != Errors.OK)
        {
            Console.WriteLine(err);
        }

        //Console.WriteLine(pos);
        _songCursor = (ulong)(pos * 1000);


        //float songPointer = Util.MapRange((float)_songCursor, 0, (float)_songLength, 0, _windowWidth);
        //drawRectangle(0, windowHeight - 40, songPointer, 40, new Vector4(242/255f, 191/255f, 36/255f, 1));
        RectangleF hitbox = new(0, _windowHeight - 40, _windowWidth, 40);
        float mouseX, mouseY;
        var mFlags = SDL_GetMouseState(&mouseX, &mouseY);
        if ((mFlags & SDL_MouseButtonFlags.SDL_BUTTON_LMASK) != 0)
        {
            // Console.WriteLine(hitbox);
            // Console.WriteLine($"{mouseX} {mouseY}");
            if (hitbox.Contains(mouseX, mouseY))
            {
                Console.WriteLine("Mouse inside");
                float songPointer = Util.MapRange(mouseX, 0, _windowWidth, 0, (float)_songLength);
                Bass.ChannelSetPosition(_musicChannel, Bass.ChannelSeconds2Bytes(_musicChannel, songPointer / 1000));
            }
        }
    }

    public class TrailInfo
    {
        public Vector2 Position = new Vector2();
        public float Life = 50;

        public TrailInfo()
        {
        }
    }

    private Queue<TrailInfo> trails = new();
    private Vector2 lastPosition = Vector2.Zero;

    public void Draw(double delta)
    {
        float mouseX = 0, mouseY = 0;
        SDL_GetMouseState(&mouseX, &mouseY);
        //mouseX *= 2;
        //mouseY *= 2;
        //SDL_HideCursor();

        if (Vector2.Distance(new Vector2(mouseX, mouseY), lastPosition) > 5)
        {
            trails.Enqueue(new TrailInfo()
            {
                Position = lastPosition,
            });
        }

        if (trails.Count > 48)
        {
            trails.Dequeue();
        }

        GL.Viewport(0, 0, _windowWidth, _windowHeight);
        GL.Enable(EnableCap.Blend);
        //GL.Enable(EnableCap.DepthTest);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        //GL.ClearColor(0.1f, 0.2f, 0.3f, 1f);
        GL.ClearColor(0f, 0f, 0f, 1f);
        GL.Clear(ClearBufferMask.ColorBufferBit /*| ClearBufferMask.DepthBufferBit*/);


        // bg
        if (_windowWidth * ((float)_backgroundTexture.Height / _backgroundTexture.Width) > _windowHeight)
        {
            // sağ sol bar
            float padding = _windowWidth -
                            (_windowHeight * (((float)_backgroundTexture.Width) / (float)_backgroundTexture.Height));
            drawTexture(_backgroundTexture, padding / 2, 0,
                _windowHeight * ((float)_backgroundTexture.Width / _backgroundTexture.Height), _windowHeight,
                new(1, 1, 1, 1));
        }
        else
        {
            // üst alt bar
            float padding = _windowHeight - (_windowWidth * (((float)_backgroundTexture.Height) /
                                                             ((float)_backgroundTexture.Width)));
            drawTexture(_backgroundTexture, 0, padding / 2, _windowWidth,
                _windowWidth * ((float)_backgroundTexture.Height / _backgroundTexture.Width), new(1, 1, 1, 1));
        }

        // hitobjects
        float ratio = PLAYFIELD_W / (float)PLAYFIELD_H;

        float max = Math.Min(_windowWidth, _windowHeight);
        max -= 350; // some offset

        float playfieldWidth = max * ratio;
        float playfieldHeight = max;

        Vector2 playfieldAreaTopLeftCorner = new((float)_windowWidth / 2 - playfieldWidth / 2,
            (float)_windowHeight / 2 - playfieldHeight / 2);

        drawRectangle(
            playfieldAreaTopLeftCorner.X,
            playfieldAreaTopLeftCorner.Y,
            playfieldWidth, playfieldHeight, new Vector4(1, 1, 1, 0.6f));

        float startX = (float)_windowWidth / 2 - playfieldWidth / 2;
        float startY = (float)_windowHeight / 2 - playfieldHeight / 2;


        // float osuRadius = 54.4f - 4.48f * _beatmap.CircleSize;
        // float scale = playfieldWidth / PLAYFIELD_W;
        // float circleDiameter = osuRadius * 2f * scale;
        // float circleSize = circleDiameter;
        float scale = playfieldWidth / PLAYFIELD_W;

        float osuRadius = 54.4f - 4.48f * _beatmap.CircleSize;

        float baseCircleSize = osuRadius * 2f * scale;

        foreach (var hitObject in _beatmap.HitObjects)
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
                float posX = startX + hitObject.Position.X * scale;
                float posY = startY + hitObject.Position.Y * scale;

                if ((hitObject.Time + _startingTimer - (int)_songCursor) > -posttime &&
                    (hitObject.Time + _startingTimer - (int)_songCursor) < pretime)
                {
                    float _fadein = 400 * Math.Min(1, preempt / 450);
                    float objectCircleSize = baseCircleSize;
                    float fadein =
                        Math.Clamp(
                            Util.MapRange((float)_songCursor, hitObject.Time - _fadein, (hitObject.Time - pretime / 2),
                                0, 1), 0, 1);
                    float fadeout =
                        Math.Clamp(Util.MapRange((float)_songCursor, hitObject.Time, hitObject.Time + posttime, 1, 0),
                            0, 1);
                    float drawSize = (float)(objectCircleSize + objectCircleSize * (1 - fadeout) * 0.1);
                    float approachCircleSize =
                        Math.Max(
                            Util.MapRange((float)_songCursor, hitObject.Time - pretime, hitObject.Time + 0,
                                drawSize * 4, drawSize), drawSize);

                    drawTexture(_approachCircleTexture,
                        posX - approachCircleSize / 2,
                        posY - approachCircleSize / 2,
                        approachCircleSize,
                        approachCircleSize, hitObject.Color with { W = Math.Min(fadein, fadeout) });

                    drawTexture(_hitcircleTexture,
                        posX - drawSize / 2,
                        posY - drawSize / 2,
                        drawSize,
                        drawSize, hitObject.Color with { W = Math.Min(fadein, fadeout) });

                    drawTexture(_hitcircleOverlayTexture,
                        posX - drawSize / 2,
                        posY - drawSize / 2,
                        drawSize,
                        drawSize, new Vector4(1, 1, 1, 1) with { W = Math.Min(fadein, fadeout) });

                    // number handling
                    Texture texture = _numberTextures[hitObject.ComboNumber];
                    var numHeight = drawSize - 90;
                    var numRatio = numHeight / texture.Height;
                    var numWidth = texture.Width * numRatio;

                    drawTexture(texture,
                        posX - numWidth / 2,
                        posY - numHeight / 2,
                        numWidth, numHeight, new Vector4(1, 1, 1, 1) with { W = Math.Min(fadein, fadeout) });
                }
            }

            if (hitObject is Slider slider)
            {
                float posX = startX + hitObject.Position.X * scale;
                float posY = startY + hitObject.Position.Y * scale;

                if ((hitObject.Time + _startingTimer - (int)_songCursor) > -posttime &&
                    (hitObject.Time + _startingTimer - (int)_songCursor) < pretime)
                {
                    float _fadein = 400 * Math.Min(1, preempt / 450);
                    float objectCircleSize = baseCircleSize;
                    float fadein =
                        Math.Clamp(
                            Util.MapRange((float)_songCursor, hitObject.Time - _fadein, (hitObject.Time - pretime / 2),
                                0, 1), 0, 1);
                    float fadeout =
                        Math.Clamp(Util.MapRange((float)_songCursor, hitObject.Time, hitObject.Time + posttime, 1, 0),
                            0, 1);
                    float drawSize = (float)(objectCircleSize + objectCircleSize * (1 - fadeout) * 0.1);
                    float approachCircleSize =
                        Math.Max(
                            Util.MapRange((float)_songCursor, hitObject.Time - pretime, hitObject.Time + 0,
                                drawSize * 4, drawSize), drawSize);
                    
                    foreach (var point in slider.Points)
                    {
                        float scaledX = startX + point.X * scale;
                        float scaledY = startY + point.Y * scale;

                        drawTexture(_circleTexture,
                            scaledX - drawSize / 2,
                            scaledY - drawSize / 2,
                            drawSize,
                            drawSize,
                            hitObject.Color with { W = Math.Min(fadein, fadeout) });
                    }

                    drawTexture(_approachCircleTexture,
                        posX - approachCircleSize / 2,
                        posY - approachCircleSize / 2,
                        approachCircleSize,
                        approachCircleSize, hitObject.Color with { W = Math.Min(fadein, fadeout) });

                    drawTexture(_hitcircleTexture,
                        posX - drawSize / 2,
                        posY - drawSize / 2,
                        drawSize,
                        drawSize, hitObject.Color with { W = Math.Min(fadein, fadeout) });

                    drawTexture(_hitcircleOverlayTexture,
                        posX - drawSize / 2,
                        posY - drawSize / 2,
                        drawSize,
                        drawSize, new Vector4(1, 1, 1, 1) with { W = Math.Min(fadein, fadeout) });

                    // number handling
                    Texture texture = _numberTextures[hitObject.ComboNumber];
                    var numHeight = drawSize - 90;
                    var numRatio = numHeight / texture.Height;
                    var numWidth = texture.Width * numRatio;

                    drawTexture(texture,
                        posX - numWidth / 2,
                        posY - numHeight / 2,
                        numWidth, numHeight, new Vector4(1, 1, 1, 1) with { W = Math.Min(fadein, fadeout) });

                   
                }
            }
        }
        //Console.Write($"\n");


        // timeline
        float songPointer = Util.MapRange((float)_songCursor, 0, (float)_songLength, 0, _windowWidth);
        drawRectangle(0, _windowHeight - 40, songPointer, 40, new Vector4(242 / 255f, 191 / 255f, 36 / 255f, 1));

        // cursor
        float size = _cursorTrailTexture.Width * 1.5f;
        lastPosition = new(mouseX, mouseY);
        int i = trails.Count;
        foreach (var trail in trails.Reverse())
        {
            trail.Life -= (float)delta;
            if (trail.Life <= 0)
            {
                trails.Dequeue();
            }

            size -= 1;
            drawTexture(_cursorTrailTexture,
                trail.Position.X - size / 2,
                trail.Position.Y - size / 2,
                size, size, new(1, 1, 1, (float)i / trails.Count),
                0);
            i--;
        }

        size = _cursorTexture.Width * 1.5f;
        drawTexture(_cursorTexture,
            mouseX - size / 2,
            mouseY - size / 2,
            size, size, new(1, 1, 1, 1),
            0);
    }

    private void drawTexture(Texture texture, float x, float y, float width, float height, Vector4 color,
        float rotation = 0)

    {
        Matrix4 projection =
            Matrix4.CreateOrthographicOffCenter(
                0,
                _windowWidth,
                _windowHeight,
                0,
                -1f,
                1f);

        Matrix4 model =
            Matrix4.CreateTranslation(-0.5f, -0.5f, 0f) *
            Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(rotation)) *
            Matrix4.CreateTranslation(0.5f, 0.5f, 0f) *
            Matrix4.CreateScale(width, height, 1f) *
            Matrix4.CreateTranslation(x, y, 0f);

        texture.Bind();

        shader.Use();
        shader.SetInt("ourTexture", 0);
        shader.SetVector4("color", color);
        shader.SetMatrix4("model", model);
        shader.SetMatrix4("view", Matrix4.Identity);
        shader.SetMatrix4("projection", projection);

        vao.Bind();

        GL.DrawElements(
            PrimitiveType.Triangles,
            6,
            DrawElementsType.UnsignedInt,
            IntPtr.Zero);
    }

    private void drawRectangle(float x, float y, float width, float height, Vector4 color,
        float rotation = 0)
    {
        Matrix4 projection =
            Matrix4.CreateOrthographicOffCenter(
                0,
                _windowWidth,
                _windowHeight,
                0,
                -1f,
                1f);

        Matrix4 model =
            Matrix4.CreateTranslation(-0.5f, -0.5f, 0f) *
            Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(rotation)) *
            Matrix4.CreateTranslation(0.5f, 0.5f, 0f) *
            Matrix4.CreateScale(width, height, 1f) *
            Matrix4.CreateTranslation(x, y, 0f);

        _whiteTexture.Bind();

        shader.Use();
        shader.SetInt("ourTexture", 0);
        shader.SetVector4("color", color);
        shader.SetMatrix4("model", model);
        shader.SetMatrix4("view", Matrix4.Identity);
        shader.SetMatrix4("projection", projection);

        vao.Bind();

        GL.DrawElements(
            PrimitiveType.Triangles,
            6,
            DrawElementsType.UnsignedInt,
            IntPtr.Zero);
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
        foreach (var pair in _numberTextures)
        {
            pair.Value.Dispose();
        }
    }
}