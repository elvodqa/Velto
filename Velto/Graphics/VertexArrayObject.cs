namespace Velto.Graphics;

using OpenTK.Graphics.OpenGL;

public unsafe class VertexArrayObject<TVertexType, TIndexType> : IDisposable
    where TVertexType : unmanaged
    where TIndexType : unmanaged
{
    private int _handle;
    
    public VertexArrayObject(BufferObject<TVertexType> vbo, BufferObject<TIndexType> ebo)
    {
        _handle = GL.GenVertexArray();
        Bind();
        vbo.Bind();
        ebo.Bind();
    }

    public void VertexAttributePointer(uint index, int count, VertexAttribPointerType type, uint length, int offSet)
    {
        //GL.VertexAttribPointer(index, count, type, false, (int)(vertexSize * sizeof(TVertexType)), (void*) (offSet * sizeof(TVertexType)));
        GL.VertexAttribPointer(index, count, type, false, (int)(length), (void*) offSet);
        GL.EnableVertexAttribArray(index);
    }

    public void Bind()
    {
        GL.BindVertexArray(_handle);
    }
    
    public void Unbind()
    {
        GL.BindVertexArray(0);
    }

    public void Dispose()
    {
        GL.DeleteVertexArray(_handle);
    }
}