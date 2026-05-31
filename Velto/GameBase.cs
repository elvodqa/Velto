using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using Velto.Graphics;
using Velto.Views;

namespace Velto;

using Velto.Logging;
using OpenTK;
using OpenTK.Core.Utility;
using OpenTK.Graphics;
using System;
using SDL;
using static SDL.SDL3;
using OpenTK.Graphics.OpenGL;

public class BindingContext : IBindingsContext
{
    public IntPtr GetProcAddress(string procName)
    {
        return SDL_GL_GetProcAddress(procName);
    }
}

public unsafe class GameBase : IDisposable
{
    private Logger _logger;
    private SDL_Window* _window;
    private SDL_GLContextState* _glContextState;

    private GCHandle _eventWatchHandle;
    private IntPtr _eventWatchUserdata;
    private ulong _eventWatchTickLast;

    private GameView _gameDisplay;
    private bool _running;

    private Renderer _renderer;
    
    private double _fpsTimer = 0.0;
    private int _frameCount = 0;
    private double _fps = 0;

    private MSDFFont _debugFont;


    public GameBase()
    {
        SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO);
        _logger = new Logger();
    }


    public void Initialize()
    {
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
        
        _window = SDL_CreateWindow("Velto"u8, 1280, 720, SDL_WindowFlags.SDL_WINDOW_OPENGL | SDL_WindowFlags.SDL_WINDOW_RESIZABLE
            | SDL_WindowFlags.SDL_WINDOW_HIGH_PIXEL_DENSITY
            );
        _glContextState = SDL_GL_CreateContext(_window);
        SDL_GL_MakeCurrent(_window, _glContextState);
        // SDL_GL_SetSwapInterval(0);

        int interval;
        SDL_GL_GetSwapInterval(&interval);
        _logger.Info($"Swap Interval = {interval}");
        SDL_GL_SetSwapInterval(1);
        
        GLLoader.LoadBindings(new BindingContext());
    
        GCHandle handle = GCHandle.Alloc(_logger);
        IntPtr ptr = GCHandle.ToIntPtr(handle);
        //GL.DebugMessageCallback(DebugCallback, IntPtr.Zero);
        
        GL.Enable(EnableCap.DebugOutput);
        GL.Enable(EnableCap.DebugOutputSynchronous);
        
        _logger.Info($"BasePath: {SDL_GetBasePath()}");
        _logger.Info($"Vendor:   {GL.GetString(StringName.Vendor)}");
        _logger.Info($"Renderer: {GL.GetString(StringName.Renderer)}");
        _logger.Info($"Version:  {GL.GetString(StringName.Version)}");
        _logger.Info($"GLSL:     {GL.GetString(StringName.ShadingLanguageVersion)}");
        
        GL.GetInteger(GetPName.NumExtensions, out var numOfExtensions);

        for (int i = 0; i < numOfExtensions; i++)
        {
            var extension = GL.GetStringi(StringName.Extensions, (uint)i);
            //_logger.Info($"\t{extension}");
        }
        
        _running = false;
        _renderer = new(_window);
        _gameDisplay = new(_renderer);

        // During a live window resize, the OS may block the main loop. An event watch lets us redraw.
        _eventWatchHandle = GCHandle.Alloc(this);
        _eventWatchUserdata = GCHandle.ToIntPtr(_eventWatchHandle);
        SDL_AddEventWatch(&EventWatch, _eventWatchUserdata);

        _debugFont = MSDFFont.Load(Resources.GetPath("Resources/Fonts/arial/arial"));
    }

    

    public void Run()
    {
        Initialize();
        SDL_MaximizeWindow(_window);
        //SDL_HideCursor();
        
        ulong tickNow = SDL_GetPerformanceCounter();
        ulong tickLast = 0;
        double deltaTime = 0;
        _running = true;
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

                //SDL_SetWindowTitle(_window,
                //    $"Velto - FPS: {fps:F1}");

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
                        _logger.Info($"Window Resized: {ev.window.data1} x {ev.window.data2}");
                        break;
                    case (uint)SDL_EventType.SDL_EVENT_KEY_DOWN:
                        if (ev.key.key == SDL_Keycode.SDLK_F1)
                        {
                            _gameDisplay.Dispose();
                            _gameDisplay = new(_renderer);
                        }
                        break;
                }
                Input.UpdateEvents(ev);
            }

            Loop(deltaTime);
            
            SDL_GL_SwapWindow(_window);
        }
    }

    private void Loop(double deltaTime)
    {
        _renderer.BeginFrame();

        _gameDisplay.Width = (int)_renderer.WindowSizeInPixels.X;
        _gameDisplay.Height = (int)_renderer.WindowSizeInPixels.Y;
        
        Input.GetKeyboardState();
        Input.UpdateMouse(_window);
            
        _gameDisplay.Update(deltaTime);
        _gameDisplay.Draw(deltaTime);
        //_renderer.Line();
        _renderer.DrawText(_debugFont, $"FPS: {_fps.ToString("0000.0")} [{deltaTime.ToString("00.00")}ms] | DrawCallCount: {_renderer.DrawCallCount:000000}", new (5, 5), 0.6f, new Vector4(1, 1, 1, 1));
        _renderer.FlushText(_debugFont);
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
        var game = ((GCHandle)userdata).Target as GameBase;
        if (game == null || ev == null)
            return true;

        // Use the top-level event discriminator; reading ev->window for non-window events is unsafe.
        var type = (SDL_EventType)ev->type;

        if (type == SDL_EventType.SDL_EVENT_WINDOW_EXPOSED ||
            type == SDL_EventType.SDL_EVENT_WINDOW_HIT_TEST ||
            type == SDL_EventType.SDL_EVENT_WINDOW_RESIZED)
        {
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

        SDL_GL_DestroyContext(_glContextState);
        SDL_DestroyWindow(_window);
    }
    
    private static void DebugCallback(DebugSource source, DebugType type, uint id, DebugSeverity severity, int length, IntPtr message, IntPtr userParam)
    {
        string msg = System.Runtime.InteropServices.Marshal
            .PtrToStringAnsi(message, length);

        string formatted =
            $"[{severity}] {type} ({source}) #{id}: {msg}";
        Console.WriteLine(formatted);
        /*switch (severity)
        {
            case DebugSeverity.DebugSeverityHigh:
                _logger.Error(formatted);
                break;

            case DebugSeverity.DebugSeverityMedium:
                _logger.Warn(formatted);
                break;

            case DebugSeverity.DebugSeverityLow:
                _logger.Info(formatted);
                break;

            default:
                _logger.Debug(formatted);
                break;
        }*/
    }
}