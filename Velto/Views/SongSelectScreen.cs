using System.Drawing;
using OpenTK.Mathematics;
using SDL;
using Velto.Audio;
using Velto.Core;
using Velto.Game.osu;
using Velto.Graphics;

namespace Velto.Game.Views;

public class SongSelectScreen : Screen, IDisposable
{
    private const float MaxVisibleOffset = 4f;

    private static readonly Color4<Rgba> Pink = new(1f, 0.4f, 0.67f, 1f);
    private static readonly Color4<Rgba> Panel = new(0.035f, 0.06f, 0.1f, 0.82f);
    private static readonly Color4<Rgba> White = new(1f, 1f, 1f, 1f);
    private static readonly Color4<Rgba> SubText = new(1f, 1f, 1f, 0.62f);
    private static readonly Color4<Rgba> Border = new(1f, 1f, 1f, 0.12f);

    private readonly OsuContext _context;
    private readonly List<BeatmapMeta> _beatmapMetas = new();
    private readonly Dictionary<string, Texture> _textureCache = new();

    private int _cursor;
    private float _cursorDisplay;
    private double _inputCooldown;
    private bool _autoplayEnabled;
    private bool _modMenuOpen;

    private RectangleF _backButton;
    private RectangleF _modsButton;
    private RectangleF _playButton;
    private RectangleF _modPanel;
    private RectangleF _autoplayButton;
    private RectangleF _closeButton;

    private bool _backHovered;
    private bool _modsHovered;
    private bool _playHovered;
    private bool _autoplayHovered;
    private bool _closeHovered;

    public SongSelectScreen(OsuContext context) : base(context)
    {
        _context = context;
    }

    public override void Update(double dt)
    {
        UpdateLayout();

        if (_inputCooldown > 0)
            _inputCooldown -= dt;

        if (_beatmapMetas.Count > 0)
        {
            _cursor = Math.Clamp(_cursor, 0, _beatmapMetas.Count - 1);

            if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_UP))
                MoveCursor(-1);

            if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_DOWN))
                MoveCursor(1);

            if (!_modMenuOpen)
            {
                var wheel = (int)Math.Clamp(Input.WheelY * 20f, -1f, 1f);
                if (wheel != 0)
                    MoveCursor(-wheel);
            }
        }

        var ease = Math.Clamp((float)(dt / 1000.0 * 14.0), 0f, 1f);
        _cursorDisplay += (_cursor - _cursorDisplay) * ease;
        if (Math.Abs(_cursor - _cursorDisplay) < 0.001f)
            _cursorDisplay = _cursor;

        var mouse = new Vector2(Input.MouseX, Input.MouseY);

        for (var i = 0; i < _beatmapMetas.Count; i++)
        {
            var meta = _beatmapMetas[i];
            var wasHovering = meta.IsHovered;
            meta.Bounds = GetRowBounds(i);
            meta.IsHovered = !_modMenuOpen && mouse.Y < Height - Height * 0.085f &&
                             meta.Bounds.Contains(mouse.X, mouse.Y);

            if (meta.IsHovered && !wasHovering && _inputCooldown <= 0)
                AudioManager.Instance.PlaySample(_context.Skin.MenuClick);
        }

        _backHovered = !_modMenuOpen && Contains(_backButton, mouse);
        _modsHovered = !_modMenuOpen && Contains(_modsButton, mouse);
        _playHovered = !_modMenuOpen && Contains(_playButton, mouse);
        _autoplayHovered = _modMenuOpen && Contains(_autoplayButton, mouse);
        _closeHovered = _modMenuOpen && Contains(_closeButton, mouse);

        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_ESCAPE))
        {
            if (_modMenuOpen)
            {
                _modMenuOpen = false;
                AudioManager.Instance.PlaySample(_context.Skin.MenuBack);
            }
            else
            {
                AudioManager.Instance.PlaySample(_context.Skin.MenuBack);
                Transition(new IntroScreen(_context), 200);
            }
        }

        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_F2))
        {
            _modMenuOpen = !_modMenuOpen;
            AudioManager.Instance.PlaySample(_context.Skin.MenuClick);
        }

        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_RETURN) && !_modMenuOpen)
            PlaySelected();

        if (_inputCooldown <= 0 && Input.IsMouseJustPressed(SDLButton.SDL_BUTTON_LEFT))
            HandleClick(mouse);
    }

    public override void Draw(Renderer r)
    {
        r.PushScissor(0, 0, Width, Height);

        DrawBackground(r);
        DrawHeader(r);
        DrawInfoPanel(r);
        DrawCarousel(r);
        DrawFooter(r);
        r.FlushText(Fonts.Default);

        if (_modMenuOpen)
        {
            DrawModMenu(r);
            r.FlushText(Fonts.Default);
        }

        r.PopScissor();
    }

    private void UpdateLayout()
    {
        var footerHeight = Height * 0.085f;
        var footerY = Height - footerHeight;
        var padding = Width * 0.018f;
        var buttonHeight = footerHeight * 0.58f;
        var buttonY = footerY + (footerHeight - buttonHeight) / 2f;

        _backButton = new RectangleF(padding, buttonY, Width * 0.13f, buttonHeight);
        _modsButton = new RectangleF(padding * 2f + Width * 0.13f, buttonY, Width * 0.17f, buttonHeight);
        _playButton = new RectangleF(Width - padding - Width * 0.18f, buttonY, Width * 0.18f, buttonHeight);

        var panelWidth = Width * 0.38f;
        var panelHeight = Height * 0.5f;
        var panelX = (Width - panelWidth) / 2f;
        var panelY = (Height - panelHeight) / 2f;
        var inner = panelWidth * 0.07f;

        _modPanel = new RectangleF(panelX, panelY, panelWidth, panelHeight);
        _autoplayButton = new RectangleF(panelX + inner, panelY + panelHeight * 0.28f,
            panelWidth - inner * 2f, panelHeight * 0.18f);
        _closeButton = new RectangleF(panelX + inner, panelY + panelHeight * 0.78f,
            panelWidth - inner * 2f, panelHeight * 0.12f);
    }

    private void DrawBackground(Renderer r)
    {
        var texture = _context.Skin.MenuBackground;

        if (_beatmapMetas.Count > 0)
            texture = GetBackgroundTexture(_beatmapMetas[_cursor]) ?? texture;

        DrawCover(r, texture, 0, 0, Width, Height, White);
        r.DrawRectangle(0, 0, Width, Height, new Color4<Rgba>(0.01f, 0.02f, 0.05f, 0.72f));
        r.DrawRectangle(0, 0, Width * 0.58f, Height, new Color4<Rgba>(0.01f, 0.02f, 0.04f, 0.22f));
    }

    private void DrawHeader(Renderer r)
    {
        r.DrawText(Fonts.Default, "song select", new Vector2(Width * 0.055f, Height * 0.055f),
            Height / 28f, White);

        var count = _beatmapMetas.Count == 0 ? "0 / 0" : $"{_cursor + 1} / {_beatmapMetas.Count}";
        var countSize = r.MeasureText(Fonts.Default, count, Height / 38f);
        r.DrawText(Fonts.Default, count, new Vector2(Width * 0.945f - countSize.X, Height * 0.07f),
            Height / 38f, SubText);

        r.DrawRectangle(Width * 0.055f, Height * 0.115f, Width * 0.07f, Math.Max(2f, Height / 240f), Pink);
    }

    private void DrawInfoPanel(Renderer r)
    {
        if (_beatmapMetas.Count == 0)
        {
            r.DrawTextCentered(Fonts.Default, "No beatmaps found", new Vector2(Width / 2f, Height / 2f),
                Height / 24f, SubText);
            return;
        }

        var beatmap = _beatmapMetas[_cursor].Beatmap;
        var x = Width * 0.055f;
        var y = Height * 0.16f;
        var width = Width * 0.36f;
        var height = Height * 0.56f;
        var padding = width * 0.06f;
        var textX = x + padding;
        var textWidth = width - padding * 2f;

        r.DrawRectangle(x, y, width, height, Panel);
        r.DrawRectangle(x, y, width, Math.Max(3f, Height * 0.006f), Pink);
        r.DrawRectangleBorder(x, y, width, height, Math.Max(1f, Height / 900f), Border);

        var titleSize = Height / 24f;
        var artistSize = Height / 30f;
        var metaSize = Height / 40f;
        var textY = y + padding;

        var titleHeight = r.MeasureTextWrapped(Fonts.Default, beatmap.Title, titleSize, textWidth);
        r.DrawTextWrapped(Fonts.Default, beatmap.Title, new Vector2(textX, textY), titleSize, textWidth, White);
        textY += titleHeight.Y + Height * 0.008f;

        r.DrawText(Fonts.Default, FitText(r, beatmap.Artist, artistSize, textWidth),
            new Vector2(textX, textY), artistSize, SubText);
        textY += artistSize * 1.35f;

        r.DrawText(Fonts.Default, FitText(r, $"[{beatmap.Version}]", metaSize, textWidth),
            new Vector2(textX, textY), metaSize, White with { W = 0.85f });
        textY += metaSize * 1.5f;

        r.DrawText(Fonts.Default, FitText(r, $"mapped by {beatmap.Creator}", metaSize, textWidth),
            new Vector2(textX, textY), metaSize, SubText);

        var statsY = Math.Max(y + height * 0.6f, textY + Height * 0.03f);
        r.DrawRectangle(textX, statsY - Height * 0.016f, textWidth, Math.Max(1f, Height / 900f), Border);

        var bpm = GetBpm(beatmap);
        var length = GetLength(beatmap);
        var stats =
            $"CS {beatmap.CircleSize:0.0}    AR {beatmap.ApproachRate:0.0}\n" +
            $"OD {beatmap.OverallDifficulty:0.0}    HP {beatmap.HPDrainRate:0.0}\n" +
            $"BPM {bpm}    Length {FormatTime(length)}";

        r.DrawText(Fonts.Default, stats, new Vector2(textX, statsY), metaSize, SubText);
    }

    private void DrawCarousel(Renderer r)
    {
        if (_beatmapMetas.Count == 0) return;

        var visible = new List<int>();

        for (var i = 0; i < _beatmapMetas.Count; i++)
        {
            if (Math.Abs(i - _cursorDisplay) > MaxVisibleOffset)
                continue;

            var bounds = GetRowBounds(i);
            if (bounds.Bottom < 0 || bounds.Top > Height)
                continue;

            visible.Add(i);
        }

        foreach (var index in visible.OrderByDescending(i => Math.Abs(i - _cursorDisplay)))
            DrawRow(r, index);
    }

    private void DrawRow(Renderer r, int index)
    {
        var meta = _beatmapMetas[index];
        var bounds = GetRowBounds(index);
        var distance = Math.Abs(index - _cursorDisplay);
        var alpha = Math.Clamp(1f - distance * 0.19f, 0.18f, 1f);
        var selected = index == _cursor;
        var hovered = meta.IsHovered;

        var background = selected
            ? new Color4<Rgba>(0.1f, 0.16f, 0.26f, 0.94f)
            : new Color4<Rgba>(0.05f, 0.08f, 0.13f, 0.8f);

        if (hovered)
            background = selected
                ? new Color4<Rgba>(0.13f, 0.2f, 0.33f, 0.96f)
                : new Color4<Rgba>(0.09f, 0.14f, 0.22f, 0.9f);

        r.DrawRectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height,
            background with { W = background.W * alpha });

        var border = selected || hovered
            ? Pink with { W = alpha * 0.8f }
            : new Color4<Rgba>(1f, 1f, 1f, 0.08f * alpha);

        r.DrawRectangleBorder(bounds.X, bounds.Y, bounds.Width, bounds.Height,
            Math.Max(1f, Height / 900f), border);

        if (selected || hovered)
            r.DrawRectangle(bounds.X, bounds.Y, Math.Max(3f, bounds.Height * 0.035f), bounds.Height,
                Pink with { W = alpha });

        var padding = bounds.Height * 0.14f;
        var thumbSize = bounds.Height - padding * 2f;
        var thumb = new RectangleF(bounds.X + padding, bounds.Y + padding, thumbSize, thumbSize);

        r.DrawRectangle(thumb.X, thumb.Y, thumb.Width, thumb.Height,
            new Color4<Rgba>(0f, 0f, 0f, 0.35f * alpha));

        if (distance <= 2.2f)
        {
            var texture = GetBackgroundTexture(meta);

            if (texture != null)
            {
                r.PushScissor(thumb.X, thumb.Y, thumb.Width, thumb.Height);
                DrawCover(r, texture, thumb.X, thumb.Y, thumb.Width, thumb.Height,
                    new Color4<Rgba>(1f, 1f, 1f, alpha));
                r.PopScissor();
            }
        }

        var textX = thumb.Right + padding * 1.2f;
        var maxTextWidth = bounds.Right - textX - padding;
        var titleSize = bounds.Height * 0.3f;
        var subSize = bounds.Height * 0.19f;

        r.DrawText(Fonts.Default, FitText(r, meta.Beatmap.Title, titleSize, maxTextWidth),
            new Vector2(textX, bounds.Y + bounds.Height * 0.16f), titleSize,
            new Color4<Rgba>(1f, 1f, 1f, alpha));

        r.DrawText(Fonts.Default,
            FitText(r, $"{meta.Beatmap.Artist}  //  {meta.Beatmap.Version}", subSize, maxTextWidth),
            new Vector2(textX, bounds.Y + bounds.Height * 0.52f), subSize,
            new Color4<Rgba>(1f, 1f, 1f, alpha * 0.7f));

        if (selected)
        {
            r.DrawText(Fonts.Default,
                FitText(r, $"mapped by {meta.Beatmap.Creator}", bounds.Height * 0.15f, maxTextWidth),
                new Vector2(textX, bounds.Y + bounds.Height * 0.74f), bounds.Height * 0.15f,
                new Color4<Rgba>(1f, 1f, 1f, alpha * 0.55f));
        }
    }

    private void DrawFooter(Renderer r)
    {
        var footerHeight = Height * 0.085f;
        var footerY = Height - footerHeight;

        r.DrawRectangle(0, footerY, Width, footerHeight, new Color4<Rgba>(0.02f, 0.035f, 0.06f, 0.92f));
        r.DrawRectangle(0, footerY, Width, Math.Max(1f, Height / 900f), Border);

        DrawFooterButton(r, _backButton, "Back", "ESC", _backHovered, false);
        DrawFooterButton(r, _modsButton, "Mods", _autoplayEnabled ? "AUTOPLAY ON" : "F2",
            _modsHovered, _autoplayEnabled);
        DrawFooterButton(r, _playButton, "Play Selected", "ENTER", _playHovered, true);
    }

    private void DrawFooterButton(Renderer r, RectangleF rect, string label, string hint, bool hovered,
        bool accent)
    {
        var background = accent
            ? new Color4<Rgba>(1f, 0.4f, 0.67f, hovered ? 0.35f : 0.18f)
            : new Color4<Rgba>(1f, 1f, 1f, hovered ? 0.1f : 0.035f);

        r.DrawRectangle(rect.X, rect.Y, rect.Width, rect.Height, background);
        r.DrawRectangleBorder(rect.X, rect.Y, rect.Width, rect.Height, Math.Max(1f, Height / 900f),
            accent || hovered ? Pink with { W = 0.75f } : new Color4<Rgba>(1f, 1f, 1f, 0.1f));

        r.DrawTextCentered(Fonts.Default, label,
            new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height * 0.4f),
            rect.Height * 0.36f, White);

        r.DrawTextCentered(Fonts.Default, hint,
            new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height * 0.74f),
            rect.Height * 0.2f, SubText);
    }

    private void DrawModMenu(Renderer r)
    {
        r.DrawRectangle(0, 0, Width, Height, new Color4<Rgba>(0f, 0f, 0f, 0.62f));

        var panel = _modPanel;
        var padding = panel.Width * 0.07f;

        r.DrawRectangle(panel.X, panel.Y, panel.Width, panel.Height,
            new Color4<Rgba>(0.04f, 0.07f, 0.12f, 0.97f));
        r.DrawRectangle(panel.X, panel.Y, panel.Width, Math.Max(4f, Height * 0.008f), Pink);
        r.DrawRectangleBorder(panel.X, panel.Y, panel.Width, panel.Height,
            Math.Max(1f, Height / 720f), Pink with { W = 0.45f });

        r.DrawText(Fonts.Default, "Mods", new Vector2(panel.X + padding, panel.Y + panel.Height * 0.08f),
            panel.Height * 0.09f, White);

        r.DrawText(Fonts.Default, "Song select modifiers",
            new Vector2(panel.X + padding, panel.Y + panel.Height * 0.16f),
            panel.Height * 0.045f, SubText);

        DrawModToggle(r, _autoplayButton, "Autoplay", _autoplayEnabled ? "ON" : "OFF",
            _autoplayHovered, _autoplayEnabled);

        r.DrawText(Fonts.Default, "Automatically plays the selected beatmap.",
            new Vector2(panel.X + padding, _autoplayButton.Bottom + panel.Height * 0.04f),
            panel.Height * 0.045f, SubText);

        DrawFooterButton(r, _closeButton, "Close", "F2 / ESC", _closeHovered, false);
    }

    private void DrawModToggle(Renderer r, RectangleF rect, string label, string value, bool hovered,
        bool active)
    {
        var background = active
            ? new Color4<Rgba>(1f, 0.4f, 0.67f, hovered ? 0.3f : 0.16f)
            : new Color4<Rgba>(1f, 1f, 1f, hovered ? 0.08f : 0.03f);

        r.DrawRectangle(rect.X, rect.Y, rect.Width, rect.Height, background);
        r.DrawRectangleBorder(rect.X, rect.Y, rect.Width, rect.Height, Math.Max(1f, Height / 900f),
            active || hovered ? Pink with { W = 0.75f } : new Color4<Rgba>(1f, 1f, 1f, 0.1f));

        var labelSize = rect.Height * 0.38f;
        var valueSize = rect.Height * 0.34f;
        var labelWidth = r.MeasureText(Fonts.Default, label, labelSize);
        var valueWidth = r.MeasureText(Fonts.Default, value, valueSize);

        r.DrawText(Fonts.Default, label,
            new Vector2(rect.X + rect.Width * 0.04f,
                rect.Y + (rect.Height - labelSize) / 2f), labelSize, White);

        r.DrawText(Fonts.Default, value,
            new Vector2(rect.Right - valueWidth.X - rect.Width * 0.04f,
                rect.Y + (rect.Height - valueSize) / 2f), valueSize, active ? Pink : SubText);
    }

    private RectangleF GetRowBounds(int index)
    {
        var offset = index - _cursorDisplay;
        var distance = Math.Abs(offset);
        var selectedHeight = Height * 0.118f;
        var selectedWidth = Width * 0.5f;
        var heightFactor = 1f - Math.Min(distance, MaxVisibleOffset) * 0.16f;
        var widthFactor = 1f - Math.Min(distance, MaxVisibleOffset) * 0.08f;
        var height = selectedHeight * heightFactor;
        var width = selectedWidth * widthFactor;
        var pitch = selectedHeight * 1.045f;
        var right = Width * 0.95f;
        var contentBottom = Height - Height * 0.085f;
        var centerY = contentBottom * 0.5f;
        var x = right - width;
        var y = centerY + offset * pitch - height / 2f;

        return new RectangleF(x, y, width, height);
    }

    private void MoveCursor(int delta)
    {
        if (_beatmapMetas.Count == 0) return;

        var next = Math.Clamp(_cursor + delta, 0, _beatmapMetas.Count - 1);
        if (next == _cursor) return;

        _cursor = next;
        AudioManager.Instance.PlaySample(_context.Skin.MenuClick);
    }

    private void HandleClick(Vector2 mouse)
    {
        if (_modMenuOpen)
        {
            if (Contains(_autoplayButton, mouse))
            {
                _autoplayEnabled = !_autoplayEnabled;
                AudioManager.Instance.PlaySample(_context.Skin.MenuClick);
                return;
            }

            if (Contains(_closeButton, mouse) || !Contains(_modPanel, mouse))
            {
                _modMenuOpen = false;
                AudioManager.Instance.PlaySample(_context.Skin.MenuBack);
            }

            return;
        }

        if (Contains(_backButton, mouse))
        {
            AudioManager.Instance.PlaySample(_context.Skin.MenuBack);
            Transition(new IntroScreen(_context), 200);
            return;
        }

        if (Contains(_modsButton, mouse))
        {
            _modMenuOpen = true;
            AudioManager.Instance.PlaySample(_context.Skin.MenuClick);
            return;
        }

        if (Contains(_playButton, mouse))
        {
            PlaySelected();
            return;
        }

        if (mouse.Y >= Height - Height * 0.085f) return;

        var index = _beatmapMetas.FindIndex(m => m.IsHovered);
        if (index < 0) return;

        if (index == _cursor)
        {
            PlaySelected();
            return;
        }

        _cursor = index;
        AudioManager.Instance.PlaySample(_context.Skin.MenuClick);
    }

    private void PlaySelected()
    {
        if (_beatmapMetas.Count == 0) return;
        PlayBeatmap(_beatmapMetas[_cursor].Beatmap);
    }

    private void PlayBeatmap(Beatmap beatmap)
    {
        AudioManager.Instance.PlaySample(_context.Skin.MenuClick);
        var game = new GameScreen(_context);
        game.SetBeatmap(beatmap);
        game.Player.SetState(_autoplayEnabled ? PlayerState.Autoplay : PlayerState.Player);
        Transition(game, 200);
    }

    private Texture? GetBackgroundTexture(BeatmapMeta meta)
    {
        var path = meta.BackgroundPath;

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

        if (_textureCache.TryGetValue(path, out var texture))
            return texture;

        try
        {
            texture = new Texture(path);
            _textureCache[path] = texture;
            return texture;
        }
        catch
        {
            return null;
        }
    }

    private static void DrawCover(Renderer r, Texture texture, float x, float y, float width, float height,
        Color4<Rgba> color)
    {
        if (texture.Width <= 0 || texture.Height <= 0) return;

        var scale = Math.Max(width / texture.Width, height / texture.Height);
        var drawWidth = texture.Width * scale;
        var drawHeight = texture.Height * scale;

        r.DrawTexture(texture, x + (width - drawWidth) / 2f, y + (height - drawHeight) / 2f,
            drawWidth, drawHeight, color);
    }

    private static bool Contains(RectangleF rect, Vector2 point)
    {
        return rect.Contains(point.X, point.Y);
    }

    private static string FitText(Renderer r, string text, float lineHeight, float maxWidth)
    {
        if (maxWidth <= 0) return string.Empty;
        if (r.MeasureText(Fonts.Default, text, lineHeight).X <= maxWidth) return text;

        var value = text;
        while (value.Length > 0 &&
               r.MeasureText(Fonts.Default, value + "...", lineHeight).X > maxWidth)
        {
            value = value[..^1];
        }

        return value.TrimEnd() + "...";
    }

    private static int GetBpm(Beatmap beatmap)
    {
        var timing = beatmap.TimingPoints
            .OrderBy(t => t.Time)
            .FirstOrDefault(t => t.Uninherited == 1 && t.BeatLength > 0);

        return timing.BeatLength > 0 ? (int)Math.Round(60000.0 / timing.BeatLength) : 0;
    }

    private static double GetLength(Beatmap beatmap)
    {
        return beatmap.HitObjects.Count == 0 ? 0 : beatmap.HitObjects[^1].Time;
    }

    private static string FormatTime(double milliseconds)
    {
        var time = TimeSpan.FromMilliseconds(Math.Max(0, milliseconds));
        return $"{(int)time.TotalMinutes}:{time.Seconds:00}";
    }

    public override void OnEnter()
    {
        base.OnEnter();
        LoadBeatmaps();
        _inputCooldown = 300;

        _cursor = Math.Clamp(_cursor, 0, Math.Max(0, _beatmapMetas.Count - 1));
        _cursorDisplay = _cursor;

        if (_beatmapMetas.Count > 0)
            GetBackgroundTexture(_beatmapMetas[_cursor]);
    }

    public override void OnExit()
    {
        base.OnExit();
        Dispose();
    }

    public void LoadBeatmaps()
    {
        if (_beatmapMetas.Count > 0) return;

        var songDirs = Directory.GetDirectories(Resources.GetPath("Resources/Songs"));

        foreach (var dir in songDirs)
        {
            foreach (var file in Directory.GetFiles(dir))
            {
                if (Path.GetExtension(file) != ".osu") continue;

                var beatmap = new Beatmap(file);
                if (beatmap.Mode != 0) continue;

                var backgroundPath = string.IsNullOrWhiteSpace(beatmap.BackgroundFile)
                    ? null
                    : Path.Combine(beatmap.Folder, beatmap.BackgroundFile);

                _beatmapMetas.Add(new BeatmapMeta
                {
                    Beatmap = beatmap,
                    BackgroundPath = backgroundPath,
                });
            }
        }

        _beatmapMetas.Sort((a, b) =>
        {
            var artist = string.Compare(a.Beatmap.Artist, b.Beatmap.Artist, StringComparison.OrdinalIgnoreCase);
            if (artist != 0) return artist;

            var title = string.Compare(a.Beatmap.Title, b.Beatmap.Title, StringComparison.OrdinalIgnoreCase);
            if (title != 0) return title;

            return string.Compare(a.Beatmap.Version, b.Beatmap.Version, StringComparison.OrdinalIgnoreCase);
        });
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;

        foreach (var texture in _textureCache.Values)
            texture.Dispose();

        _textureCache.Clear();
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

    private class BeatmapMeta
    {
        public Beatmap Beatmap = null!;
        public string? BackgroundPath;
        public RectangleF Bounds;
        public bool IsHovered;
    }
}
