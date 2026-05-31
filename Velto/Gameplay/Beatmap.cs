using OpenTK.Mathematics;
using Velto.Graphics;
using SDL;
using static SDL.SDL3;

namespace Velto.Gameplay;

public struct TimingPoint
{
    public double Time;
    public double BeatLength;
    public int Meter;
    public int SampleSet;
    public int SampleIndex;
    public int Volume;
    public int Uninherited; // (0 or 1): Whether or not the timing point is uninherited.
}

public unsafe class Beatmap
{
    private const byte ComboSkipMask = 0b0111_0000;
    public string Filename;
    public string Folder; // path to folder containing the map

    public Beatmap(string filePath)
    {
        HitObjects = new();
        TimingPoints = new();
        Filename = Path.GetFileName(filePath);
        Folder = Path.GetDirectoryName(filePath)!;

        var reachedGeneral = true;
        var reachedTimingPoints = false;
        var reachedHitobjects = false;
        var reachedEvents = false;
        foreach (var line in File.ReadAllLines(filePath))
        {
            if (line.StartsWith("//")) continue;
            if (line.StartsWith(" ")) continue;
            if (line.StartsWith("\n")) continue;
            if (string.IsNullOrWhiteSpace(line))
                continue;
            if (line == "\n") continue;
            
            if (line.StartsWith("[HitObjects]"))
            {
                reachedHitobjects = true;
                reachedEvents = false;
                reachedTimingPoints = false;
                reachedGeneral = false;
                continue;
            }

            if (line.StartsWith("[Events]"))
            {
                reachedEvents = true;
                reachedHitobjects = false;
                reachedTimingPoints = false;
                reachedGeneral = false;
                continue;
            }
            if (line.StartsWith("[TimingPoints]"))
            {
                reachedEvents = false;
                reachedHitobjects = false;
                reachedTimingPoints = true;
                reachedGeneral = false;
                continue;
            }
            
            if (reachedGeneral)
            {
                if (!line.Contains(":")) continue;
                var split = line.Split(":");
                var first = split[0].Trim();
                var second = split[1].Trim();
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
                    var split = line.Split(",");
                    BackgroundFile = split[2];
                    BackgroundFile = BackgroundFile.Substring(1, BackgroundFile.Length - 2);
                }
            }
            else if (reachedTimingPoints)
            {
                var split = line.Split(",");
                
                if (split.Length < 7)
                {
                    // colors are here
                    //Console.WriteLine($"Invalid timing point: {line}");
                    continue;
                }
                TimingPoint timingPoint = new();
                timingPoint.Time = double.Parse(split[0]);
                timingPoint.BeatLength = double.Parse(split[1]);
                timingPoint.Meter = int.Parse(split[2]);
                timingPoint.SampleSet = int.Parse(split[3]);
                timingPoint.SampleIndex = int.Parse(split[4]);
                timingPoint.Volume = int.Parse(split[5]);
                timingPoint.Uninherited = int.Parse(split[6]);
                TimingPoints.Add(timingPoint);
            }
            else if (reachedHitobjects)
            {
                var split = line.Split(",");
                int x, y, time;
                x = int.Parse(split[0]);
                y = int.Parse(split[1]);
                time = int.Parse(split[2]);
                var type = int.Parse(split[3]);

                var flags = (HitObjectType)(type & 0b1000_1111);
                // keep bits 0–3 and 7 as enum flags

                var comboSkip = (type & ComboSkipMask) >> 4;

                var isCircle = flags.HasFlag(HitObjectType.Circle);
                var isSlider = flags.HasFlag(HitObjectType.Slider);
                var isNewCombo = flags.HasFlag(HitObjectType.NewCombo);
                var isSpinner = flags.HasFlag(HitObjectType.Spinner);
                var isHold = flags.HasFlag(HitObjectType.ManiaHold);

                //Console.WriteLine($"Flags: {flags}");
                //Console.WriteLine($"Combo skip: {comboSkip}");

                if (isCircle)
                {
                    HitCircle hitCircle = new();
                    hitCircle.NewCombo = isNewCombo;
                    hitCircle.Time = time;
                    hitCircle.Position = new Vector2(x, y);

                    HitObjects.Add(hitCircle);
                }
                else if (isSlider)
                {
                    Slider slider = new();
                    slider.NewCombo = isNewCombo;
                    slider.Position = new Vector2(int.Parse(split[0]), int.Parse(split[1]));
                    slider.Time = int.Parse(split[2]);
                    
                    //slider.HitSound = int.Parse(split[4]);
                    var sliderData = split[5].Split('|');
                    var defaultCurveType = sliderData[0] switch
                    {
                        "B" => CurveType.Bezier,
                        "C" => CurveType.Catmull,
                        "L" => CurveType.Linear,
                        "P" => CurveType.Perfect,
                        _ => CurveType.Linear
                    };

                    slider.CurvePoints.Add(new CurvePoint
                    {
                        Position = slider.Position,
                        Type = defaultCurveType
                    });

                    for (var i = 1; i < sliderData.Length; i++)
                    {
                        var point = sliderData[i].Split(':');
                        var curvePoint = new CurvePoint();
                        curvePoint.Position = new Vector2(int.Parse(point[0]), int.Parse(point[1]));
                        if (i > 1 && sliderData[i - 1] == sliderData[i])
                        {
                            curvePoint.Type = CurveType.Linear;
                            // remove i-1
                            slider.CurvePoints.RemoveAt(slider.CurvePoints.Count - 1);
                        }
                        else
                        {
                            curvePoint.Type = defaultCurveType;
                        }

                        slider.CurvePoints.Add(curvePoint);
                    }
                    
                    slider.SlideRepeatCount = int.Parse(split[6]);
                    slider.Length = double.Parse(split[7]);
                    
                    /*if (int.Parse(split[3]) == 6)
                    {
                        curComboNumber = 1;
                        slider.ComboNumber = curComboNumber;
                        curComboNumber++;
                    }
                    else
                    {
                        slider.ComboNumber = curComboNumber;
                        curComboNumber++;
                    }*/
                    HitObjects.Add(slider);
                }
            }
        }

        //CalculatePrepass();
    }

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

    public List<TimingPoint> TimingPoints { get; set; }
    public List<HitObject> HitObjects { get; set; }
    

    public float Preempt;
    public float Posttime;
    
    
    
    public void CalculatePrepass(SDL_Window* window)
    {
        var comboCounter = 0;
        var colorCounter = 0;
        var sortedTimingPoints = TimingPoints.OrderBy(t => t.Time);

        double sliderVelocity = 1f;
        
        foreach (var hitobject in HitObjects)
        {
            float preempt;
            if (ApproachRate < 5)
                preempt = 1200 + 120 * (5 - ApproachRate);
            else if (ApproachRate == 5)
                preempt = 1200;
            else
                preempt = 1200 - 150 * (ApproachRate - 5);
            
            float posttime = 150;

            Preempt = preempt;
            Posttime = posttime;
            
            if (hitobject.NewCombo)
            {
                colorCounter++;
                colorCounter %= 4;
                comboCounter = 0;
            }

            comboCounter++;
            //if (comboCounter >= 9) comboCounter = 9;
            var color = colorCounter switch
            {
                0 => new Vector4(1, 0, 0, 1),
                1 => new Vector4(0, 1, 0, 1),
                2 => new Vector4(0, 0, 1, 1),
                3 => new Vector4(1, 1, 0, 1)
            };

            hitobject.Color = color;
            hitobject.ComboNumber = comboCounter;

            if (hitobject is Slider slider)
            {
                var segments = new List<List<CurvePoint>>();
                var current = new List<CurvePoint>();

                for (var i = 0; i < slider.CurvePoints.Count; i++)
                {
                    var point = slider.CurvePoints[i];
                    current.Add(point);

                    // End segment when reaching a Linear point
                    // but not if it's the very first point in the segment
                    if (point.Type == CurveType.Linear && current.Count > 1)
                    {
                        segments.Add(current);
                        current = new List<CurvePoint>();

                        // carry over the last point as start of next segment
                        current.Add(point);
                    }
                }
                
                // Add remaining points
                if (current.Count > 1) segments.Add(current);

                foreach (var segment in segments)
                {
                    BezierCurve bezier =
                        new(segment.Select(x => new Vector2(x.Position.X, x.Position.Y)));
                    for (float i = 0; i <= 1.0f; i += 0.01f) slider.Points.Add(bezier.CalculatePoint(i));
                }
                
                var timingPoint = TimingPoints[0];
                foreach (var point in sortedTimingPoints)
                {
                    if (point.Time < slider.Time)
                    {
                        timingPoint = point;
                    }
                }
                
                // Calculate length
                
                var length = slider.Length;
                var sliderMultiplier = SliderMultiplier;
                var beatLength = timingPoint.BeatLength;
                
                
                if (timingPoint.Uninherited == 1)
                {
                    //sliderVelocity = 1.0f;
                }
                else
                {
                    //sliderVelocity *= (100.0f / timingPoint.BeatLength);
                }
                
                slider.Duration = length / (sliderMultiplier * 100f * sliderVelocity) * beatLength;
                slider.Duration *= slider.SlideRepeatCount;
                Console.WriteLine(slider.Duration);
            }
        }
    }
}