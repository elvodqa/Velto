using System.Text;
using Velto.Core;

namespace Velto.Game.osu;

public enum Keypress
{
    M1 = (1 << 0),
    M2 = (1 << 1),
    K1 = (1 << 2) | (1 << 0),
    K2 = (1 << 3) | (1 << 1),
    Smoke = (1 << 4)
}

public enum ModType
{
    None = 0,
    NoFail = (1 << 0),
    Easy = (1 << 1),
    TouchDevice = (1 << 2),
    Hidden = (1 << 3),
    HardRock = (1 << 4),
    SuddenDeath = (1 << 5),
    DoubleTime = (1 << 6),
    Relax = (1 << 7),
    HalfTime = (1 << 8),
    Nightcore = (1 << 9), // Note DT is applied whenever NC is applied
    Flashlight = (1 << 10),
    Autoplay = (1 << 11),
    SpunOut = (1 << 12),
    Relax2 = (1 << 13), // Autopilot
    Perfect = (1 << 14),
    Key4 = (1 << 15),
    Key5 = (1 << 16),
    Key6 = (1 << 17),
    Key7 = (1 << 18),
    Key8 = (1 << 19),
    keyMod = (1 << 20), // k4+k5+k6+k7+k8
    FadeIn = (1 << 21),
    Random = (1 << 22),
    LastMod = (1 << 23), // Cinema
    TargetPractice = (1 << 24),
    Key9 = (1 << 25),
    Coop = (1 << 26),
    Key1 = (1 << 27),
    Key3 = (1 << 28),
    Key2 = (1 << 29),
    ScoreV2 = (1 << 30),
    Mirror = (1 << 31)
}

public static class Mod
{
    /// <summary>
    /// Get the list of mods used for a replay.
    /// </summary>
    /// <param name="encoding">Encoding of mods used from an .osr file.</param>
    /// <returns>A list of the mods used.</returns>
    public static List<ModType> GetModsList(int encoding)
    {
        List<ModType> results = new List<ModType>();

        foreach (ModType currentMod in Enum.GetValues(typeof(ModType)))
        {
            // Bitwise AND to check if mod is applied
            if (((int)currentMod & encoding) != 0)
            {
                // Remove DT if NC is used
                if (currentMod == ModType.Nightcore && results.Contains(ModType.DoubleTime))
                {
                    results.Remove(ModType.DoubleTime);
                }

                results.Add(currentMod);
            }
        }

        return results;
    }

    public static List<Keypress> GetKeysPressed(int encoding)
    {
        List<Keypress> results = new List<Keypress>();

        foreach (Keypress kp in Enum.GetValues(typeof(Keypress)))
        {
            // Bitwise AND to check if key is pressed
            if (((int)kp & encoding) != 0)
            {
                // Remove mouse buttons when key is pressed
                if (kp == Keypress.K1 && results.Contains(Keypress.M1))
                {
                    results.Remove(Keypress.M1);
                }

                if (kp == Keypress.K2 && results.Contains(Keypress.M2))
                {
                    results.Remove(Keypress.M2);
                }

                results.Add(kp);
            }
        }

        return results;
    }

    /// <summary>
    /// Get the abreviation of a ModType.
    /// </summary>
    /// <param name="type">ModType.</param>
    /// <returns>The ModType's abbreviation.</returns>
    public static string ToStringAbbrev(ModType type)
    {
        switch (type)
        {
            case ModType.None:
                return string.Empty;
            case ModType.NoFail:
                return "NF";
            case ModType.Easy:
                return "EZ";
            case ModType.TouchDevice:
                return "TD";
            case ModType.Hidden:
                return "HD";
            case ModType.HardRock:
                return "HR";
            case ModType.SuddenDeath:
                return "SD";
            case ModType.DoubleTime:
                return "DT";
            case ModType.Relax:
                return "RX";
            case ModType.HalfTime:
                return "HT";
            case ModType.Nightcore:
                return "NC";
            case ModType.Flashlight:
                return "FL";
            case ModType.Autoplay:
                return "AT";
            case ModType.SpunOut:
                return "SO";
            case ModType.Relax2:
                return "AP";
            case ModType.Perfect:
                return "PF";
            case ModType.Key4:
                return "4K";
            case ModType.Key5:
                return "5K";
            case ModType.Key6:
                return "6K";
            case ModType.Key7:
                return "7K";
            case ModType.Key8:
                return "8K";
            case ModType.keyMod:
                return "xK"; // Not sure what this means, I don't play mania haha
            case ModType.FadeIn:
                return "FI";
            case ModType.Random:
                return "RD";
            case ModType.LastMod:
                return "CM";
            case ModType.TargetPractice:
                return "TP";
            case ModType.Key9:
                return "9K";
            case ModType.Coop:
                return "CP";
            case ModType.Key1:
                return "1K";
            case ModType.Key2:
                return "2K";
            case ModType.Key3:
                return "3K";
            case ModType.ScoreV2:
                return "SV2";
            case ModType.Mirror:
                return "MR";
            default:
                return string.Empty;
        }
    }

    /// <summary>
    /// Gets the abbreviations of all mods used for a replay.
    /// </summary>
    /// <param name="modsUsed">List of mods used.</param>
    /// <returns>All their abbrevivations as they'd appear in game.</returns>
    public static string ToModsAbbre(List<ModType> modsUsed)
    {
        string result = string.Empty;

        foreach (ModType modType in modsUsed)
        {
            result += ToStringAbbrev(modType);
        }

        return result;
    }
}

public enum Ruleset
{
    Standard = 0,
    Taiko = 1,
    Fruits = 2,
    Mania = 3
}

public class ReplayFrame
{
    public long MsSinceStart { get; set; }
    public long MsSincePreviousFrame { get; set; }
    public float X { get; set; }
    public float Y { get; set; }

    public List<Keypress> KeysPressed { get; set; }

    public ReplayFrame(long msSinceStart, long msSincePreviousFrame, float x, float y, int keysPressedEncoding)
    {
        MsSinceStart = msSinceStart;
        MsSincePreviousFrame = msSincePreviousFrame;
        X = x;
        Y = y;
        KeysPressed = Mod.GetKeysPressed(keysPressedEncoding);
    }

    public override string ToString()
    {
        return "[At Time: " + MsSinceStart + "ms, at (" + X + ", " + Y + "), keys (" + string.Join(", ", KeysPressed) +
               ") pressed.]";
    }
}

public class LifeBarEntry
{
    public int Time { get; set; }
    public float Health { get; set; }

    public LifeBarEntry(int time, float health)
    {
        Time = time;
        Health = health;
    }

    public override string ToString()
    {
        return "[Time: " + Time + ", HP: " + Health + "]";
    }
}

public class Replay
{
    public Ruleset Ruleset { get; set; }
    public int GameVersion { get; set; }
    public string BeatmapHash { get; set; }
    public string PlayerName { get; set; }
    public string ReplayHash { get; set; }

    public short Count300s { get; set; }
    public short Count100s { get; set; }
    public short Count50s { get; set; }
    public short Gekis { get; set; }
    public short Katus { get; set; }
    public short Misses { get; set; }

    public int TotalScore { get; set; }
    public short MaxCombo { get; set; }
    public bool PerfectFullCombo { get; set; }
    public List<ModType> ModsUsed { get; set; }

    public List<LifeBarEntry> LifeBarGraph { get; set; }
    public DateTime DateSet { get; set; }

    public int ReplayDataLength { get; set; }
    public List<ReplayFrame> Frames { get; set; }

    public long OnlineScoreID { get; set; }
    public int Seed { get; set; }
    public double AdditionalModInfo { get; set; }

    public static Replay ParseReplay(String filePath)
    {
        if (File.Exists(filePath))
        {
            Replay replay = new Replay();

            using (var stream = File.Open(filePath, FileMode.Open))
            {
                using (var reader = new BinaryReader(stream))
                {
                    try
                    {
                        replay.Ruleset = (Ruleset)reader.ReadByte();
                        replay.GameVersion = reader.ReadInt32();
                        replay.BeatmapHash = Util.ReadOsrString(reader);
                        replay.PlayerName = Util.ReadOsrString(reader);
                        replay.ReplayHash = Util.ReadOsrString(reader);

                        replay.Count300s = reader.ReadInt16();
                        replay.Count100s = reader.ReadInt16();
                        replay.Count50s = reader.ReadInt16();
                        replay.Gekis = reader.ReadInt16();
                        replay.Katus = reader.ReadInt16();
                        replay.Misses = reader.ReadInt16();

                        replay.TotalScore = reader.ReadInt32();
                        replay.MaxCombo = reader.ReadInt16();
                        replay.PerfectFullCombo = reader.ReadByte() == 1;
                        replay.ModsUsed = Mod.GetModsList(reader.ReadInt32());

                        // Life bar parsing
                        replay.LifeBarGraph = new List<LifeBarEntry>();
                        string[] lifebarEntries = Util.ReadOsrString(reader).Split(',');
                        foreach (string entry in lifebarEntries)
                        {
                            if (entry == "")
                            {
                                break;
                            }

                            string[] values = entry.Split("|");
                            int time = Convert.ToInt32(values[0]);
                            float health = (float)Convert.ToDouble(values[1]);
                            replay.LifeBarGraph.Add(new LifeBarEntry(time, health));
                        }

                        replay.DateSet = new DateTime(reader.ReadInt64(), DateTimeKind.Local);

                        replay.ReplayDataLength = reader.ReadInt32();

                        // LZMA Decompression
                        replay.Frames = new List<ReplayFrame>();
                        byte[] rawReplayData = reader.ReadBytes(replay.ReplayDataLength);
                        byte[] decompressedData = Util.Decompress(rawReplayData);
                        string[] frames = Encoding.ASCII.GetString(decompressedData).Split(',');
                        long currentMs = 0;

                        for (int i = 0; i < frames.Length; i++)
                        {
                            string[] frameInfo = frames[i].Split('|');

                            long prevMs = (long)Convert.ToDouble(frameInfo[0]);
                            float currentX = (float)Convert.ToDouble(frameInfo[1]);
                            float currentY = (float)Convert.ToDouble(frameInfo[2]);
                            int keyPresses = Convert.ToInt32(frameInfo[3]);

                            // Send the replay seed if we get -12345|0|0|seed
                            if (frameInfo[0] == "-12345")
                            {
                                replay.Seed = Convert.ToInt32(frameInfo[3]);
                                break;
                            }

                            // A frame's absolute time is the running total *including* this
                            // frame's delta. Accumulate first so the frame is stamped with its
                            // real time instead of the previous frame's (which judged everything
                            // one frame early).
                            currentMs += prevMs;

                            ReplayFrame rf = new ReplayFrame(currentMs, prevMs, currentX, currentY, keyPresses);
                            replay.Frames.Add(rf);
                        }

                        replay.OnlineScoreID = reader.ReadInt64();
                        if (replay.ModsUsed.Contains(ModType.TargetPractice))
                        {
                            replay.AdditionalModInfo = reader.ReadDouble();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Exception when attempting to parse replay: " + ex);
                    }
                }
            }

            return replay;
        }
        else
        {
            throw new FileNotFoundException();
        }
    }
}