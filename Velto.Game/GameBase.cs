using Velto.Core;
using Velto.Game.Views;

namespace Velto.Game;

public class GameBase : Core.Game
{
    private OsuContext _context;
    
    public GameBase(GameCreateInfo createInfo) : base(createInfo)
    {
        
    }

    public override void Load()
    {
        base.Load();
        
        _context = new OsuContext()
        {
            Skin = new Skin(Resources.GetPath($"Resources/Textures/rafis"))
        };
        
        ViewManager.Instance.SetTree([
            new IntroView(_context),
        ]);
    }
}