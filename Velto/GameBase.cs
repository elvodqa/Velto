using System.Runtime.InteropServices;
using Velto.Graphics;

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
    
    private GameDisplay _gameDisplay;
    private bool _running;

   
    
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
            /*| SDL_WindowFlags.SDL_WINDOW_HIGH_PIXEL_DENSITY*/);
        _glContextState = SDL_GL_CreateContext(_window);
        SDL_GL_MakeCurrent(_window, _glContextState);
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

        _running = false;
        _gameDisplay = new(_window);
    }

    

    public void Run()
    {
        Initialize();
        SDL_MaximizeWindow(_window);
        
        ulong tickNow = SDL_GetPerformanceCounter();
        ulong tickLast = 0;
        double deltaTime = 0;
        _running = true;
        while (_running)
        {
            tickLast = tickNow;
            tickNow = SDL_GetPerformanceCounter();
            deltaTime = (tickNow - tickLast)*1000 / (double)SDL_GetPerformanceFrequency();
            
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
                            _gameDisplay = new(_window);
                        }
                        break;
                }
            }
            
            _gameDisplay.Update(deltaTime);
            _gameDisplay.Draw(deltaTime);
            
            SDL_GL_SwapWindow(_window);
        }
        
    }

    public void Dispose()
    {
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