using System.IO;
using SDL3;

namespace Velto;


public class Resources
{
    public static string GetPath(string filename)
    {
        return Path.Combine(SDL.GetBasePath(), filename);
        //return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), filename);
    }

    public static string GetFontPath(string fontname)
    {
        return GetPath($"Resources/Fonts/{fontname}");
    }
}