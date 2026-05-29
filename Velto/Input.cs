using System.Runtime.InteropServices;

namespace Velto;

using SDL;
using static SDL.SDL3;

public unsafe class Input
{
    private static int _numOfKeys;

    private static SDL_MouseButtonFlags _currentMouse;
    private static SDL_MouseButtonFlags _previousMouse;
    
    private static byte[] _currentKeyboard = new byte[(int)SDL_Scancode.SDL_SCANCODE_COUNT];
    private static byte[] _previousKeyboard = new byte[(int)SDL_Scancode.SDL_SCANCODE_COUNT];

    public static float MouseX { get; private set; }
    public static float MouseY { get; private set; }
    public static float DeltaX { get; private set; }
    public static float DeltaY { get; private set; }
    public static float WheelX { get; private set; }
    public static float WheelY { get; private set; }
    
    public static byte[] GetKeyboardState()
    {
        fixed (int* keys = &_numOfKeys)
        {
            // _previousKeyState = _currentKeyState;
            // _currentKeyState =  SDL_GetKeyboardState(keys);
            Array.Copy(_currentKeyboard, _previousKeyboard, _numOfKeys);
            Marshal.Copy((nint)SDL_GetKeyboardState(keys), _currentKeyboard, 0, _numOfKeys);
            return _currentKeyboard;
        }
    }

    public static void FixScrollback()
    {
        WheelX = 0;
        WheelY = 0;
    }

    public static void UpdateEvents(SDL_Event ev)
    {
        switch (ev.type)
        {
            case (uint)SDL_EventType.SDL_EVENT_MOUSE_WHEEL:
                WheelX = ev.wheel.x;
                WheelY = ev.wheel.y;
                break;
        }
    }

    public static void UpdateMouse(SDL_Window* window)
    {
        // store previous state
        _previousMouse = _currentMouse;

        float x, y;
        _currentMouse = SDL_GetMouseState(&x, &y);

        //float scale = SDL_GetDisplayContentScale(SDL_GetPrimaryDisplay());
        //if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) scale = 2;
        float scale = SDL_GetWindowDisplayScale(window);

        // position
        float prevX = MouseX;
        float prevY = MouseY;

        MouseX = x * scale;
        MouseY = y * scale;

        // delta
        DeltaX = MouseX - prevX;
        DeltaY = MouseY - prevY;
    }
    
    // private static SDL_MouseButtonFlags ButtonMask(SDL_MouseButtonFlags button)
    // {
    //     return (SDL_MouseButtonFlags)(1 << ((int)button - 1));
    // }

    public static bool IsKeyDown(SDL_Scancode scancode)
    {
        return _currentKeyboard[(int)scancode] != 0;
    }

    public static bool IsKeyJustPressed(SDL_Scancode scancode)
    {
        return _currentKeyboard[(int)scancode] != 0 && _previousKeyboard[(int)scancode] == 0;
    }
    
    public static bool IsKeyboardJustReleased(SDL_Scancode scancode)
    {
        return _currentKeyboard[(int)scancode] == 0 && _previousKeyboard[(int)scancode] != 0;
    }
    
    public static bool IsMouseDown(SDLButton button)
    {
        return (_currentMouse & SDL_BUTTON(button)) != 0;
    }
    
    public static bool IsMouseJustPressed(SDLButton button)
    {
        var mask = SDL_BUTTON(button);

        return (_currentMouse & mask) != 0 &&
               (_previousMouse & mask) == 0;
    }
    
    public static bool IsMouseJustReleased(SDLButton button)
    {
        var mask = SDL_BUTTON(button);

        return (_currentMouse & mask) == 0 &&
               (_previousMouse & mask) != 0;
    }
}