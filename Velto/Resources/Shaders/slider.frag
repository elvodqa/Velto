#version 330 core

in vec2 vUV;
in float vProgress;

uniform vec4 uColor;
uniform float uTime;
uniform float uFadeIn;
uniform float uFadeOut;

// End-cap size in UV.y units (roughly: circleRadius / sliderLength).
uniform float uCap;

out vec4 FragColor;

void main()
{
    vec3 base = uColor.rgb;
    float gradient = mix(0.9, 1.1, vProgress);

    float alpha = uColor.a;

    // True rounded end-caps (capsule) in "radius units".
    // UV.x spans the slider width (0..1), UV.y spans length (0..1).
    // uCap ~= radius / length, so length in radius-units is Lr = 1/uCap.
    float cap = max(uCap, 1e-5);
    float Lr = 1.0 / cap;

    float xN = (vUV.x - 0.5) / 0.5; // [-1..1]
    float yN = vUV.y * Lr;          // [0..Lr]

    float y0;
    float y1;
    if (Lr <= 2.0)
    {
        // Very short sliders: treat as a circle.
        y0 = Lr * 0.5;
        y1 = y0;
    }
    else
    {
        // Capsule centers are 1 radius away from the ends.
        y0 = 1.0;
        y1 = Lr - 1.0;
    }

    float yClosest = clamp(yN, y0, y1);
    float d = length(vec2(xN, yN - yClosest)) - 1.0; // signed distance; inside < 0

    float feather = 0.06;
    float inside = smoothstep(feather, -feather, d);

    // Border thickness in radius units.
    float borderThickness = 0.20;
    float inner = smoothstep(feather, -feather, d + borderThickness);

    float fade = clamp(min(uFadeIn, uFadeOut), 0.0, 1.0);

    vec3 borderCol = vec3(1.0);
    vec3 fillCol = base * gradient;
    vec3 col = mix(borderCol, fillCol, inner);

    alpha *= inside * fade;

    FragColor = vec4(col, alpha);
}