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
    private Dictionary<int, Texture> _numberTextures = new();

    private int _windowWidth, _windowHeight;

    private Beatmap _beatmap;
    private int _musicChannel;
    private double _songCursor;
    private int _objectComboNumber = 1;
    private int _colorCounter = 0;
    private const double WAITINGTIME = 2000f;
    private double _startingTimer = 0;
    private bool _musicStarted = false;


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
        _beatmap = new(Resources.GetPath(
            "Resources/Songs/Wakeshima Kanon/ASCA - Nisemono no Koi ni Sayounara with Wakeshima Kanon (timemon) [Kyou's Extra].osu"));
        _backgroundTexture = new(Path.Combine(_beatmap.Folder, _beatmap.BackgroundFile));

        _whiteTexture = new(Resources.GetPath("Resources/Textures/white.png"));
        _hitcircleTexture = new(Resources.GetPath("Resources/Textures/skin/hitcircle.png"));
        _hitcircleOverlayTexture = new(Resources.GetPath("Resources/Textures/skin/hitcircleoverlay.png"));
        _cursorTexture = new(Resources.GetPath("Resources/Textures/skin/cursor.png"));
        _cursorTrailTexture = new(Resources.GetPath("Resources/Textures/skin/cursortrail.png"));
        _approachCircleTexture = new(Resources.GetPath("Resources/Textures/skin/approachcircle.png"));

        for (int i = 0; i < 10; i++)
        {
            _numberTextures.Add(i, new Texture(Resources.GetPath($"Resources/Textures/skin/default-{i}.png")));
        }

        Bass.Init();

        Console.WriteLine(_beatmap.AudioFilename);
        _musicChannel = Bass.CreateStream(Path.Combine(_beatmap.Folder, _beatmap.AudioFilename));
        Bass.ChannelSetAttribute(_musicChannel, ChannelAttribute.Volume, 0.02f);

        _startingTimer = WAITINGTIME + _beatmap.AudioLeadIn;
    }

    public void Update(double delta)
    {
        _startingTimer -= delta;
        if (_startingTimer <= 0 && !_musicStarted)
        {
            Bass.ChannelPlay(_musicChannel);
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
        Console.WriteLine(pos);
        _songCursor = (ulong)(pos * 1000);
    }

    public class TrailInfo
    {
        public Vector2 Position = new Vector2();
        public float Life = 50;

        public TrailInfo()
        {
        }
    }

    float mapRange(float input, float input_L, float input_H, float out_L, float out_H) => out_L+((input-input_L)/(input_H-input_L))*(out_H-out_L);
    

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

        int windowWidth, windowHeight;
        SDL_GetWindowSizeInPixels(_window, &windowWidth, &windowHeight);
        _windowWidth = windowWidth;
        _windowHeight = windowHeight;

        GL.Viewport(0, 0, _windowWidth, _windowHeight);
        GL.Enable(EnableCap.Blend);
        //GL.Enable(EnableCap.DepthTest);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.ClearColor(0.1f, 0.2f, 0.3f, 1f);
        GL.Clear(ClearBufferMask.ColorBufferBit /*| ClearBufferMask.DepthBufferBit*/);


        // bg
        if (_windowWidth * ((float)_backgroundTexture.Height / _backgroundTexture.Width) > _windowHeight)
        {
            // sağ sol bar
            float padding = windowWidth - (_windowHeight * (((float)_backgroundTexture.Width) / (float)_backgroundTexture.Height));
            drawTexture(_backgroundTexture, padding/2, 0, _windowHeight*((float)_backgroundTexture.Width/_backgroundTexture.Height), _windowHeight, new(1, 1, 1, 1));
        }
        else
        {
            // üst alt bar
            float padding = windowHeight - (_windowWidth * (((float)_backgroundTexture.Height) /
                                                            ((float)_backgroundTexture.Width)));
            drawTexture(_backgroundTexture, 0, padding/2, _windowWidth, _windowWidth*((float)_backgroundTexture.Height/_backgroundTexture.Width), new(1, 1, 1, 1));
        }

        // hitobjects
        float ratio = PLAYFIELD_W / (float)PLAYFIELD_H;

        float max = Math.Min(_windowWidth, _windowHeight);
        max -= 350; // some offset

        float w = max * ratio;
        float h = max;

        Vector2 playfieldAreaSize = new((float)_windowWidth / 2 - w / 2, (float)_windowHeight / 2 - h / 2);

        drawRectangle(
            playfieldAreaSize.X,
            playfieldAreaSize.Y,
            w, h, new Vector4(1, 1, 1, 0.6f));

        float startX = (float)_windowWidth / 2 - w / 2;
        float startY = (float)_windowHeight / 2 - h / 2;

        foreach (var hitObject in _beatmap.HitObjects)
        {
            if (hitObject is HitCircle)
            {
                float ratioX = w / PLAYFIELD_W;
                float ratioY = h / PLAYFIELD_H;
                float posX = startX + hitObject.Position.X * ratioX;
                float posY = startY + hitObject.Position.Y * ratioY;

                float areaRatio = w/PLAYFIELD_W;
                float circleSize = 100f * areaRatio;
                float pretime = 500;
                float posttime = 500;
                

                if ((hitObject.Time + _startingTimer - (int)_songCursor) > -posttime && (hitObject.Time + _startingTimer - (int)_songCursor) < pretime)
                {
                    if (hitObject.NewCombo)
                    {
                        _objectComboNumber = 1;
                        _colorCounter += 1;
                        if (_colorCounter >= 4)
                        {
                            _colorCounter = 0;
                        }
                    }

                    Vector4 _color = _colorCounter switch
                    {
                        0 => new Vector4(1, 0, 0, 1),
                        1 => new Vector4(0, 1, 0, 1),
                        2 => new Vector4(0, 0, 1, 1),
                        3 => new Vector4(1, 0, 1, 1),
                    };

                    if (!hitObject.DidSetColor)
                    {
                        hitObject.Color = _color;
                        hitObject.DidSetColor = true;
                    }
                    
                    
                    float fadein = Math.Clamp(mapRange((float)_songCursor, hitObject.Time-pretime, (hitObject.Time - pretime / 2), 0, 1),0,1);
                    float fadeout = Math.Clamp(mapRange((float)_songCursor, hitObject.Time, hitObject.Time +posttime, 1, 0),0,1);
                    circleSize += circleSize * (1-fadeout) * .1f;
                    float approachCircleSize = Math.Max(mapRange((float)_songCursor, hitObject.Time - pretime, hitObject.Time + 0, circleSize*4, circleSize),circleSize);
                    // posY += successMove;
                    drawTexture(_approachCircleTexture,
                        posX - approachCircleSize / 2,
                        posY - approachCircleSize / 2,
                        approachCircleSize,
                        approachCircleSize, new Vector4(0, 0, 0, 1));

                    drawTexture(_hitcircleTexture,
                        posX - circleSize / 2,
                        posY - circleSize / 2,
                        circleSize,
                        circleSize, hitObject.Color with {W = Math.Min(fadein,fadeout)});
                    
                    drawTexture(_hitcircleOverlayTexture,
                        posX - circleSize / 2,
                        posY - circleSize / 2,
                        circleSize,
                        circleSize, new Vector4(1, 1, 1, 1) with {W = Math.Min(fadein,fadeout)});

                    if (_objectComboNumber >= 10) _objectComboNumber = 9; // lets not crash rn
                    if (!hitObject.DidSetCombo)
                    {
                        hitObject.Combo = _objectComboNumber;
                        hitObject.DidSetCombo = true;
                    }

                    Texture texture = _numberTextures[hitObject.Combo];
                    var numHeight = circleSize - 90;
                    var numRatio = numHeight / texture.Height;
                    var numWidth = texture.Width * numRatio;

                    drawTexture(texture,
                        posX - numWidth / 2,
                        posY - numHeight / 2,
                        numWidth, numHeight, new(1, 1, 1, 1));


                    _objectComboNumber++;
                }
                
            }
        }
        Console.Write($"\n");

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
        foreach (var pair in _numberTextures)
        {
            pair.Value.Dispose();
        }
    }
}