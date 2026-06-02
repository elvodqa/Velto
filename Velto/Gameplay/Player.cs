using OpenTK.Mathematics;
using OpenTK.Mathematics;
using SDL;
using Velto.Core;

namespace Velto.Gameplay;

public class Player
{
    public Vector2 Cursor
    {
        get
        {
            if (Autoplay)
            {
                // var _posX = playfieldTopLeft.X + _player.Cursor.X * scale;
                // var _posY = playfieldTopLeft.Y + _player.Cursor.Y * scale;
                return _playfieldOffset + _cursor * _scale;
            }
            else
            {
                return new Vector2(Input.MouseX, Input.MouseY);
            }
        }
    }

    public bool Autoplay
    {
        get { return _autoplay; }
        set
        {
            _lastAutoplayHitIndex = -1;
            _primaryLastPressed = false;
            _autoplay = value;
        }
    }

    private readonly Beatmap _beatmap;
    private int _lastAutoplayHitIndex = -1;
    private bool _autoplay = true;

    public bool ActionPrimaryPressed = false;
    public bool ActionPrimaryDown = false;
    public bool ActionSecondaryPressed = false;
    public bool ActionSecondaryDown = false;

    private bool _primaryLastPressed = false;
    private Vector2 _cursor = new Vector2();
    private Vector2 _playfieldOffset = new();
    private float _scale = 0;

    public bool Dance { get; set; } = false;


    public Player(Beatmap beatmap)
    {
        _beatmap = beatmap;

        if (_beatmap.HitObjects.Count > 0)
            _cursor = _beatmap.HitObjects[0].Position;
    }

    public void Update(double deltaTime, double songCursor, Vector2 playfieldOffset, float scale)
    {
        _playfieldOffset = playfieldOffset;
        _scale = scale;

        ActionPrimaryDown = false;
        ActionPrimaryPressed = false;
        ActionSecondaryPressed = false;
        ActionSecondaryDown = false;

        if (!_autoplay)
        {
            if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_Z)) 
                ActionPrimaryPressed = true;
            if (Input.IsKeyDown(SDL_Scancode.SDL_SCANCODE_Z)) 
                ActionPrimaryDown = true;
            if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_X)) 
                ActionSecondaryPressed = true;
            if (Input.IsKeyDown(SDL_Scancode.SDL_SCANCODE_X)) 
                ActionSecondaryDown = true;
            return;
        }

        var objects = _beatmap.HitObjects;
        if (objects.Count == 0) return;

        // Find the object we should be interacting with
        int currentIndex = -1;
        HitObject current = null;

        for (int i = 0; i < objects.Count; i++)
        {
            var obj = objects[i];

            if (obj is Slider slider)
            {
                if (songCursor >= slider.Time && songCursor <= slider.Time + slider.Duration)
                {
                    currentIndex = i;
                    current = obj;
                    break;
                }
            }

            if (songCursor < obj.Time)
            {
                currentIndex = i;
                current = obj;
                break;
            }
        }

        if (currentIndex == -1)
        {
            _cursor = objects[^1].Position;
            return;
        }

        // ====================== SLIDER ======================
        if (current is Slider activeSlider &&
            songCursor >= activeSlider.Time &&
            songCursor <= activeSlider.Time + activeSlider.Duration)
        {
            _cursor = activeSlider.GetPositionAt(songCursor);
            ActionPrimaryDown = true;

            if (_lastAutoplayHitIndex != currentIndex)
            {
                Alternate();
                _lastAutoplayHitIndex = currentIndex;
            }

            return;
        }

        // ====================== HIT CIRCLE / NEXT OBJECT ======================
        double timeToHit = current.Time - songCursor;

        // Move towards target
        _cursor = GetPositionAtTime(songCursor, currentIndex);

        // Hit timing - more reliable
        double HIT_WINDOW = 80.0 - 6.0 * _beatmap.OverallDifficulty;

        if (timeToHit <= HIT_WINDOW && timeToHit >= -20.0) // allow slight late hit
        {
            if (_lastAutoplayHitIndex != currentIndex)
            {
                Alternate();
                _lastAutoplayHitIndex = currentIndex;
            }
        }
    }

    private Vector2 GetPositionAtTime(double songCursor, int targetIndex)
    {
        var objects = _beatmap.HitObjects;
        if (targetIndex == 0)
            return objects[0].Position;

        var current = objects[targetIndex];
        var previous = objects[targetIndex - 1];

        Vector2 startPos;
        double startTime;

        if (previous is Slider prevSlider)
        {
            startTime = prevSlider.Time + prevSlider.Duration;
            startPos = prevSlider.GetPositionAt(startTime);
        }
        else
        {
            startTime = previous.Time;
            startPos = previous.Position;
        }

        double duration = current.Time - startTime;
        if (duration <= 0)
            return current.Position;

        float t = (float)((songCursor - startTime) / duration);
        t = Math.Clamp(t, 0f, 1f);
        t = t * t * (3f - 2f * t); // smoothstep

        return Vector2.Lerp(startPos, current.Position, t);
    }

    private void Alternate()
    {
        if (!_primaryLastPressed) ActionPrimaryPressed = true;
        else ActionSecondaryPressed = true;
        _primaryLastPressed = !_primaryLastPressed;
    }
}