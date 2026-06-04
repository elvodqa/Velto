using System;
using System.Threading;
using Velto.Core;

namespace Velto;

class Program
{
    static void Main(string[] args)
    {
        GameCreateInfo gameInfo = new()
        {
            Title = "Velto",
            Maximized = false,
        };

        using Velto game = new(gameInfo);
        game.Run();

        // using (GameBase game = new()) 
        // {
        //     game.Run();
        // }
    }
}   