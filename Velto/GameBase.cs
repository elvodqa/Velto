using System.Drawing;
using Velto.Audio;

namespace Velto;

using OpenTK;
using OpenTK.Core.Utility;
using OpenTK.Graphics;
using System;
using SDL;
using static SDL.SDL3;
using OpenTK.Graphics.OpenGL;
using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using Velto.Core;
using Velto.Graphics;
using Velto.Views;


public class BindingContext : IBindingsContext
{
    public IntPtr GetProcAddress(string procName)
    {
        return SDL_GL_GetProcAddress(procName);
    }
}

public unsafe class GameBase : IDisposable
{
    private SDL_Window* _window;
    private SDL_GLContextState* _glContextState;

    private GCHandle _eventWatchHandle;
    private IntPtr _eventWatchUserdata;
    private ulong _eventWatchTickLast;

    private GameView _gameView;
    private SettingsView _settingsView;
    private SongSelectorView _songSelectorView;
    
    private bool _running;

    private Renderer _renderer;
    
    private double _fpsTimer = 0.0;
    private int _frameCount = 0;
    private double _fps = 0;

    private MSDFFont _debugFont;

    private List<IInputReceiver> _views = new();
    private IInputReceiver _hovered;
    private IInputReceiver _captured;
    
    private IInputReceiver GetTopView(float x, float y)
    {
        for (int i = _views.Count - 1; i >= 0; i--)
        {
            var v = _views[i];
            if (v.HitTest(x, y))
                return v;
        }
        return null;
    }


    public GameBase()
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
        if (!SDL_GL_SetSwapInterval(-1))
        {
            SDL_GL_SetSwapInterval(1);
        }
        SDL_GL_GetSwapInterval(&interval);
        Logger.Instance.Info($"Swap Interval = {interval}");
       
        
        GLLoader.LoadBindings(new BindingContext());
        
        Logger.Instance.Info($"BasePath: {SDL_GetBasePath()}");
        Logger.Instance.Info($"Vendor:   {GL.GetString(StringName.Vendor)}");
        Logger.Instance.Info($"Renderer: {GL.GetString(StringName.Renderer)}");
        Logger.Instance.Info($"Version:  {GL.GetString(StringName.Version)}");
        Logger.Instance.Info($"GLSL:     {GL.GetString(StringName.ShadingLanguageVersion)}");
        
        GL.GetInteger(GetPName.NumExtensions, out var numOfExtensions);

        string extensions = "";
        for (int i = 0; i < numOfExtensions; i++)
        {
            var extension = GL.GetStringi(StringName.Extensions, (uint)i);
            extensions += $"{extension} | ";
        }
        Logger.Instance.Info($"{extensions}");
        
        _running = false;
        _renderer = new(_window);
        _gameView = new(_renderer)
        {
            Framebuffer = new Framebuffer(_renderer, 1280, 720)
        };
        _settingsView = new(_renderer)
        {
            Framebuffer = new Framebuffer(_renderer, 1280, 720)
        };
        _songSelectorView = new(_renderer, _gameView)
        {
            Framebuffer = new Framebuffer(_renderer, 1280, 720)
        };
        
        _views.Add(_gameView);

        // During a live window resize, the OS may block the main loop. An event watch lets us redraw.
        _eventWatchHandle = GCHandle.Alloc(this);
        _eventWatchUserdata = GCHandle.ToIntPtr(_eventWatchHandle);
        SDL_AddEventWatch(&EventWatch, _eventWatchUserdata);

        _debugFont = MSDFFont.Load(Resources.GetPath("Resources/Fonts/arial/arial"));
        
    }
    
    public void Run()
    {
        Initialize();
        // SDL_MaximizeWindow(_window);
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
                        _gameView.Framebuffer.Resize((int)_renderer.WindowSizeInPixels.X, (int)_renderer.WindowSizeInPixels.Y);
                        _settingsView.Framebuffer.Resize((int)_renderer.WindowSizeInPixels.X, (int)_renderer.WindowSizeInPixels.Y);
                        _songSelectorView.Framebuffer.Resize((int)_renderer.WindowSizeInPixels.X, (int)_renderer.WindowSizeInPixels.Y);
                        
                        Logger.Instance.Info($"Window Resized: {ev.window.data1} x {ev.window.data2}");
                        break;
                    case (uint)SDL_EventType.SDL_EVENT_KEY_DOWN:
                        // if (ev.key.key == SDL_Keycode.SDLK_F1)
                        // {
                        //     _gameDisplay.Dispose();
                        //     _gameDisplay = new(_renderer);
                        // }
                        break;
                    case (uint)SDL_EventType.SDL_EVENT_MOUSE_MOTION:
                        break;
                }
                Input.UpdateEvents(ev);
            }

            Loop(deltaTime);
            
            SDL_GL_SwapWindow(_window);
        }
    }


    private double f = 0;

    private void Loop(double deltaTime)
    {
        AudioManager.Instance.Update(deltaTime);
        Input.GetKeyboardState();
        Input.UpdateMouse(_window);

        f += deltaTime / 1000;
        
        _renderer.BeginFrame();
    
        double valueX = (Math.Cos(f) + 1.0) * 0.5;
        double valueY = (Math.Sin(f) + 1.0) * 0.5;

        _gameView.Width = (float)((_renderer.WindowSizeInPixels.X / 2) + (_renderer.WindowSizeInPixels.X / 2) * valueX);
        _gameView.Height = (float)((_renderer.WindowSizeInPixels.Y / 2) + (_renderer.WindowSizeInPixels.Y / 2) * valueY);
        _gameView.X = _renderer.WindowSizeInPixels.X / 2 - _gameView.Width / 2;
        _gameView.Y = _renderer.WindowSizeInPixels.Y / 2 - _gameView.Height / 2;


        if (Input.IsKeyDown(SDL_Scancode.SDL_SCANCODE_LCTRL))
        {
            if (Input.IsKeyJustPressed(SDL_Scancode.SDL_SCANCODE_O))
            {
                foreach (var receiver in _views)
                {
                    if (receiver is View view)
                    {
                        if (view is SettingsView settingsView)
                        {
                            
                        }

                        if (view is GameView gameView)
                        {
                            
                        }
                    }
                }
                
            }
        }
        
        var top = GetTopView(Input.MouseX, Input.MouseY);
        
        // Mouse move
        IInputReceiver target = _captured ?? top;
        if (target != null)
        {
            float localX = Input.MouseX - target.X;
            float localY = Input.MouseY - target.Y;

            target.OnMouseMove(new MouseEventArgs
            {
                X = (int)localX,
                Y = (int)localY
            });
        }
        

        if (top != null)
        {
            _captured = top;

            var button = MouseButton.None;
            if (Input.IsMouseJustPressed(SDLButton.SDL_BUTTON_LEFT)) button = MouseButton.Left;
            if (Input.IsMouseJustPressed(SDLButton.SDL_BUTTON_RIGHT)) button = MouseButton.Right;
            if (Input.IsMouseJustPressed(SDLButton.SDL_BUTTON_MIDDLE)) button = MouseButton.Middle;

            if (button != MouseButton.None)
            {
                top.OnMouseDown(button, new MouseEventArgs
                {
                    X = (int)(Input.MouseX - top.X),
                    Y = (int)(Input.MouseY - top.Y)
                });
            }
            
        }
        
        if (_captured != null)
        {
            var button = MouseButton.None;
            if (Input.IsMouseJustReleased(SDLButton.SDL_BUTTON_LEFT)) button = MouseButton.Left;
            if (Input.IsMouseJustReleased(SDLButton.SDL_BUTTON_RIGHT)) button = MouseButton.Right;
            if (Input.IsMouseJustReleased(SDLButton.SDL_BUTTON_MIDDLE)) button = MouseButton.Middle;
            
            _captured.OnMouseUp(button, new MouseEventArgs
            {
                X = (int)(Input.MouseX - _captured.X),
                Y = (int)(Input.MouseY - _captured.Y)
            });

            _captured = null;
        }
        
        if (top != _hovered)
        {
            _hovered?.OnMouseLeave();

            _hovered = top;

            _hovered?.OnMouseEnter();
        }

        foreach (var receiver in _views)
        {
            if (receiver is View view)
            {
                view.Update(deltaTime);
                
                _renderer.BindFramebuffer(view.Framebuffer);
                view.Draw(deltaTime);
                _renderer.UnbindFramebuffer(view.Framebuffer);
            }
        }
        
        _renderer.SetScissor(0, 0, (int)_renderer.WindowSizeInPixels.X, (int)_renderer.WindowSizeInPixels.Y);
        _renderer.Clear(new Vector4(0, 0, 0, 1));
        foreach (var receiver in _views)
        {
            if (receiver is View view)
            {
                _renderer.DrawTexture(_gameView.Framebuffer.Texture, view.X, view.Y,view.Width, view.Height, new Vector4(1, 1, 1, 1));
            }
        }
        
        _renderer.DrawText(_debugFont, $"FPS: {_fps.ToString("0000.0")} [{deltaTime.ToString("00.00")}ms] | DrawCallCount: {_renderer.DrawCallCount:000000}", new (5, 5), 0.5f, new Vector4(1, 1, 1, 1));
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
}