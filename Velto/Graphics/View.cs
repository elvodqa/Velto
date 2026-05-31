namespace Velto.Graphics;

public abstract class View : IDisposable
{
    public float Width;
    public float Height;
    
    public abstract void Update(double delta);
    public abstract void Draw(double delta);

    public void Dispose()
    {
        // TODO release managed resources here
    }
}