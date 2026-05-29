using System.IO;

namespace Velto;

using SDL;
using static SDL.SDL3;

public class Resources
{
    public static string GetPath(string filename)
    {
        return Path.Combine(SDL_GetBasePath(), filename);
        //return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), filename);
    }
}