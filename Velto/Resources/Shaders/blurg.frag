#version 330 core

uniform sampler2D texture;
varying vec2 texcoord;
varying vec4 col;
void main()
{
    gl_FragColor = col * texture2D(texture, texcoord);
}