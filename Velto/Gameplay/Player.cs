using OpenTK.Mathematics;
using OpenTK.Mathematics;
using SDL;
using Velto.Core;

namespace Velto.Gameplay;

public enum PlayerState
{
    Player,
    Autoplay,
    Replay
}

public class Player
{
    public Vector2 Cursor
    {
        get
        {
            switch (State)
            {
                case PlayerState.Player:
                    return new Vector2(Input.MouseX, Input.MouseY);
                case PlayerState.Autoplay:
                    return _playfieldOffset + _cursor * _scale;
                case PlayerState.Replay:
                    if (Replay != null)
                    {
                        var frames = Replay.Frames;

                        if (frames.Count == 0)
                            return _playfieldOffset;

                        // Advance frame index forward only (no scanning backwards)
                        while (_replayFrameIndex < frames.Count - 2 &&
                               frames[_replayFrameIndex + 1].MsSinceStart <= _songCursor)
                        {
                            _replayFrameIndex++;
                        }

                        var frame = frames[_replayFrameIndex];
                        var next = frames[_replayFrameIndex + 1];

                        double startTime = frame.MsSinceStart;
                        double endTime = next.MsSinceStart;

                        Vector2 startPos = new Vector2(frame.X, frame.Y);
                        Vector2 endPos = new Vector2(next.X, next.Y);

                        float t;

                        if (endTime <= startTime)
                        {
                            t = 0f;
                        }
                        else
                        {
                            t = (float)((_songCursor - startTime) / (endTime - startTime));
                            t = Math.Clamp(t, 0f, 1f);

                            // smoothstep for nicer motion
                            t = t * t * (3f - 2f * t);
                        }

                        var pos = Vector2.Lerp(startPos, endPos, t);

                        return _playfieldOffset + pos * _scale;
                    }
                    return new Vector2(0, 0);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    public PlayerState State { get; private set; } = PlayerState.Autoplay;
    public Replay? Replay { get; private set; } = null;
    private int _replayFrameIndex = 0;
    private double _prevSongCursor;
    private readonly Beatmap _beatmap;
    private int _lastAutoplayHitIndex = -1;

    public bool ActionPrimaryPressed = false;
    public bool ActionPrimaryDown = false;
    public bool ActionSecondaryPressed = false;
    public bool ActionSecondaryDown = false;

    private bool _prevPrimary;
    private bool _prevSecondary;

    private bool _primaryLastPressed = false;
    private Vector2 _cursor = new Vector2();
    private Vector2 _playfieldOffset = new();
    private float _scale = 0;
    private double _songCursor = 0;
    
   
    
    public bool Dance { get; set; } = false;


    public Player(Beatmap beatmap)
    {
        _beatmap = beatmap;

        if (_beatmap.HitObjects.Count > 0)
            _cursor = _beatmap.HitObjects[0].Position;
    }

    public void SetReplay(Replay replay)
    {
        Replay = replay;
    }
    
    public void SetState(PlayerState state)
    {
        State = state;
        switch (State)
        {
            case PlayerState.Player:
                _lastAutoplayHitIndex = -1;
                _primaryLastPressed = false;
                break;
            case PlayerState.Autoplay:

                break;
            case PlayerState.Replay:
                _replayFrameIndex = 0;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public void Update(double deltaTime, double songCursor, Vector2 playfieldOffset, float scale)
    {
        if (_songCursor < _prevSongCursor)
        {
            _replayFrameIndex = 0;
        }
        _prevSongCursor = _songCursor;
        
        _playfieldOffset = playfieldOffset;
        _scale = scale;
        _songCursor = songCursor;

        ActionPrimaryDown = false;
        ActionPrimaryPressed = false;
        ActionSecondaryPressed = false;
        ActionSecondaryDown = false;

        if (State == PlayerState.Player)
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

        if (State == PlayerState.Autoplay)
        {
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

        if (State == PlayerState.Replay)
        {
            if (Replay != null)
            {
                var frame = Replay.Frames.OrderBy(f => Math.Abs(f.MsSinceStart - songCursor))
                    .First();
                foreach (var keypress in frame.KeysPressed)
                {
                    if (keypress == Keypress.K1 && !_prevPrimary) ActionPrimaryPressed = true;
                    if (keypress == Keypress.K2 && !_prevSecondary) ActionSecondaryPressed = true;
                    if (keypress == Keypress.K1) ActionPrimaryDown = true;
                    if (keypress == Keypress.K2) ActionSecondaryDown = true;
                }
            }
        }

        _prevPrimary = ActionPrimaryDown;
        _prevSecondary = ActionSecondaryDown;
        
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