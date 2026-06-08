#include <metal_stdlib>
#include <simd/simd.h>

using namespace metal;

enum class DrawType {
    Primitive = 1,
    Texture = 2,
    MSDF = 3,
};

struct DrawCall {
    DrawType type;
    simd::float4x4 model;
    simd::float4 color;
    texture2d<float> texture;
    float cornerRadius;
    float distanceRange;
    float smoothingScale;
    struct {             // For text
        float u0, v0, u1, v1;
    } uv;
    simd::float2 glyphSize;
} __attribute__((packed));
static_assert(sizeof(DrawCall) % 16 == 0);

struct RendererData {
    float4 position [[position]];
    float2 textureCoordinate;
    float2 rectSize;      // Add rect size for corner radius calculation
    uint instance_id;
};

vertex RendererData vertex_main0(uint vid [[vertex_id]], 
                                uint iid [[instance_id]], 
                                constant float3* vertices [[buffer(0)]],
                                constant float2* uvs [[buffer(1)]],
                                constant float4x4& projection [[buffer(2)]],
                                constant DrawCall* drawCalls [[buffer(3)]]
                                    ) {
    RendererData out;
    out.position = projection * drawCalls[iid].model * float4(vertices[vid], 1.0);
    out.textureCoordinate = float2(uvs[vid]);
    out.instance_id = iid;
    
    // Extract scale from model matrix for corner radius calculation
    float4x4 model = drawCalls[iid].model;
    out.rectSize = float2(length(model.columns[0].xyz), length(model.columns[1].xyz));
    
    return out;
}

// Signed distance function for rounded rectangle
float roundedBoxSDF(float2 centerPosition, float2 size, float radius) {
    return length(max(abs(centerPosition) - size + radius, 0.0)) - radius;
}

float median(float r, float g, float b) {
    return max(min(r, g), min(max(r, g), b));
}


fragment float4 fragment_main0(RendererData in [[stage_in]],
                              constant DrawCall* drawCalls [[buffer(0)]]
) {
    constexpr sampler textureSampler(mag_filter::linear, min_filter::linear, mip_filter::none);
    
    DrawCall call = drawCalls[in.instance_id];
    float4 finalColor;

    if (call.type == DrawType::Texture) {
        finalColor = call.texture.sample(textureSampler, in.textureCoordinate) * call.color;
    } 
    else if (call.type == DrawType::Primitive) {
        finalColor = call.color;
    } 
    else if (call.type == DrawType::MSDF) {
        float2 customUV = float2(
            mix(call.uv.u0, call.uv.u1, in.textureCoordinate.x),
            mix(call.uv.v0, call.uv.v1, in.textureCoordinate.y)
        );
        
        // Sample MSDF texture
        constexpr sampler s(address::clamp_to_edge, filter::linear);
        float3 msdfSample = call.texture.sample(s, customUV).rgb;
        
        // Signed distance in texture space (centered at 0)
        float sd = median(msdfSample.r, msdfSample.g, msdfSample.b) - 0.5;
        
        // ---- screenPxRange() equivalent ----
        // distanceRange in "pxRange" terms is provided per DrawCall
        float2 texSize = float2(call.texture.get_width(), call.texture.get_height());
        float2 unitRange = float2(call.distanceRange) / texSize;
        float2 screenTexSize = float2(1.0) / fwidth(in.textureCoordinate);
        float screenPxRange = max(0.5 * dot(unitRange, screenTexSize), 1.0);
        // ------------------------------------
        
        // Convert to screen-space signed distance
        float screenPxDistance = screenPxRange * sd;
        
        // Opacity computation like in GLSL
        float opacity = clamp(screenPxDistance + 0.5, 0.0, 1.0);
        
        // Mix background and foreground colors based on opacity
        float4 bg = float4(0.0, 0.0, 0.0, 0.0); // transparent background
        float4 fg = call.color;                 // glyph color
        finalColor = mix(bg, fg, opacity);
        
        if (finalColor.a <= 0.5) {
            discard_fragment();
        }
    }

    float alpha = 1.0;

    // corner smoothing (except for MSDF)
    if (call.type != DrawType::MSDF) {
        if (call.cornerRadius > 0.0) {

            float2 centerPos = (in.textureCoordinate - 0.5) * in.rectSize;
            float2 halfSize = in.rectSize * 0.5;
            float distance = roundedBoxSDF(centerPos, halfSize, call.cornerRadius);
            float edge = 1.0;  // 1 pixel edge width
            alpha = 1.0 - smoothstep(-edge, 0.0, distance);
        }
    }
    
    finalColor.a *= alpha;
    if (finalColor.a <= 0) {
        discard_fragment();
    }

    return finalColor;
}