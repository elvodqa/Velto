using OpenTK.Graphics.OpenGL;

namespace Velto.Graphics;

public unsafe class BufferObject<TDataType> : IDisposable where TDataType : unmanaged
{
    private readonly BufferTarget _bufferTarget;
    private BufferUsage _bufferUsage;
    private readonly int _handle;

    /// <summary>
    ///     Creates a GL buffer without allocating storage. You must call BufferData/Allocate before BufferSubData.
    /// </summary>
    public BufferObject(BufferTarget target)
    {
        _bufferTarget = target;
        _handle = GL.GenBuffer();
    }

    public BufferObject(int elementCount, BufferTarget bufferTarget, BufferUsage bufferUsage)
    {
        _bufferTarget = bufferTarget;
        _bufferUsage = bufferUsage;

        _handle = GL.GenBuffer();
        Allocate(elementCount, bufferUsage);
    }

    public BufferObject(Span<TDataType> data, BufferTarget bufferTarget, BufferUsage bufferUsage)
    {
        _bufferTarget = bufferTarget;
        _bufferUsage = bufferUsage;

        _handle = GL.GenBuffer();
        BufferData(data, bufferTarget, bufferUsage);
    }

    public void Dispose()
    {
        GL.DeleteBuffer(_handle);
    }

    public void Allocate(int elementCount, BufferUsage bufferUsage)
    {
        Bind();
        GL.BufferData(_bufferTarget, elementCount * sizeof(TDataType), IntPtr.Zero, bufferUsage);
    }

    public void BufferData(Span<TDataType> data, BufferTarget bufferTarget, BufferUsage bufferUsage)
    {
        Bind();
        fixed (void* d = data)
        {
            GL.BufferData(_bufferTarget, data.Length * sizeof(TDataType), d, bufferUsage);
        }
    }

    public void Bind()
    {
        GL.BindBuffer(_bufferTarget, _handle);
    }
}