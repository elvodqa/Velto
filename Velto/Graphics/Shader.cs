using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Velto.Graphics;

using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

using SDL;
using static SDL.SDL3;

public unsafe class Shader : IDisposable
{
    public int Handle { get; private set; }
    
    public Dictionary<string, int> _uniforms = new Dictionary<string, int>();

    public Shader(string shaderName)
    {
        string vertexSource = File.ReadAllText(Path.Combine(SDL_GetBasePath(), "Resources", "Shaders", $"{shaderName}.vert"));
        string fragmentSource = File.ReadAllText(Path.Combine(SDL_GetBasePath(), "Resources", "Shaders", $"{shaderName}.frag"));
        load(vertexSource, fragmentSource);
    }

    public Shader(string vertexSource, string fragmentSource)
    {
        load(vertexSource, fragmentSource);
    }

    private void load(string vertexSource, string fragmentSource)
    {
        int vertexShader = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vertexShader, vertexSource);
        GL.CompileShader(vertexShader);

        string infoLog = GL.GetShaderInfoLog(vertexShader);
        if (!string.IsNullOrWhiteSpace(infoLog))
        {
            throw new Exception($"Error compiling vertex shader: {infoLog}");
        }

        int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(fragmentShader, fragmentSource);
        GL.CompileShader(fragmentShader);

        infoLog = GL.GetShaderInfoLog(fragmentShader);
        if (!string.IsNullOrWhiteSpace(infoLog))
        {
            throw new Exception($"Error compiling fragment shader: {infoLog}");
        }

        Handle = GL.CreateProgram();
        GL.AttachShader(Handle, vertexShader);
        GL.AttachShader(Handle, fragmentShader);
        GL.LinkProgram(Handle);

        infoLog = GL.GetProgramInfoLog(Handle);
        if (!string.IsNullOrWhiteSpace(infoLog))
        {
            throw new Exception($"Error linking shader program: {infoLog}");
        }

        GL.DetachShader(Handle, vertexShader);
        GL.DetachShader(Handle, fragmentShader);
        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);
        
        GL.GetProgrami(Handle, ProgramProperty.ActiveUniforms, out int count);

        const int maxNameLength = 256;
        for (uint i = 0; i < count; i++)
        {

            int length;
            int size;
            byte* nameBuffer = stackalloc byte[maxNameLength];
            UniformType uniformType;
            GL.GetActiveUniform(Handle, i, maxNameLength, &length, &size, &uniformType, nameBuffer);

            string name = System.Text.Encoding.ASCII.GetString(nameBuffer, length);

            string cleanName = name.Split('[')[0];

            int location = GL.GetUniformLocation(Handle, cleanName);

            if (location != -1)
            {
                _uniforms[cleanName] = location;
            }
        }
    }

    public void Use()
    {
        GL.UseProgram(Handle);
    }
    
    private int GetLocation(string name)
    {
        if (_uniforms.TryGetValue(name, out int loc))
            return loc;

        throw new Exception($"Uniform '{name}' not found in shader.");
    }

    public void SetInt(string name, int value)
        => GL.Uniform1i(GetLocation(name), value);

    public void SetFloat(string name, float value)
        => GL.Uniform1f(GetLocation(name), value);

    public void SetVector2(string name, Vector2 value)
        => GL.Uniform2f(GetLocation(name), value.X, value.Y);

    public void SetVector3(string name, Vector3 value)
        => GL.Uniform3f(GetLocation(name), value.X, value.Y, value.Z);

    public void SetVector4(string name, Vector4 value)
        => GL.Uniform4f(GetLocation(name), value.X, value.Y, value.Z, value.W);

    public void SetMatrix4(string name, Matrix4 value)
    {
        GL.UniformMatrix4fv(GetLocation(name), 1, false, (float*)&value);
    }

    public void Dispose()
    {
        GL.DeleteProgram(Handle);
    }
}