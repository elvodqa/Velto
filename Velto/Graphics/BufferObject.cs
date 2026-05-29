using System;

namespace Velto.Graphics;

using OpenTK.Graphics.OpenGL;

public class BufferObject<TDataType> : IDisposable where TDataType : unmanaged
{
    private int _handle;
    private BufferTarget _bufferTarget;
    private BufferUsage _bufferUsage;
    
    public unsafe BufferObject(Span<TDataType> data, BufferTarget bufferTarget, BufferUsage bufferUsage)
    {
        _bufferTarget = bufferTarget;
        _bufferUsage = bufferUsage;
        
        _handle = GL.GenBuffer();
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

    public void Dispose()
    {
        GL.DeleteBuffer(_handle);
    }
    
}