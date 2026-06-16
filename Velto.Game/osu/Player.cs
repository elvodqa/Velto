using OpenTK.Mathematics;
using SDL;
using Velto.Core;
using Velto.Game.Views;
using Velto.Graphics;

namespace Velto.Game.osu;

public enum PlayerState
{
    Player,
    Autoplay,
    Replay
}

public class Player
{
    public Vector2 Cursor => GetCurrentCursor();
    
    public PlayerState State { get; private set; } = PlayerState.Autoplay;
    public Replay? Replay { get; private set; } = null;

    public bool ActionPrimaryPressed { get; private set; }
    public bool ActionPrimaryDown { get; private set; }
    public bool ActionSecondaryPressed { get; private set; }
    public bool ActionSecondaryDown { get; private set; }

    public bool Dance { get; set; } = false;

    private readonly Beatmap _beatmap;
    private readonly GameScreen _gameScreen;

    // Replay fields
    private int _replayFrameIndex = 0;
    private double _songCursor = 0;
    private Vector2 _playfieldOffset = Vector2.Zero;
    private float _scale = 1f;

    // Autoplay fields
    private int _lastAutoplayHitIndex = -1;
    private Vector2 _autoplayCursor = Vector2.Zero;
    private bool _primaryLastPressed = false;

    // Input state tracking
    private bool _prevPrimary;
    private bool _prevSecondary;

    public Player(Beatmap beatmap, GameScreen screen)
    {
        _beatmap = beatmap;
        _gameScreen = screen;

        if (_beatmap.HitObjects.Count > 0)
            _autoplayCursor = _beatmap.HitObjects[0].Position;
    }

    public void SetReplay(Replay replay)
    {
        Replay = replay;
        
        _replayFrameIndex = 0;
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
                _lastAutoplayHitIndex = -1;
                break;

            case PlayerState.Replay:
                _replayFrameIndex = 0;
                break;
        }
    }

    /// <summary>
    /// Main update method
    /// </summary>
    public void Update(double deltaTime, double songCursor, Vector2 playfieldOffset, float scale)
    {
        bool isRollback = songCursor < _songCursor;   // Detect seeking backwards
        
        _songCursor = songCursor;
        _playfieldOffset = playfieldOffset;
        _scale = scale;

        // Reset input states
        ActionPrimaryPressed = false;
        ActionPrimaryDown = false;
        ActionSecondaryPressed = false;
        ActionSecondaryDown = false;
        
        if (isRollback)
        {
            if (State == PlayerState.Replay)
                HandleReplayRollback();
            else if (State == PlayerState.Autoplay)
                HandleAutoplayRollback();
        }

        switch (State)
        {
            case PlayerState.Player:
                UpdatePlayerInput();
                break;

            case PlayerState.Autoplay:
                UpdateAutoplay();
                break;

            case PlayerState.Replay:
                UpdateReplay();
                break;
        }

        // NOTE: _prevPrimary/_prevSecondary are only meaningful for replays and are
        // advanced per processed frame inside UpdateReplay(). They must NOT be clobbered
        // here, otherwise an update that processes no new replay frame would reset the
        // held-key state and make the next frame look like a fresh press.
    }

    private void UpdatePlayerInput()
    {
        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_Z))
            ActionPrimaryPressed = true;
        if (Input.IsKeyDown(SDL_Scancode.SDL_SCANCODE_Z))
            ActionPrimaryDown = true;

        if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_X))
            ActionSecondaryPressed = true;
        if (Input.IsKeyDown(SDL_Scancode.SDL_SCANCODE_X))
            ActionSecondaryDown = true;
        
        _gameScreen.Judge(_gameScreen.clock.CurrentTime);
    }

    private void UpdateAutoplay()
    {
        double hitWindow = 80.0 - 6.0 * _beatmap.OverallDifficulty;

        var objects = _beatmap.HitObjects;
        if (objects.Count == 0) return;

        // --- Judging: process ALL objects whose hit time has arrived ---
        // Uses a replay-style while loop so no object is skipped when
        // songCursor jumps past several objects in a single update.
        while (_lastAutoplayHitIndex + 1 < objects.Count)
        {
            int nextIndex = _lastAutoplayHitIndex + 1;
            var nextObj = objects[nextIndex];

            if (_songCursor >= nextObj.Time - hitWindow)
            {
                Alternate(nextObj);
                _gameScreen.Judge(nextObj.Time);
                _lastAutoplayHitIndex = nextIndex;

                // If we just hit an active slider, stop here — it needs to be
                // held through its duration before we advance to the next object.
                if (nextObj is Slider s && _songCursor <= s.Time + s.Duration)
                    break;
            }
            else
            {
                break;
            }
        }

        // --- Cursor movement: find the current object based on song position ---
        // This is independent of the judging index so the cursor always
        // reflects the correct spatial position regardless of early judges.
        int cursorIndex = -1;
        for (int i = 0; i < objects.Count; i++)
        {
            var obj = objects[i];
            if (_songCursor < obj.Time)
            {
                cursorIndex = i;
                break;
            }
            if (obj is Slider slider &&
                _songCursor >= slider.Time &&
                _songCursor <= slider.Time + slider.Duration)
            {
                cursorIndex = i;
                break;
            }
        }

        if (cursorIndex == -1)
        {
            _autoplayCursor = objects[^1].Position;
            return;
        }

        var current = objects[cursorIndex];

        // Active slider hold
        if (current is Slider activeSlider &&
            _songCursor >= activeSlider.Time &&
            _songCursor <= activeSlider.Time + activeSlider.Duration)
        {
            int nextIdx = cursorIndex + 1;
            if (nextIdx < objects.Count &&
                objects[nextIdx].Time - _songCursor <= 100)
            {
                ActionPrimaryDown = false;
                return;
            }

            _autoplayCursor = activeSlider.GetPositionAt(_songCursor);
            ActionPrimaryDown = true;
            return;
        }

        // Move toward the upcoming object
        _autoplayCursor = GetPositionAtTime(_songCursor, cursorIndex);
    }
    
    private void HandleAutoplayRollback()
    {
        // Recalculate the last-hit index based on current song position.
        // Without this, seeking backwards during autoplay would cause
        // objects to be skipped because _lastAutoplayHitIndex stays stale.
        var objects = _beatmap.HitObjects;
        _lastAutoplayHitIndex = -1;
        while (_lastAutoplayHitIndex + 1 < objects.Count &&
               objects[_lastAutoplayHitIndex + 1].Time <= _songCursor)
        {
            _lastAutoplayHitIndex++;
        }
    }

    private void HandleReplayRollback()
    {
        // Reset to beginning when going backwards
        if (_songCursor <= 0)
        {
            _replayFrameIndex = 0;
            return;
        }

        // Find the correct frame for current time (binary search would be better for large replays)
        var frames = Replay!.Frames;
        _replayFrameIndex = 0;

        while (_replayFrameIndex < frames.Count - 1 && 
               frames[_replayFrameIndex + 1].MsSinceStart <= _songCursor)
        {
            _replayFrameIndex++;
        }

        // Restart edge detection cleanly after a seek so the first frame we re-process
        // doesn't get treated as a spurious key press.
        _prevPrimary = false;
        _prevSecondary = false;
    }

    private void UpdateReplay()
    {
        if (Replay == null || Replay.Frames.Count == 0)
            return;

        var frames = Replay.Frames;

        // Normal forward advancement.
        // Each replay frame is judged at its own timestamp. Key edges ("just pressed") are
        // computed against the previously *processed* frame and the per-frame state is
        // advanced inside the loop, so a key held across many frames only fires a single
        // press event instead of one per frame (which previously leaked onto later objects
        // and judged them far too early).
        while (_replayFrameIndex < frames.Count &&
               frames[_replayFrameIndex].MsSinceStart <= _songCursor)
        {
            var currentFrame = frames[_replayFrameIndex];

            // Reconstruct key states for this frame from scratch (so a release is honored).
            bool primaryDown = false;
            bool secondaryDown = false;
            foreach (var key in currentFrame.KeysPressed)
            {
                if (key == Keypress.K1 || key == Keypress.M1)
                    primaryDown = true;
                else if (key == Keypress.K2 || key == Keypress.M2)
                    secondaryDown = true;
            }

            ActionPrimaryDown = primaryDown;
            ActionSecondaryDown = secondaryDown;
            ActionPrimaryPressed = primaryDown && !_prevPrimary;
            ActionSecondaryPressed = secondaryDown && !_prevSecondary;

            _gameScreen.Judge(currentFrame.MsSinceStart);

            _prevPrimary = primaryDown;
            _prevSecondary = secondaryDown;
            _replayFrameIndex++;
        }
    }
    
    private Vector2 GetCurrentCursor()
    {
        switch (State)
        {
            case PlayerState.Player:
                if (_gameScreen == null) return Vector2.Zero;

                float mx = Math.Clamp(Input.MouseX, 0, 0 + _gameScreen.Width);
                float my = Math.Clamp(Input.MouseY, 0, 0 + _gameScreen.Height);
                return new Vector2(mx - 0, my - 0);

            case PlayerState.Autoplay:
                return _playfieldOffset + _autoplayCursor * _scale;

            case PlayerState.Replay:
                return GetInterpolatedReplayCursor();

            default:
                return Vector2.Zero;
        }
    }

    private Vector2 GetInterpolatedReplayCursor()
    {
        if (Replay == null || Replay.Frames.Count == 0)
            return _playfieldOffset;

        var frames = Replay.Frames;
        if (_replayFrameIndex < frames.Count)
            return _playfieldOffset + _scale * new Vector2(frames[_replayFrameIndex].X, frames[_replayFrameIndex].Y);
        return _playfieldOffset + _scale * new Vector2(frames[_replayFrameIndex-1].X, frames[_replayFrameIndex-1].Y);
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
        t = t * t * (3f - 2f * t);

        return Vector2.Lerp(startPos, current.Position, t);
    }

    private void Alternate(HitObject o)
    {
        //_gameView.SongCursor = o.Time; // I'm a slimey bastard
        if (!_primaryLastPressed)
            ActionPrimaryPressed = true;
        else
            ActionSecondaryPressed = true;

        _primaryLastPressed = !_primaryLastPressed;
    }
}