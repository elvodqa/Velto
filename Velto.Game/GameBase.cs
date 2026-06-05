using Velto.Core;
using Velto.Game.Views;

namespace Velto.Game;

public class GameBase : Core.Game
{
    public GameBase(GameCreateInfo createInfo) : base(createInfo)
    {
    }

    public override void Load()
    {
        base.Load();
        
        ViewManager.Instance.SetTree([
            View.Create<IntroView>((int)WindowSizeInPixels.X, (int)WindowSizeInPixels.Y),
        ]);
    }
}