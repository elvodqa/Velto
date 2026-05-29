#version 330 core

uniform mat4 viewprojection;

layout (location = 0) in vec2 v_position;
layout (location = 1) in vec2 v_texcoord;
layout (location = 2) in vec4 v_color;

out vec2 texcoord;
out vec4 col;

void main()
{
    gl_Position = viewprojection * vec4(v_position, 0.0, 1.0);
    texcoord = vec2(v_texcoord.x, v_texcoord.y);
    col = v_color;
}