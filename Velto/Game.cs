using System.Diagnostics;
using System.Runtime.InteropServices;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using SDL;
using Velto.Audio;
using Velto.Core.Timing;
using Velto.Graphics;
using Velto.Graphics.Metal;
using Velto.Graphics.OpenGL;
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
    public Window Window { get; private set; }
    public IGraphicsDevice GraphicsDevice { get; private set; }
    
    private GCHandle _eventWatchHandle;
    private IntPtr _eventWatchUserdata;
    private ulong _eventWatchTickLast;
    private bool _running;
    private IRenderer _renderer;
    private double _fpsTimer = 0.0;
    private int _frameCount = 0;
    private double _fps = 0;
    private bool _debugInfo = true;
   
    private FramedClock drawClock;
    private GraphicsBackend backend;
    
    public Game(GraphicsBackend backend = GraphicsBackend.OpenGL)
    {
        this.backend = backend;
    }

    private void Initialize()
    {
        if (backend == GraphicsBackend.OpenGL)
        {
            Window = new Window(GraphicsBackend.OpenGL);
            GraphicsDevice = new OpenGLGraphicsDevice(Window);
            _renderer = new OpenGLRenderer(GraphicsDevice as OpenGLGraphicsDevice, Window);
        }
        if (backend == GraphicsBackend.Metal)
        {
            Window = new Window(GraphicsBackend.Metal);
            GraphicsDevice = new MetalGraphicsDevice(Window);
            _renderer = new MetalRenderer(GraphicsDevice as MetalGraphicsDevice, Window);
        }
        
        
        Window.SetLoop(Loop);
       
        
        Fonts.Default = Font.Load(GraphicsDevice, Resources.GetPath("Resources/Fonts/arial/arial"));
        ScreenManager.Instance.Renderer = _renderer;
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
        Window.Run();
    }

    
    public void Loop(double delta)
    {
        AudioManager.Instance.Update(delta);
        
        var inputStart = Stopwatch.GetTimestamp();
        Input.GetKeyboardState();
        Input.UpdateMouse(Window.Handle);
        var inputEnd = Stopwatch.GetTimestamp();
        
        var updateStart = Stopwatch.GetTimestamp();
        ScreenManager.Instance.Update(delta);
        var updateEnd = Stopwatch.GetTimestamp();
        
        var drawBegin = Stopwatch.GetTimestamp();
        _renderer.BeginFrame();
        ScreenManager.Instance.Draw(_renderer);
        ScreenManager.Instance.Present();
        var draweEnd = Stopwatch.GetTimestamp();


        var input = (double)(inputEnd - inputStart) * 1000f / Stopwatch.Frequency;
        var update = (double)(updateEnd - updateStart) * 1000f / Stopwatch.Frequency;
        var draw = (double)(draweEnd - drawBegin) * 1000f / Stopwatch.Frequency;

        var count = _renderer.DrawCallCount;
        _renderer.DrawText(Fonts.Default, $"FPS: {Window.Clock.FramesPerSecond} [{Window.Clock.AverageFrameTime.ToString("00.00")}ms]" +
                                          $" | DrawCallCount: {count:000000} | {backend} \n" +
                                          $"Screen: {ScreenManager.Instance.Top}\n"
            +$"Input {input.ToString("00.0000")}ms | Update {update.ToString("00.0000")}ms | Draw {draw.ToString("00.0000")}ms", 
            new (5, 5), Window.WindowSize.Y / 45, new Color4<Rgba>(1, 1, 1, 1));
    
        _renderer.FlushText(Fonts.Default);
        
        _renderer.EndFrame();
    }
    
    public void Dispose()
    {
        _renderer.Dispose();
        Window.Dispose();
    }
}