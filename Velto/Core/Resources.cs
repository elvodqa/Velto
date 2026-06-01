using System.IO;
using System.Runtime.InteropServices;

namespace Velto.Core;

using SDL;
using static SDL.SDL3;

public class Resources
{
    public static string GetPath(string filename)
    {
        // if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        // {
        //     string baseDir = AppContext.BaseDirectory;
        //
        //     return Path.GetFullPath(Path.Combine(baseDir, "..", /*"Resources",*/ filename));
        // }
        
        return Path.Combine(SDL_GetBasePath(), filename);
        //return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), filename);
    }

    public static string GetFontPath(string fontname)
    {
        return GetPath($"Resources/Fonts/{fontname}");
    }
    
    public static string DefaultSkinPath => GetPath($"Resources/Textures/default");
    
}