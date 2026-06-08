using System.Drawing;
using OpenTK.Mathematics;
using SDL;
using Velto.Audio;
using Velto.Core;
using Velto.Game.osu;
using Velto.Graphics;
using Velto.Graphics.OpenGL;

namespace Velto.Game.Views;

public class SongSelectScreen : Screen, IDisposable
{
    private const float BoxHeight = 300;
    private float BoxWidth = 300;
    private List<BeatmapMeta> _beatmapMetas = new();
    private int _cursor = 0;
  
    private OsuContext _context;
    private Dictionary<string, ITexture> _textureCache = new();
    private IGraphicsDevice device;
    public SongSelectScreen(IGraphicsDevice device, OsuContext context) : base(device, context)
    {
        this.device = device;
        _context = context;
    }

    public override void Update(double dt)
    {
        BoxWidth = Width / 1.5f;

        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_UP))
        {
            _cursor -= 1;
            AudioManager.Instance.PlaySample(_context.Skin.MenuClick);
        }
        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_DOWN))
        {
            _cursor += 1;
            AudioManager.Instance.PlaySample(_context.Skin.MenuClick);
        }
        
        foreach (var box in _beatmapMetas)
        {
            var wasHovering = box.IsHovered;
            box.IsHovered = false;
            RectangleF collision = new(box.Position.X, box.Position.Y, box.Size.X, box.Size.Y);
            if (collision.Contains(Input.MouseX, Input.MouseY))
            {
                if (!wasHovering) AudioManager.Instance.PlaySample(_context.Skin.MenuClick);
                box.IsHovered = true;
            }
        }

        _cursor -= (int)Math.Clamp(Input.WheelY * 20, -1, 1);
        
        _cursor = Math.Clamp(_cursor, 0, _beatmapMetas.Count - 1);
        
        for (int i = 0; i < _beatmapMetas.Count; i++)
        {
            var th = i - _cursor;
            _beatmapMetas[i].Size = new Vector2(Width, BoxHeight);
            _beatmapMetas[i].Position = new Vector2(
                Width/4 + Math.Abs(100 * th),
                (Height/2 - BoxHeight/2) + ((BoxHeight/1.2f) * th)
            );
        }


        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_RETURN))
        {
            PlayBeatmap(_beatmapMetas[_cursor].Beatmap);
        }
        
        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_ESCAPE))
        {
            AudioManager.Instance.PlaySample(_context.Skin.MenuBack);
            Transition(new IntroScreen(device, _context), 200);
        }

        if (Input.IsMouseJustPressed(SDLButton.SDL_BUTTON_LEFT))
        {
            var meta = _beatmapMetas.FirstOrDefault(m => m.IsHovered);
            if (meta != null)
            {
                PlayBeatmap(meta.Beatmap);
            }
        }
    }

    public override void Draw(IRenderer r)
    {
        r.PushScissor(0, 0, (int)Width, (int)Height);
        r.DrawTexture(_context.Skin.MenuBackground, 0, 0, Width, Height, Color4.White);
        
        for (int i = 0; i < _cursor; i++)
        {
            DrawBeatmapBox(r, _beatmapMetas[i]);
        }
        
        for (int i = _beatmapMetas.Count - 1; i > _cursor; i--)
        {
            DrawBeatmapBox(r, _beatmapMetas[i]);
        }
        
        DrawBeatmapBox(r, _beatmapMetas[_cursor]);
        
        r.PopScissor();
    }
    
    private void DrawBeatmapBox(IRenderer r, BeatmapMeta box)
    {
        var selectedColor = box.IsHovered
            ? Color4.Deeppink
            : Color4.Darkorange;

        r.DrawTexture(_context.Skin.MenuButtonBackground,
            box.Position.X,
            box.Position.Y,
            box.Size.X,
            box.Size.Y,
            selectedColor
        );

        var thumbWidth = BoxHeight * 1.5f;

        
        // draw text
        r.DrawText(Fonts.Default, box.Beatmap.ToString(),
            new Vector2(box.Position.X + 200 + thumbWidth, box.Position.Y + 80),
            box.Size.Y/5f, new Color4<Rgba>(1, 1, 1, 1));
        r.DrawText(Fonts.Default, $"By: {box.Beatmap.Creator}",
            new Vector2(box.Position.X + 200 + thumbWidth, box.Position.Y + 150),
            box.Size.Y/5f, new Color4<Rgba>(1, 1, 1, 1));
        
        r.FlushText(Fonts.Default);
    }

    private void PlayBeatmap(Beatmap beatmap)
    {
        AudioManager.Instance.PlaySample(_context.Skin.MenuClick);
        var game = new GameScreen(device, _context);
        game.SetBeatmap(beatmap);
        game.Player.SetState(PlayerState.Autoplay);
        Transition(game, 200);
    }

    public override void OnEnter()
    {
        base.OnEnter();
        LoadBeatmaps();
        foreach (var meta in _beatmapMetas)
        {
            meta.LoadTexture();
        }
    }

    public override void OnExit()
    {
        base.OnExit();
        Dispose();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var texture in _textureCache)
            {
                texture.Value.Dispose();
            }
        }
    }

    public new void Dispose()
    {
        Dispose(true);
        base.Dispose();
        GC.SuppressFinalize(this);
    }
    
    public override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        Width = e.Width;
        Height = e.Height;
    }

    public void LoadBeatmaps()
    {
        var songDirs = Directory.GetDirectories(Resources.GetPath("Resources/Songs"));
        foreach (var dir in songDirs)
        {
            var files = Directory.GetFiles(dir);
            foreach (var file in files)
            {
                if (Path.GetExtension(file) == ".osu")
                {
                    var box = new BeatmapMeta();
                    box.Beatmap = new Beatmap(file);
                    box.Path = Path.Combine(box.Beatmap.Folder, box.Beatmap.BackgroundFile);

                    if (!_textureCache.ContainsKey(box.Path))
                    {
                        //_textureCache[box.Path] = new Texture(box.Path);
                    }
                    
                    _beatmapMetas.Add(box);
                }
            }
        }
    }
    
    private class BeatmapMeta
    {
        public Beatmap Beatmap;
        public Vector2 Position;
        public Vector2 Size;
        public string Path;
        public bool IsHovered = false;

        public void LoadTexture()
        {
            //if (Texture is not null) return;
            //Texture = new Texture(Path.Combine(Beatmap.Folder, Beatmap.BackgroundFile));
        }

        public void UnloadTexture()
        {
            //Texture?.Dispose();
        }
    }
}