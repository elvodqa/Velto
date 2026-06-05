using System.Drawing;
using OpenTK.Mathematics;
using SDL;
using Velto.Audio;
using Velto.Core;
using Velto.Game.osu;
using Velto.Graphics;

namespace Velto.Game.Views;

public class SongSelectView : View, IDisposable
{
    private const float BoxHeight = 300;
    private float BoxWidth = 300;
    private List<BeatmapMeta> _beatmapMetas = new();
    private int _cursor = 0;
    private Texture _menuBackground;
    private Texture _menuButtonBackground;
    private AudioChannel _menuClickAudio;
    private AudioChannel _menuBackAudio;
        
    public override void Update(double dt)
    {
        BoxWidth = Width / 1.5f;

        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_UP))
        {
            _cursor -= 1;
            AudioManager.Instance.PlaySample(_menuClickAudio);
        }
        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_DOWN))
        {
            _cursor += 1;
            AudioManager.Instance.PlaySample(_menuClickAudio);
        }

        _cursor += (int)Math.Clamp(Input.WheelY * 20, -1, 1);
        
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
            AudioManager.Instance.PlaySample(_menuClickAudio);
            var game = Create<GameView>();
            game.SetBeatmap(_beatmapMetas[_cursor].Beatmap);
            game.Player.SetState(PlayerState.Autoplay);
            ViewManager.Instance.Transition(this, game, 1000);
        }
        
        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_ESCAPE))
        {
            AudioManager.Instance.PlaySample(_menuBackAudio);
            ViewManager.Instance.Transition(this, Create<IntroView>(), 1000);
        }
    }

    public override void Draw(double dt, Renderer r)
    {
        r.PushScissor(0, 0, Width, Height);
        r.DrawTexture(_menuBackground, 0, 0, Width, Height, Color4.White);
        
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
    
    private void DrawBeatmapBox(Renderer r, BeatmapMeta box)
    {
        var selectedColor = box.IsHovered
            ? Color4.Deeppink
            : Color4.Darkorange;

        r.DrawTexture(_menuButtonBackground,
            box.Position.X,
            box.Position.Y,
            box.Size.X,
            box.Size.Y,
            selectedColor
        );

        var thumbWidth = BoxHeight * 1.5f;
        
        var thumb = Renderer.WhiteTexture;
        r.DrawTexture(
            thumb,
            box.Position.X,
            box.Position.Y,
            thumbWidth,
            BoxHeight,
            new Color4<Rgba>(1, 1, 1, 1)
        );
        
        // draw text
        r.DrawText(Fonts.Default, box.Beatmap.ToString(),
            new Vector2(box.Position.X + 100 + thumbWidth, box.Position.Y + 25),
            box.Size.Y/3, new Color4<Rgba>(1, 1, 1, 1));
        r.DrawText(Fonts.Default, $"By: {box.Beatmap.Creator}",
            new Vector2(box.Position.X + 100 + thumbWidth, box.Position.Y + 65),
            box.Size.Y/3, new Color4<Rgba>(1, 1, 1, 1));
        
        r.FlushText(Fonts.Default);
    }

    public override void OnEnter()
    {
        base.OnEnter();
        LoadBeatmaps();
        foreach (var meta in _beatmapMetas)
        {
            meta.LoadTexture();
        }
        _menuBackground = new Texture(Resources.GetPath("Resources/Textures/default/menu-background@2x.png"));
        _menuButtonBackground = new Texture(Resources.GetPath("Resources/Textures/default/menu-button-background@2x.png"));
        _menuClickAudio = AudioManager.Instance.LoadAudio(Resources.GetPath("Resources/Textures/default/menuclick.wav"));
        _menuBackAudio = AudioManager.Instance.LoadAudio(Resources.GetPath("Resources/Textures/default/menuback.wav"));
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
            _menuBackground.Dispose();
            _menuButtonBackground.Dispose();
            _menuClickAudio.Dispose();
            _menuBackAudio.Dispose();
        }
    }

    public new void Dispose()
    {
        Dispose(true);
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    public override void OnMouseDown(MouseButton button, MouseEventArgs e)
    {
        base.OnMouseDown(button, e);
        foreach (var beatmapMeta in _beatmapMetas)
        {
            if (beatmapMeta.IsHovered) return;
        }
    }

    public override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        foreach (var box in _beatmapMetas)
        {
            var wasHovering = box.IsHovered;
            box.IsHovered = false;
            RectangleF collision = new(box.Position.X, box.Position.Y, box.Size.X, box.Size.Y);
            if (collision.Contains(Input.MouseX, Input.MouseY))
            {
                if (!wasHovering) AudioManager.Instance.PlaySample(_menuClickAudio);
                box.IsHovered = true;
            }
        }
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