#version 330 core

layout (location = 0) in vec3 aPos;
layout (location = 1) in vec2 aTexCoord;

layout (location = 2) in mat4 model;
layout (location = 6) in vec4 color;

out vec2 TexCoord;
out vec4 Color;

uniform mat4 view;
uniform mat4 projection;

void main()
{
    Color = color;
    TexCoord = aTexCoord;
    gl_Position = projection * view * model * vec4(aPos, 1.0);
}