using System.Text;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using SDL;

namespace Velto.Graphics;

using static SDL3;

public unsafe class Shader : IDisposable
{
    public Dictionary<string, int> _uniforms = new();

    public Shader(string shaderName)
    {
        var vertexSource =
            File.ReadAllText(Path.Combine(SDL_GetBasePath(), "Resources", "Shaders", $"{shaderName}.vert"));
        var fragmentSource =
            File.ReadAllText(Path.Combine(SDL_GetBasePath(), "Resources", "Shaders", $"{shaderName}.frag"));
        load(vertexSource, fragmentSource);
    }

    public Shader(string vertexSource, string fragmentSource)
    {
        load(vertexSource, fragmentSource);
    }

    public int Handle { get; private set; }

    public void Dispose()
    {
        GL.DeleteProgram(Handle);
    }

    private void load(string vertexSource, string fragmentSource)
    {
        var vertexShader = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vertexShader, vertexSource);
        GL.CompileShader(vertexShader);

        var infoLog = GL.GetShaderInfoLog(vertexShader);
        if (!string.IsNullOrWhiteSpace(infoLog)) throw new Exception($"Error compiling vertex shader: {infoLog}");

        var fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(fragmentShader, fragmentSource);
        GL.CompileShader(fragmentShader);

        infoLog = GL.GetShaderInfoLog(fragmentShader);
        if (!string.IsNullOrWhiteSpace(infoLog)) throw new Exception($"Error compiling fragment shader: {infoLog}");

        Handle = GL.CreateProgram();
        GL.AttachShader(Handle, vertexShader);
        GL.AttachShader(Handle, fragmentShader);
        GL.LinkProgram(Handle);

        infoLog = GL.GetProgramInfoLog(Handle);
        if (!string.IsNullOrWhiteSpace(infoLog)) throw new Exception($"Error linking shader program: {infoLog}");

        GL.DetachShader(Handle, vertexShader);
        GL.DetachShader(Handle, fragmentShader);
        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);

        GL.GetProgrami(Handle, ProgramProperty.ActiveUniforms, out var count);

        const int maxNameLength = 256;
        for (uint i = 0; i < count; i++)
        {
            int length;
            int size;
            var nameBuffer = stackalloc byte[maxNameLength];
            UniformType uniformType;
            GL.GetActiveUniform(Handle, i, maxNameLength, &length, &size, &uniformType, nameBuffer);

            var name = Encoding.ASCII.GetString(nameBuffer, length);

            var cleanName = name.Split('[')[0];

            var location = GL.GetUniformLocation(Handle, cleanName);

            if (location != -1) _uniforms[cleanName] = location;
        }
    }

    public void Use()
    {
        GL.UseProgram(Handle);
    }

    private int GetLocation(string name)
    {
        if (_uniforms.TryGetValue(name, out var loc))
            return loc;

        throw new Exception($"Uniform '{name}' not found in shader.");
    }

    public void SetInt(string name, int value)
    {
        GL.Uniform1i(GetLocation(name), value);
    }

    public void SetFloat(string name, float value)
    {
        GL.Uniform1f(GetLocation(name), value);
    }

    public void SetVector2(string name, Vector2 value)
    {
        GL.Uniform2f(GetLocation(name), value.X, value.Y);
    }

    public void SetVector3(string name, Vector3 value)
    {
        GL.Uniform3f(GetLocation(name), value.X, value.Y, value.Z);
    }

    public void SetVector4(string name, Vector4 value)
    {
        GL.Uniform4f(GetLocation(name), value.X, value.Y, value.Z, value.W);
    }

    public void SetMatrix4(string name, Matrix4 value)
    {
        GL.UniformMatrix4fv(GetLocation(name), 1, false, (float*)&value);
    }
}