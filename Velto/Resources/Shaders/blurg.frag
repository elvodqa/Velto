#version 330 core

uniform sampler2D oTexture;
in vec2 texcoord;
in vec4 col;

out vec4 FragColor;

void main()
{
    FragColor = col * texture(oTexture, texcoord);
}