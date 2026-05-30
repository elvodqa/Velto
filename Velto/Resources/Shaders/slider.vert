#version 330 core

layout(location = 0) in vec3 aPos;
layout(location = 1) in vec2 aUV;
layout(location = 2) in float aProgress;

uniform mat4 uProjection;
uniform mat4 uView;
uniform mat4 uModel;

out vec2 vUV;
out float vProgress;

void main()
{
    vUV = aUV;
    vProgress = aProgress;

    gl_Position = uProjection * uView * uModel * vec4(aPos, 1.0);
}