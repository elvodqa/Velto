using Velto.Core;

namespace Velto.Game;

class Program
{
    static void Main(string[] args)
    {
        using GameBase game = new();
        game.Run();
    }
}