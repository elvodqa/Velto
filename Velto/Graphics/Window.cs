using System.Runtime.InteropServices;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using SDL;
using Velto.Core;
using Velto.Core.Timing;
using static SDL.SDL3;

namespace Velto.Graphics;

public class BindingContext : IBindingsContext
{
    public IntPtr GetProcAddress(string procName)
    {
        return SDL_GL_GetProcAddress(procName);
    }
}

public unsafe class Window : IDisposable
{
    public string Title
    {
        get => title;
        set
        {
            title = value;
            SDL_SetWindowTitle(Handle, title);
        }
    }

    public SDL_Window* Handle { get; private set; }
    public SDL_GLContextState* MainGLContextState { get; private set; }
    public Vector2 WindowSize
    {
        get
        {
            int w, h;
            SDL_GetWindowSizeInPixels(Handle, &w, &h);
            return new Vector2(w, h);
        }
    }
    public FramedClock Clock { get; private set; }
    
    private string title;
    private bool running = false;
    private Action<double> loopFunction;
    private GraphicsBackend backend;
    private ulong eventWatchTickLast;
    private GCHandle eventWatchHandle;
    private IntPtr eventWatchUserdata;
    
    public Window(GraphicsBackend _backend)
    {
        backend = _backend;
        
        SDL_SetHint(SDL_HINT_AUDIO_DEVICE_SAMPLE_FRAMES, "128");
        if (!SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO | SDL_InitFlags.SDL_INIT_AUDIO))
        {
            throw new Exception(SDL_GetError());
        }
        if (!SDL3_mixer.MIX_Init())
        {
            throw new Exception(SDL_GetError());
        }

        if (backend == GraphicsBackend.OpenGL)
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
            
            Handle = SDL_CreateWindow(Title, 1280, 720, SDL_WindowFlags.SDL_WINDOW_OPENGL 
                                                        | SDL_WindowFlags.SDL_WINDOW_RESIZABLE
                                                        | SDL_WindowFlags.SDL_WINDOW_HIGH_PIXEL_DENSITY
            );
            MainGLContextState = CreateGLContext();
            SetGLContext(MainGLContextState);
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
        }
        
        eventWatchHandle = GCHandle.Alloc(this);
        eventWatchUserdata = GCHandle.ToIntPtr(eventWatchHandle);
        SDL_AddEventWatch(&EventWatch, eventWatchUserdata);

        Clock = new();
    }
    
    public void SetLoop(Action<double> loop)
    {
        loopFunction = loop;
    }

    public void Run()
    {
        running = true;
        
        Input.GetKeyboardState();
        Input.UpdateMouse(Handle);
        
        while (running)
        {
            Input.FixScrollback();
            SDL_PumpEvents();
            
            SDL_Event ev;
            while (SDL_PollEvent(&ev))
            {
                switch (ev.type)
                {
                    case (uint)SDL_EventType.SDL_EVENT_QUIT:
                        running = false;
                        break;
                    case (uint)SDL_EventType.SDL_EVENT_WINDOW_RESIZED:
                        var size = Renderer.WindowSizeInPixels;
                        ScreenManager.Instance.ResizeCallback(ev.window.data1, ev.window.data2);
                        Logger.Instance.Info($"Window Resized: {ev.window.data1} x {ev.window.data2}");
                        break;
                }
                Input.UpdateEvents(ev);
            }
            
            loopFunction(Clock.ElapsedFrameTime);
            Clock.ProcessFrame();
            
            if (backend == GraphicsBackend.OpenGL)
            {
                GLSwapWindow();
            }
        }
    }
    
    private void RenderFromEventWatch()
    {
        if (backend == GraphicsBackend.OpenGL)
        {
            SetGLContext(MainGLContextState);
        }
        
        var tickNow = SDL_GetPerformanceCounter();
        double deltaTime;
        if (eventWatchTickLast == 0)
        {
            deltaTime = 0;
        }
        else
        {
            deltaTime = (tickNow - eventWatchTickLast) * 1000 / (double)SDL_GetPerformanceFrequency();
            if (deltaTime < 0) deltaTime = 0;
            if (deltaTime > 250) deltaTime = 250; // avoid giant jumps during resize stalls
        }

        eventWatchTickLast = tickNow;

        loopFunction(deltaTime);
        Clock.ProcessFrame();
        
        if (backend == GraphicsBackend.OpenGL)
        {
            GLSwapWindow();
        }
    }
    
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static SDLBool EventWatch(IntPtr userdata, SDL_Event* ev)
    {
        var window = ((GCHandle)userdata).Target as Window;
        if (window == null || ev == null) return true;

        var type = (SDL_EventType)ev->type;

        if (type == SDL_EventType.SDL_EVENT_WINDOW_RESIZED ||
            type == SDL_EventType.SDL_EVENT_WINDOW_EXPOSED)
        {
            var size = Renderer.WindowSizeInPixels;
            ScreenManager.Instance.ResizeCallback((int)size.X, (int)size.Y);
            window.RenderFromEventWatch();
        }

        return true;
    }
    
    // OpenGL stuff
    public SDL_GLContextState* CreateGLContext()
    {
        return SDL_GL_CreateContext(Handle);
    }

    public void SetGLContext(SDL_GLContextState* state)
    {
        SDL_GL_MakeCurrent(Handle, state);
    }

    public void GLSwapWindow()
    {
        SDL_GL_SwapWindow(Handle);
    }

    public void DestroyGLContext(SDL_GLContextState* contextState)
    {
        SDL_GL_DestroyContext(contextState);
    }
    
    public void Dispose()
    {
        if (eventWatchUserdata != IntPtr.Zero)
        {
            SDL_RemoveEventWatch(&EventWatch, eventWatchUserdata);
            eventWatchUserdata = IntPtr.Zero;
        }

        if (eventWatchHandle.IsAllocated)
            eventWatchHandle.Free();
        
        if (MainGLContextState != null) DestroyGLContext(MainGLContextState);
        SDL_DestroyWindow(Handle);
    }
}