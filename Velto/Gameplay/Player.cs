using OpenTK.Mathematics;


using OpenTK.Mathematics;

namespace Velto.Gameplay;

public class Player
{
    public Vector2 Cursor { get; private set; }
    public bool Autoplay { get; set; } = true;

    private readonly Beatmap _beatmap;
    
    public Player(Beatmap beatmap)
    {
        _beatmap = beatmap;

        if (_beatmap.HitObjects.Count > 0)
            Cursor = _beatmap.HitObjects[0].Position;
    }

    public void Update(double deltaTime, double songCursor)
    {
        if (!Autoplay)
        {
            Cursor = new Vector2(Input.MouseX, Input.MouseY);
            return;
        }
        
        
        var objects = _beatmap.HitObjects;

        if (objects.Count == 0)
            return;

        // Find the object we're currently approaching/playing
        int currentIndex = -1;

        for (int i = 0; i < objects.Count; i++)
        {
            if (songCursor < objects[i].Time)
            {
                currentIndex = i;
                break;
            }

            if (objects[i] is Slider slider)
            {
                if (songCursor >= slider.Time &&
                    songCursor <= slider.Time + slider.Duration)
                {
                    currentIndex = i;
                    break;
                }
            }
        }

        if (currentIndex == -1)
        {
            Cursor = objects[^1].Position;
            return;
        }

        var current = objects[currentIndex];

        // ---------------------------------------------------------
        // ACTIVE SLIDER: follow slider ball
        // ---------------------------------------------------------
        if (current is Slider activeSlider &&
            songCursor >= activeSlider.Time &&
            songCursor <= activeSlider.Time + activeSlider.Duration)
        {
            float progress =
                (float)((songCursor - activeSlider.Time) / activeSlider.Duration);

            progress = Math.Clamp(progress, 0f, 1f);
                
            int index = (int)(progress * (activeSlider.Points.Count - 1));
            var position = activeSlider.Points[index];

            Cursor = position;
            return;
        }

        // ---------------------------------------------------------
        // FIRST OBJECT
        // ---------------------------------------------------------
        if (currentIndex == 0)
        {
            Cursor = current.Position;
            return;
        }

        var previous = objects[currentIndex - 1];

        Vector2 startPos;
        double startTime;

        if (previous is Slider previousSlider)
        {
            float progress =
                (float)((songCursor - previousSlider.Time) / previousSlider.Duration);

            progress = Math.Clamp(progress, 0f, 1f);
                
            int index = (int)(progress * (previousSlider.Points.Count - 1));
            var position = previousSlider.Points[index];

            startPos = position;
            startTime = previousSlider.Time + previousSlider.Duration;
        }
        else
        {
            startPos = previous.Position;
            startTime = previous.Time;
        }

        Vector2 endPos = current.Position;
        double endTime = current.Time;

        // ---------------------------------------------------------
        // INTERPOLATE BETWEEN OBJECTS
        // ---------------------------------------------------------
        double duration = endTime - startTime;

        if (duration <= 0)
        {
            Cursor = endPos;
            return;
        }

        float t = (float)((songCursor - startTime) / duration);
        t = Math.Clamp(t, 0f, 1f);

        // SmoothStep for natural motion
        t = t * t * (3f - 2f * t);

        Cursor = Vector2.Lerp(startPos, endPos, t);
    }
}