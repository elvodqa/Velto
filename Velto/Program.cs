using System;
using System.Threading;
using Velto.UserInterface;

namespace Velto;

class Program
{
    static void Main(string[] args)
    {
        if (Environment.GetEnvironmentVariable("RENDERDOC_CAPTURE") != null)
        {
            Thread.Sleep(1000);
        }
        
        using (GameBase game = new()) 
        {
            game.Run();
        }
    }
}   