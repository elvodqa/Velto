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
        set { 
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
        //Console.WriteLine($"Z JustPressed: {Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_Z)}");
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
        }
        else
        {
            var objects = _beatmap.HitObjects;

            if (objects.Count == 0)
                return;

            // Find current object
            int currentIndex = -1;

            for (int i = 0; i < objects.Count; i++)
            {
                if (songCursor < objects[i].Time)
                {
                    currentIndex = i;
                    break;
                }

                if (objects[i] is Slider slider)
                {
                    if (songCursor >= slider.Time &&
                        songCursor <= slider.Time + slider.Duration)
                    {
                        currentIndex = i;
                        break;
                    }
                }
            }

            if (currentIndex == -1)
            {
                _cursor = objects[^1].Position;
                return;
            }

            var current = objects[currentIndex];

            // ---------------------------------------------------------
            // SLIDER HANDLING (follow + hold input)
            // ---------------------------------------------------------
            if (current is Slider activeSlider &&
                songCursor >= activeSlider.Time &&
                songCursor <= activeSlider.Time + activeSlider.Duration)
            {
                _cursor = activeSlider.GetPositionAt(songCursor);

                ActionPrimaryDown = true;

                // Only "press" once when entering slider
                if (_lastAutoplayHitIndex != currentIndex)
                {
                    Alternate();
                    _lastAutoplayHitIndex = currentIndex;
                }

                return;
            }

            // ---------------------------------------------------------
            // HITCIRCLE / NORMAL OBJECT HIT
            // ---------------------------------------------------------
            float hitWindow = 80f - 6f * _beatmap.OverallDifficulty; // ms window, this is right before you can get a 300
          

            if (Math.Abs(songCursor - current.Time) <= hitWindow)
            {
                _cursor = current.Position;

                if (_lastAutoplayHitIndex != currentIndex)
                {
                    Alternate();
                    _lastAutoplayHitIndex = currentIndex;
                }

                return;
            }

            // ---------------------------------------------------------
            // MOVEMENT (between objects)
            // ---------------------------------------------------------
            if (currentIndex == 0)
            {
                _cursor = current.Position;
                return;
            }

            var previous = objects[currentIndex - 1];

            Vector2 startPos;
            double startTime;

            if (previous is Slider previousSlider)
            {
                startTime = previousSlider.Time + previousSlider.Duration;
                startPos = previousSlider.GetPositionAt(startTime);
            }
            else
            {
                startPos = previous.Position;
                startTime = previous.Time;
            }

            Vector2 endPos = current.Position;
            double endTime = current.Time;

            double duration = endTime - startTime;

            if (duration <= 0)
            {
                _cursor = endPos;
                return;
            }

            float t = (float)((songCursor - startTime) / duration);
            t = Math.Clamp(t, 0f, 1f);

            t = t * t * (3f - 2f * t);

            // startPos = playfieldOffset + startPos * scale;
            // endPos = playfieldOffset + endPos * scale;
            _cursor = Vector2.Lerp(startPos, endPos, t);
        }
    }

    private void Alternate()
    {
        if (!_primaryLastPressed) ActionPrimaryPressed = true;
        else ActionSecondaryPressed = true;
        _primaryLastPressed = !_primaryLastPressed;
    }
}