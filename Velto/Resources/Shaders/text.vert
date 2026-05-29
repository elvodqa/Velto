#version 410 core

layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec2 aUV;

/* INSTANCE DATA */

layout(location = 2) in mat4 iModel;

layout(location = 6) in vec4 iColor;

layout(location = 7) in vec4 iUV;

layout(location = 8) in vec2 iGlyphSize;

layout(location = 9) in float iDistanceRange;

uniform mat4 uProjection;

out vec2 vUV;
out vec4 vColor;
out vec4 vGlyphUV;
out float vDistanceRange;

void main()
{
    gl_Position =
    uProjection *
    iModel *
    vec4(aPosition, 1.0);

    vUV = aUV;

    vColor = iColor;

    vGlyphUV = iUV;
    
    vDistanceRange = iDistanceRange;

    vec3 scaleX = iModel[0].xyz;
    vec3 scaleY = iModel[1].xyz;
}