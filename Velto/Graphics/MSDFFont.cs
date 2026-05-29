using System.Text.Json;
using OpenTK.Graphics.OpenGL;
using StbImageSharp;

namespace Velto.Graphics;

public class MSDFFont
{
    public float Ascender;
    public int AtlasHeight;
    public int AtlasTexture;
    public int AtlasWidth;
    public float Descender;
    public float DistanceRange;
    public float EmSize;

    public Dictionary<uint, Glyph> Glyphs = new();
    public Dictionary<(uint, uint), float> Kerning = new();
    public float LineHeight;

    public static MSDFFont Load(string basePath)
    {
        MSDFFont font = new();
        LoadTexture(font, basePath + ".png");
        LoadJson(font, basePath + ".json");
        return font;
    }

    private static void LoadTexture(MSDFFont font, string path)
    {
        // Texture.cs sets STB to flip images globally; make the font atlas load deterministic.
        // Our UV computation in LoadJson assumes the atlas image is NOT flipped on load.
        StbImage.stbi_set_flip_vertically_on_load(0);

        using var stream = File.OpenRead(path);
        var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

        // Restore default used by Texture to avoid surprising later loads.
        StbImage.stbi_set_flip_vertically_on_load(1);

        var tex = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, tex);
        GL.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, image.Width, image.Height, 0,
            PixelFormat.Rgba, PixelType.UnsignedByte, image.Data);
        GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameteri(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        font.AtlasTexture = tex;
        font.AtlasWidth = image.Width;
        font.AtlasHeight = image.Height;
    }

    private static void LoadJson(MSDFFont font, string path)
    {
        var jsonText = File.ReadAllText(path);
        var doc = JsonDocument.Parse(jsonText);
        var root = doc.RootElement;
        var atlas = root.GetProperty("atlas");
        font.DistanceRange = atlas.GetProperty("distanceRange").GetSingle();
        font.EmSize = atlas.GetProperty("size").GetSingle();
        var metrics = root.GetProperty("metrics");
        font.LineHeight = metrics.GetProperty("lineHeight").GetSingle();
        font.Ascender = metrics.GetProperty("ascender").GetSingle();
        font.Descender = metrics.GetProperty("descender").GetSingle();
        foreach (var glyphJson in root.GetProperty("glyphs").EnumerateArray())
        {
            var unicode = glyphJson.GetProperty("unicode").GetUInt32();
            Glyph glyph = new();
            glyph.XAdvance = glyphJson.GetProperty("advance").GetSingle();
            if (glyphJson.TryGetProperty("atlasBounds", out var atlasBounds) &&
                glyphJson.TryGetProperty("planeBounds", out var planeBounds))
            {
                glyph.HasGeometry = true;
                glyph.X = atlasBounds.GetProperty("left").GetSingle();
                glyph.Y = atlasBounds.GetProperty("bottom").GetSingle();
                var right = atlasBounds.GetProperty("right").GetSingle();
                var top = atlasBounds.GetProperty("top").GetSingle();
                glyph.Width = right - glyph.X;
                glyph.Height = top - glyph.Y;
                glyph.XOffset = planeBounds.GetProperty("left").GetSingle();
                glyph.YOffset = planeBounds.GetProperty("bottom").GetSingle();
                glyph.U0 = glyph.X / font.AtlasWidth;
                glyph.V0 = 1f - (glyph.Y + glyph.Height) / font.AtlasHeight;
                glyph.U1 = (glyph.X + glyph.Width) / font.AtlasWidth;
                glyph.V1 = 1f - glyph.Y / font.AtlasHeight;
            }

            font.Glyphs[unicode] = glyph;
        }

        if (root.TryGetProperty("kerning", out var kernings))
            foreach (var k in kernings.EnumerateArray())
            {
                var first = k.GetProperty("unicode1").GetUInt32();
                var second = k.GetProperty("unicode2").GetUInt32();
                var advance = k.GetProperty("advance").GetSingle();
                font.Kerning[(first, second)] = advance;
            }
    }

    public float GetKerning(uint first, uint second)
    {
        if (Kerning.TryGetValue((first, second), out var k)) return k;
        return 0f;
    }

    public struct Glyph
    {
        public float X;
        public float Y;
        public float Width;
        public float Height;
        public float XOffset;
        public float YOffset;
        public float XAdvance;
        public float U0;
        public float V0;
        public float U1;
        public float V1;
        public bool HasGeometry;
    }
}