using Velto.Core;

namespace Velto.Game;

class Program
{
    static void Main(string[] args)
    {
        GameCreateInfo gameInfo = new()
        {
            Title = "Velto",
            Maximized = false,
        };

        using GameBase game = new(gameInfo);
        game.Run();
    }
}