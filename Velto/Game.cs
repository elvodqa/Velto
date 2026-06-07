using System.Runtime.InteropServices;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using SDL;
using Velto.Audio;
using Velto.Core.Timing;
using Velto.Graphics;
using static SDL.SDL3;

namespace Velto.Core;

public struct GameCreateInfo
{
    public string Title;
    public bool Maximized;
}

public class BindingContext : IBindingsContext
{
    public IntPtr GetProcAddress(string procName)
    {
        return SDL_GL_GetProcAddress(procName);
    }
}

public unsafe class Game : IDisposable
{
    private string Title;
    private bool Maximized;
    
    private SDL_Window* _window;
    private SDL_GLContextState* _glContextState;
    
    private GCHandle _eventWatchHandle;
    private IntPtr _eventWatchUserdata;
    private ulong _eventWatchTickLast;
    private bool _running;
    private Renderer _renderer;
    private double _fpsTimer = 0.0;
    private int _frameCount = 0;
    private double _fps = 0;
    private bool _debugInfo = true;
    private FramedClock framedClock;

    public Vector2 WindowSizeInPixels => Renderer.WindowSizeInPixels;
    
    public Game(GameCreateInfo createInfo)
    {
        Title = createInfo.Title;
        Maximized = createInfo.Maximized;
    }

    private void Initialize()
    {
        SDL_SetHint(SDL_HINT_AUDIO_DEVICE_SAMPLE_FRAMES, "128");
        if (!SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO | SDL_InitFlags.SDL_INIT_AUDIO))
        {
            throw new Exception(SDL_GetError());
        }
        if (!SDL3_mixer.MIX_Init())
        {
            throw new Exception(SDL_GetError());
        }
        
        SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_CONTEXT_MAJOR_VERSION, 4);
        SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_CONTEXT_MINOR_VERSION, 1);
        SDL_GL_SetAttribute(    
            SDL_GLAttr.SDL_GL_CONTEXT_FLAGS,
            SDL_GL_CONTEXT_FORWARD_COMPATIBLE_FLAG |
            SDL_GL_CONTEXT_DEBUG_FLAG);
        SDL_GL_SetAttribute(
            SDL_GLAttr.SDL_GL_CONTEXT_PROFILE_MASK,
            SDL_GL_CONTEXT_PROFILE_CORE);
        SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_DOUBLEBUFFER, 1);
        SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_DEPTH_SIZE, 24);
        
        _window = SDL_CreateWindow(Title, 1280, 720, SDL_WindowFlags.SDL_WINDOW_OPENGL 
                                                    | SDL_WindowFlags.SDL_WINDOW_RESIZABLE
                                                    | SDL_WindowFlags.SDL_WINDOW_HIGH_PIXEL_DENSITY
        );
        _glContextState = SDL_GL_CreateContext(_window);
        SDL_GL_MakeCurrent(_window, _glContextState);
        GLLoader.LoadBindings(new BindingContext());
        
        int interval;
        if (!SDL_GL_SetSwapInterval(-1))
        {
            SDL_GL_SetSwapInterval(1);
        }
        SDL_GL_GetSwapInterval(&interval);
        
        Logger.Instance.Info($"BasePath: {SDL_GetBasePath()}");
        Logger.Instance.Info($"Vendor:   {GL.GetString(StringName.Vendor)}");
        Logger.Instance.Info($"Renderer: {GL.GetString(StringName.Renderer)}");
        Logger.Instance.Info($"Version:  {GL.GetString(StringName.Version)}");
        Logger.Instance.Info($"GLSL:     {GL.GetString(StringName.ShadingLanguageVersion)}");
        Logger.Instance.Info($"Swap:     {interval}");
        
        _running = false;
        _renderer = new(_window);
        
        _eventWatchHandle = GCHandle.Alloc(this);
        _eventWatchUserdata = GCHandle.ToIntPtr(_eventWatchHandle);
        SDL_AddEventWatch(&EventWatch, _eventWatchUserdata);
        
        Fonts.Default = MSDFFont.Load(Resources.GetPath("Resources/Fonts/arial/arial"));
        ScreenManager.Instance.Renderer = _renderer;

        framedClock = new();
    }
    
    public virtual void Load() { }

    public void SetScreen(Screen screen)
    {
        ScreenManager.Instance.SetTree([screen]);
    }

    public void Run()
    {
        Initialize();
        Load();
        ulong tickNow = SDL_GetPerformanceCounter();
        ulong tickLast = 0;
        double deltaTime = 0;
        _running = true;

        if (Maximized) SDL_MaximizeWindow(_window);
        
        Input.GetKeyboardState();
        Input.UpdateMouse(_window);
        while (_running)
        {
            Input.FixScrollback();
            SDL_PumpEvents();
            tickLast = tickNow;
            tickNow = SDL_GetPerformanceCounter();
            deltaTime = (tickNow - tickLast)*1000 / (double)SDL_GetPerformanceFrequency();
            
            _frameCount++;
            _fpsTimer += deltaTime;
            if (_fpsTimer >= 1000.0)
            {
                _fps = _frameCount * 1000.0 / _fpsTimer;
                _frameCount = 0;
                _fpsTimer = 0.0;
            }
            
            SDL_Event ev;
            while (SDL_PollEvent(&ev))
            {
                switch (ev.type)
                {
                    case (uint)SDL_EventType.SDL_EVENT_QUIT:
                        _running = false;
                        break;
                    case (uint)SDL_EventType.SDL_EVENT_WINDOW_RESIZED:
                        var size = Renderer.WindowSizeInPixels;
                        ScreenManager.Instance.ResizeCallback(ev.window.data1, ev.window.data2);
                        Logger.Instance.Info($"Window Resized: {ev.window.data1} x {ev.window.data2}");
                        break;
                }
                Input.UpdateEvents(ev);
            }

            if (Input.IsKeyboardJustReleased(SDL_Scancode.SDL_SCANCODE_F12)) _debugInfo = !_debugInfo;
            
            Loop(framedClock.ElapsedFrameTime);
            
            
            SDL_GL_SwapWindow(_window);
        }
    }

    public void Loop(double delta)
    {
        AudioManager.Instance.Update(delta);
        Input.GetKeyboardState();
        Input.UpdateMouse(_window);
        
        _renderer.BeginFrame();
        
        ScreenManager.Instance.Update(delta);
        ScreenManager.Instance.Draw(delta, _renderer);
        ScreenManager.Instance.Present(delta);

        if (_debugInfo)
        {
            _renderer.DrawText(Fonts.Default, $"FPS: {framedClock.FramesPerSecond} [{framedClock.AverageFrameTime.ToString("00.00")}ms]" +
                                              $" | DrawCallCount: {_renderer.DrawCallCount:000000}\n" +
                                              $"Top: {ScreenManager.Instance.Top}", 
                new (5, 5), Renderer.WindowSizeInPixels.Y / 45, new Color4<Rgba>(1, 1, 1, 1));
        
            _renderer.FlushText(Fonts.Default);
        }
        
        framedClock.ProcessFrame();
    }
    
    private void RenderFromEventWatch()
    {
        // Ensure the correct context is current before any GL calls.
        SDL_GL_MakeCurrent(_window, _glContextState);

        var tickNow = SDL_GetPerformanceCounter();
        double deltaTime;
        if (_eventWatchTickLast == 0)
        {
            deltaTime = 0;
        }
        else
        {
            deltaTime = (tickNow - _eventWatchTickLast) * 1000 / (double)SDL_GetPerformanceFrequency();
            if (deltaTime < 0) deltaTime = 0;
            if (deltaTime > 250) deltaTime = 250; // avoid giant jumps during resize stalls
        }

        _eventWatchTickLast = tickNow;

        Loop(deltaTime);
        
        SDL_GL_SwapWindow(_window);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static unsafe SDLBool EventWatch(IntPtr userdata, SDL_Event* ev)
    {
        var game = ((GCHandle)userdata).Target as Game;
        if (game == null || ev == null)
            return true;

        // Use the top-level event discriminator; reading ev->window for non-window events is unsafe.
        var type = (SDL_EventType)ev->type;

        if (type == SDL_EventType.SDL_EVENT_WINDOW_EXPOSED ||
            type == SDL_EventType.SDL_EVENT_WINDOW_HIT_TEST ||
            type == SDL_EventType.SDL_EVENT_WINDOW_RESIZED)
        {
            var size = Renderer.WindowSizeInPixels;
            ScreenManager.Instance.ResizeCallback((int)size.X, (int)size.Y);
            game.RenderFromEventWatch();
        }

        // Return true so we don't accidentally swallow events before the main poll loop sees them.
        return true;
    }
    
    public void Dispose()
    {
        if (_eventWatchUserdata != IntPtr.Zero)
        {
            SDL_RemoveEventWatch(&EventWatch, _eventWatchUserdata);
            _eventWatchUserdata = IntPtr.Zero;
        }

        if (_eventWatchHandle.IsAllocated)
            _eventWatchHandle.Free();

        _renderer.Dispose();
        SDL_GL_DestroyContext(_glContextState);
        SDL_DestroyWindow(_window);
    }
}