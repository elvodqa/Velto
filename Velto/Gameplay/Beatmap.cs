namespace Velto.Gameplay;

public class Beatmap
{
    public string Filename;
    public string Folder; // path to folder containing the map
    
    // General
    public string AudioFilename { get; set; }
    public int AudioLeadIn { get; set; } // Milliseconds of silence before the audio starts playing
    public float StackLeniency { get; set; } = 0.7f;
    public int Mode { get; set; } // 0 = osu!, 1 = osu!taiko, 2 = osu!catch, 3 = osu!mania
    
    // Metadata
    public string Title { get; set; }
    public string Artist { get; set; }
    public string Creator { get; set; }
    public string Version { get; set; } // difficulty name
    
    // Difficulty
    public float HPDrainRate { get; set; }
    public float CircleSize { get; set; }
    public float OverallDifficulty { get; set; }
    public float ApproachRate { get; set; }
    public float SliderMultiplier { get; set; }
    public float SliderTickRate { get; set; }
    
    public string BackgroundFile { get; set; }
    
    public List<HitObject> HitObjects { get; set; }

    const byte ComboSkipMask = 0b0111_0000;
    public Beatmap(string filePath)
    {
        HitObjects = new();
        Filename = Path.GetFileName(filePath);
        Folder = Path.GetDirectoryName(filePath)!;

        bool reachedHitobjects = false;
        bool reachedEvents = false;
        foreach (string line in File.ReadAllLines(filePath))
        {
            if (line.StartsWith("//")) continue;
            if (line.StartsWith(" ")) continue;
            if (line == "\n") continue;
            if (line.StartsWith("[HitObjects]"))
            {
                reachedHitobjects = true;
                reachedEvents = false;
                continue;
            }

            if (line.StartsWith("[Events]"))
            {
                reachedEvents = true;
                reachedHitobjects = false;
                continue;
            }

            if (!reachedHitobjects && !reachedEvents)
            {
                if (!line.Contains(":")) continue;
                string[] split = line.Split(":");
                string first = split[0].Trim();
                string second = split[1].Trim();
                switch (first)
                {
                    case "AudioFilename":
                        AudioFilename = second.Trim();
                        break;
                    case "AudioLeadIn":
                        AudioLeadIn = int.Parse(second);
                        break;
                    case "StackLeniency":
                        StackLeniency = float.Parse(second);
                        break;
                    case "Mode":
                        Mode = int.Parse(second);
                        break;
                    case "Title":
                        Title = second;
                        break;
                    case "Artist":
                        Artist = second;
                        break;
                    case "Creator":
                        Creator = second;
                        break;
                    case "Version":
                        Version = second;
                        break;
                    case "HPDrainRate":
                        HPDrainRate = float.Parse(second);
                        break;
                    case "CircleSize":
                        CircleSize = float.Parse(second);
                        break;
                    case "OverallDifficulty":
                        OverallDifficulty = float.Parse(second);
                        break;
                    case "ApproachRate":
                        ApproachRate = float.Parse(second);
                        break;
                    case "SliderMultiplier":
                        SliderMultiplier = float.Parse(second);
                        break;
                    case "SliderTickRate":
                        SliderTickRate = float.Parse(second);
                        break;
                }
            }
            else if (reachedEvents)
            {
                if (line.StartsWith("0"))
                {
                    string[] split = line.Split(",");
                    BackgroundFile = split[2];
                    BackgroundFile = BackgroundFile.Substring(1, BackgroundFile.Length - 2);
                }
            }
            else if (reachedHitobjects)
            {
                string[] split = line.Split(",");
                int x, y, time;
                x = int.Parse(split[0]);
                y = int.Parse(split[1]);
                time = int.Parse(split[2]);
                int type = int.Parse(split[3]);
                
                var flags = (HitObjectType)(type & 0b1000_1111); 
                // keep bits 0–3 and 7 as enum flags

                int comboSkip = (type & ComboSkipMask) >> 4;

                bool isCircle = flags.HasFlag(HitObjectType.Circle);
                bool isSlider = flags.HasFlag(HitObjectType.Slider);
                bool isNewCombo = flags.HasFlag(HitObjectType.NewCombo);
                bool isSpinner = flags.HasFlag(HitObjectType.Spinner);
                bool isHold = flags.HasFlag(HitObjectType.ManiaHold);

                //Console.WriteLine($"Flags: {flags}");
                //Console.WriteLine($"Combo skip: {comboSkip}");

                if (isCircle)
                {
                    HitCircle hitCircle = new();
                    hitCircle.NewCombo = isNewCombo;
                    hitCircle.Time = time;
                    hitCircle.Position = new(x, y);

                    HitObjects.Add(hitCircle);
                } else if (isNewCombo)
                {
                    
                }
            }
        }
    }
    
}