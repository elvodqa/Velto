using System.Drawing;
using ManagedBass;
using OpenTK.Mathematics;
using SDL;
using Velto.Core;
using Velto.Gameplay;
using Velto.Graphics;

namespace Velto.Views;

public class SongSelectorView : View
{
    public enum PanelState
    {
        Open,
        Closed,
    }
    public class BeatmapBox
    {
        public Texture Texture;
        public Beatmap Beatmap;
        public Vector2 Position;
        public Vector2 Size;
        public bool IsHovered = false;
    }
    
    private Renderer _renderer;

    private float _maxWidth = 600;
    private float _currentWidth = 0;
    private PanelState _state = PanelState.Closed;
    
    private float _progress; // 0 = closed, 1 = open
    private const float Duration = 0.3f;
    private MSDFFont _font;
    private float _cursor = 0;
    private List<BeatmapBox> _beatmapBoxes = new();
    private GameView _gameView;
    private float _totalContentHeight;
    
    
    public SongSelectorView(Renderer renderer, GameView gameview)
    {
        _gameView = gameview;
        _renderer = renderer;
        _font = MSDFFont.Load(Resources.GetPath("Resources/Fonts/arial/arial"));
        LoadBeatmaps();
    }
   
    public override void Update(double delta)
    {
        _maxWidth = Width / 1.5f;
        
        float step = ((float)delta/1000) / Duration;

        if (_state == PanelState.Open)
        {
            _progress = MathF.Min(_progress + step, 1f);
        }
        else
        {
            _progress = MathF.Max(_progress - step, 0f);
        }

        float eased = _state == PanelState.Open
            ? EasingFunctions.OutCubic(_progress)
            : EasingFunctions.InCubic(_progress);
        
        _currentWidth = _maxWidth * eased;
        
        if (_state == PanelState.Open) _cursor += Input.WheelY * 20;

        float itemHeight = 150f;
        float gap = 2f;
        _totalContentHeight = _beatmapBoxes.Count * itemHeight + Math.Max(0, _beatmapBoxes.Count - 1) * gap;
        float maxScroll = Math.Max(0, _totalContentHeight - Height);
        
        _cursor = Math.Clamp(_cursor, -maxScroll, 0);
    
        if (_state == PanelState.Open)
        {
            var height = 150;
            var i = 0;

            foreach (var box in _beatmapBoxes)
            {
                box.Position = new Vector2(Width-_currentWidth, i * (height + gap) + _cursor);
                box.Size = new Vector2(_currentWidth, height);
                i++;
                
                box.IsHovered = false;
                RectangleF collision = new(box.Position.X, box.Position.Y, box.Size.X, box.Size.Y);
                if (collision.Contains(Input.MouseX, Input.MouseY))
                {
                    box.IsHovered = true;
                    if (Input.IsMouseJustPressed(SDLButton.SDL_BUTTON_LEFT))
                    {
                        _gameView.SetBeatmap(box.Beatmap);
                        Toggle();
                    }
                }
            }
        }

        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_F5))
        {
            foreach (var box in _beatmapBoxes)
            {
                box.Texture.Dispose();
            }
            _beatmapBoxes.Clear();
            LoadBeatmaps();
        }
    }

    public override void Draw(double delta)
    {
        if (_currentWidth == 0) return;

        var boundingBox = new Vector4(
            Width - _currentWidth,
            0, _currentWidth, Height
            );
        
        _renderer.SetScissor((int)boundingBox.X, (int)boundingBox.Y, (int)boundingBox.Z, (int)boundingBox.W);
        _renderer.DrawRectangle(boundingBox.X, boundingBox.Y, boundingBox.Z, boundingBox.W, new Vector4(34f/255, 39f/255, 33f/255, 0.95f));
        
        foreach (var box in _beatmapBoxes)
        {
            var color = new Vector4(0.3f, 0.3f, 0.3f, 0.2f);
            if (box.IsHovered)
            {
                color = new Vector4(0.6f, 0.6f, 0.6f, 0.2f);
            }

            _renderer.DrawRectangle(box.Position.X, box.Position.Y, box.Size.X, box.Size.Y, color);
            _renderer.DrawTexture(box.Texture, box.Position.X, box.Position.Y, 230, 150, new Vector4(1, 1, 1, 1));
            _renderer.DrawText(_font, box.Beatmap.ToString(),
                new Vector2(box.Position.X + 25 + 230, box.Position.Y + 25),
                0.5f, new Vector4(1, 1, 1, 1));
            _renderer.DrawText(_font, $"By: {box.Beatmap.Creator}",
                new Vector2(box.Position.X + 25 + 230, box.Position.Y + 65),
                0.5f, new Vector4(1, 1, 1, 1));
        }
        _renderer.FlushText(_font);
        
        // draw thumb (https://thorlaksson.com/2025/scrollbars-from-scratch/)
        var l_c = _totalContentHeight;
        var l_v = Height;
        var d = -_cursor;

        var l_t = l_v * (l_v / l_c); // length of thumb
        var d_t = d * (l_v / l_c); // distance of thumb

        _renderer.DrawRectangle(Width - 30, d_t, 30, l_t, new Vector4(1, 1, 1, 1));
    }
    
     
    public void Open()
    {
        _state = PanelState.Open;
    }

    public void Close()
    {
        _state = PanelState.Closed;
    }

    public void Toggle()
    {
        if (_state == PanelState.Closed) 
            Open();
        else 
            Close();
    }

    private void LoadBeatmaps()
    {
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
                    box.Texture = new Texture(Path.Combine( box.Beatmap.Folder,  box.Beatmap.BackgroundFile));
                    _beatmapBoxes.Add(box);
                }
            }
        }
    }
}