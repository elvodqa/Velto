using SDL3;

namespace Velto;

public static class Input
{
    private static bool[] _currentKeyboard = [];
    private static bool[] _previousKeyboard = [];

    private static SDL.MouseButtonFlags _currentMouse;
    private static SDL.MouseButtonFlags _previousMouse;

    public static float MouseX { get; private set; }
    public static float MouseY { get; private set; }

    public static float DeltaX { get; private set; }
    public static float DeltaY { get; private set; }

    public static float WheelX { get; private set; }
    public static float WheelY { get; private set; }

    public static void UpdateKeyboard()
    {
        var state = SDL.GetKeyboardState(out int keyCount);

        if (_currentKeyboard.Length != keyCount)
        {
            _currentKeyboard = new bool[keyCount];
            _previousKeyboard = new bool[keyCount];
        }

        Array.Copy(_currentKeyboard, _previousKeyboard, keyCount);

        for (int i = 0; i < keyCount; i++)
        {
            _currentKeyboard[i] = state[i];
        }
    }

    public static void UpdateMouse(nint window)
    {
        _previousMouse = _currentMouse;

        _currentMouse = SDL.GetMouseState(out float x, out float y);

        float scale = SDL.GetWindowDisplayScale(window);

        float prevX = MouseX;
        float prevY = MouseY;

        MouseX = x * scale;
        MouseY = y * scale;

        DeltaX = MouseX - prevX;
        DeltaY = MouseY - prevY;
    }

    public static void UpdateEvent(SDL.Event e)
    {
        switch ((SDL.EventType)e.Type)
        {
            case SDL.EventType.MouseWheel:
                WheelX = e.Wheel.X;
                WheelY = e.Wheel.Y;
                break;
        }
    }

    public static void EndFrame()
    {
        WheelX = 0;
        WheelY = 0;
    }

    public static bool IsKeyDown(SDL.Scancode key)
    {
        return _currentKeyboard[(int)key];
    }

    public static bool IsKeyJustPressed(SDL.Scancode key)
    {
        int index = (int)key;

        return _currentKeyboard[index] &&
               !_previousKeyboard[index];
    }

    public static bool IsKeyJustReleased(SDL.Scancode key)
    {
        int index = (int)key;

        return !_currentKeyboard[index] &&
                _previousKeyboard[index];
    }

    public static bool IsMouseDown(SDL.MouseButtonFlags button)
    {
        return (_currentMouse & button) != 0;
    }

    public static bool IsMouseJustPressed(SDL.MouseButtonFlags button)
    {
        //var mask = SDL.ButtonMask(button);

        return (_currentMouse & button) != 0 &&
               (_previousMouse & button) == 0;
    }

    public static bool IsMouseJustReleased(SDL.MouseButtonFlags button)
    {
        //var mask = SDL.ButtonMask(button);

        return (_currentMouse & button) == 0 &&
               (_previousMouse & button) != 0;
    }
}