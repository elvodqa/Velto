using Velto.Core;
using Velto.Views;

namespace Velto;

public class Velto : Game
{
    public Velto(GameCreateInfo createInfo) : base(createInfo)
    {
    }

    public override void Load()
    {
        base.Load();
        
        ViewManager.Instance.SetTree([
            View.Create<GameView>((int)WindowSizeInPixels.X, (int)WindowSizeInPixels.Y),
            View.Create<SongSelectorView>((int)WindowSizeInPixels.X, (int)WindowSizeInPixels.Y),
        ]);
    }
}