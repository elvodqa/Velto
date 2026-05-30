using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using Velto.Graphics;

namespace Velto;

using Velto.Logging;
using OpenTK;
using OpenTK.Core.Utility;
using OpenTK.Graphics;
using System;
using SDL3;

using OpenTK.Graphics.OpenGL;

public class BindingContext : IBindingsContext
{
    public IntPtr GetProcAddress(string procName)
    {
        return SDL.GLGetProcAddress(procName);
    }
}

public unsafe class GameBase : IDisposable
{
    private Logger _logger;
    private IntPtr _window;
    private IntPtr _glContextState;

    private GCHandle _eventWatchHandle;
    private IntPtr _eventWatchUserdata;
    private ulong _eventWatchTickLast;

    private GameDisplay _gameDisplay;
    private bool _running;

    private Renderer _renderer;
    
    private double _fpsTimer = 0.0;
    private int _frameCount = 0;
    private double _fps = 0;

    private MSDFFont _debugFont;
    private SDL.EventFilter _eventFilter;


    public GameBase()
    {
        SDL.Init(SDL.InitFlags.Video);
        _logger = new Logger();
    }


    public void Initialize()
    {
        
        SDL.GLSetAttribute(SDL.GLAttr.ContextMajorVersion, 4);
        SDL.GLSetAttribute(SDL.GLAttr.ContextMinorVersion, 1);
        SDL.GLSetAttribute(SDL.GLAttr.ContextFlags, (int)(SDL.GLContextFlag.ForwardCompatible | SDL.GLContextFlag.Debug));
        SDL.GLSetAttribute(SDL.GLAttr.ContextProfileMask, (int)SDL.GLProfile.Core);
        SDL.GLSetAttribute(SDL.GLAttr.DoubleBuffer, 1);
        SDL.GLSetAttribute(SDL.GLAttr.DepthSize, 24);
        
        _window = SDL.CreateWindow("Velto", 1280, 720, SDL.WindowFlags.OpenGL | SDL.WindowFlags.Resizable
            | SDL.WindowFlags.HighPixelDensity
            );
        _glContextState = SDL.GLCreateContext(_window);
        SDL.GLMakeCurrent(_window, _glContextState);
        SDL.GLSetSwapInterval(0);
        
        SDL.GLGetSwapInterval(out int interval);
        _logger.Info($"Swap Interval = {interval}");
        //SDL_GL_SetSwapInterval(0);
        
        GLLoader.LoadBindings(new BindingContext());
    
        GCHandle handle = GCHandle.Alloc(_logger);
        IntPtr ptr = GCHandle.ToIntPtr(handle);
        //GL.DebugMessageCallback(DebugCallback, IntPtr.Zero);
        GL.Enable(EnableCap.DebugOutput);
        GL.Enable(EnableCap.DebugOutputSynchronous);
        
        _logger.Info($"BasePath: {SDL.GetBasePath()}");
        _logger.Info($"Vendor:   {GL.GetString(StringName.Vendor)}");
        _logger.Info($"Renderer: {GL.GetString(StringName.Renderer)}");
        _logger.Info($"Version:  {GL.GetString(StringName.Version)}");
        _logger.Info($"GLSL:     {GL.GetString(StringName.ShadingLanguageVersion)}");

        _running = false;
        _renderer = new(_window);
        _gameDisplay = new(_renderer);

        // During a live window resize, the OS may block the main loop. An event watch lets us redraw.
        _eventWatchHandle = GCHandle.Alloc(this);
        _eventWatchUserdata = GCHandle.ToIntPtr(_eventWatchHandle);
        _eventFilter = Filter;
        SDL.AddEventWatch(_eventFilter, _eventWatchUserdata);

        _debugFont = MSDFFont.Load(Resources.GetPath("Resources/Fonts/arial/arial"));
    }
    
    public void Run()
    {
        Initialize();
        SDL.MaximizeWindow(_window);
        SDL.HideCursor();
        
        ulong tickNow = SDL.GetPerformanceCounter();
        ulong tickLast = 0;
        double deltaTime = 0;
        _running = true;
        Input.UpdateKeyboard();
        Input.UpdateMouse(_window);
        while (_running)
        {
            SDL.PumpEvents();
            tickLast = tickNow;
            tickNow = SDL.GetPerformanceCounter();
            deltaTime = (tickNow - tickLast)*1000 / (double)SDL.GetPerformanceFrequency();
            
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

            
            while (SDL.PollEvent(out SDL.Event ev))
            {
                switch ((SDL.EventType)ev.Type)
                {
                    case SDL.EventType.Quit:
                        _running = false;
                        break;
                    case SDL.EventType.WindowResized:
                        _logger.Info($"Window Resized: {ev.Window.Data1} x {ev.Window.Data2}");
                        break;
                    case SDL.EventType.KeyDown:
                        if (ev.Key.Key == SDL.Keycode.F1)
                        {
                            _gameDisplay.Dispose();
                            _gameDisplay = new(_renderer);
                        }
                        break;
                }
                Input.UpdateEvent(ev);
            }

            Loop(deltaTime);
            
            SDL.GLSwapWindow(_window);
        }
    }

    private void Loop(double deltaTime)
    {
        Input.UpdateKeyboard();
        Input.UpdateMouse(_window);
            
        _gameDisplay.Update(deltaTime);
        _gameDisplay.Draw(deltaTime);
        
        _renderer.DrawText(_debugFont, $"FPS: {_fps:F1} [{deltaTime:F2}ms]", new (5, 5), 0.6f, new Vector4(1, 1, 1, 1));
        
        Input.EndFrame();
    }
    
    private void RenderFromEventWatch()
    {
        // Ensure the correct context is current before any GL calls.
        SDL.GLMakeCurrent(_window, _glContextState);

        var tickNow = SDL.GetPerformanceCounter();
        double deltaTime;
        if (_eventWatchTickLast == 0)
        {
            deltaTime = 0;
        }
        else
        {
            deltaTime = (tickNow - _eventWatchTickLast) * 1000 / (double)SDL.GetPerformanceFrequency();
            if (deltaTime < 0) deltaTime = 0;
            if (deltaTime > 250) deltaTime = 250; // avoid giant jumps during resize stalls
        }

        _eventWatchTickLast = tickNow;

        Loop(deltaTime);

        SDL.GLSwapWindow(_window);
    }

    //[UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private bool Filter(IntPtr userdata, ref SDL.Event ev)
    {
        var game = ((GCHandle)userdata).Target as GameBase;
        if (game == null)
            return true;

        // Use the top-level event discriminator; reading ev->window for non-window events is unsafe.
        var type = (SDL.EventType)ev.Type;

        if (type == SDL.EventType.WindowExposed ||
            type == SDL.EventType.WindowHitTest ||
            type == SDL.EventType.WindowResized)
        {
            game.RenderFromEventWatch();
        }

        // Return true so we don't accidentally swallow events before the main poll loop sees them.
        return true;
    }
    
    /*
     @(private)
       resizeCallback :: proc "c" (userdata: rawptr, event: ^sdl.Event) -> bool {
           context = runtime.default_context()
           game := cast(^Game)userdata
       
           if event.window.commonEvent.type == .WINDOW_EXPOSED || event.window.commonEvent.type == .WINDOW_HIT_TEST {
               width, height: c.int
               sdl.GetWindowSize(window, &width, &height)
               work_game_frame(game, width, height, game.userdata)
               sdl.GL_SwapWindow(window) 
           }
       
           return false
       }
     
     */

    public void Dispose()
    {
        if (_eventWatchUserdata != IntPtr.Zero)
        {
            SDL.RemoveEventWatch(Filter, _eventWatchUserdata);
            _eventWatchUserdata = IntPtr.Zero;
        }

        if (_eventWatchHandle.IsAllocated)
            _eventWatchHandle.Free();

        SDL.GLDestroyContext(_glContextState);
        SDL.DestroyWindow(_window);
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