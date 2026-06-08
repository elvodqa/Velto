using Velto.Audio;
using Velto.Core;
using Velto.Game.Views;
using Velto.Graphics;

namespace Velto.Game;

public class GameBase : Core.Game
{
    private OsuContext _context;
    
    public GameBase() : base(GraphicsBackend.Metal)
    {
        
    }

    public override void Load()
    {
        base.Load();
        
        _context = new OsuContext()
        {
            Skin = new Skin(GraphicsDevice, Resources.GetPath($"Resources/Textures/rafis")),
            SystemTrack = AudioManager.Instance.CreateTrack(),
        };
        
        SetScreen(new IntroScreen(GraphicsDevice, _context));
        Window.Title = "Velto!";
    }
}