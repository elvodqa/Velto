#version 330 core
uniform mat4 viewprojection;

attribute vec2 v_position;
attribute vec2 v_texcoord;
attribute vec4 v_color;

varying vec2 texcoord;
varying vec4 col;
void main()
{
    gl_Position = viewprojection * vec4(v_position, 0.0, 1.0);
    texcoord = vec2(v_texcoord.x, v_texcoord.y);
    col = v_color;
}