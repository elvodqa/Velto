#include <metal_stdlib>
#include <simd/simd.h>

using namespace metal;

struct RendererData {
    float4 position [[position]];
    float2 textureCoordinate;
    uint instance_id;
};

vertex RendererData vertex_main0(uint vid [[vertex_id]], 
                                uint iid [[instance_id]], 
                                constant float3* vertices [[buffer(0)]],
                                constant float2* uvs [[buffer(1)]],
                                constant float4x4& projection [[buffer(2)]],
                                constant float4x4& model [[buffer(3)]]
                                ) {
    RendererData out;
    
    float4 pos = float4(vertices[vid], 1.0);
    out.position = projection * model * pos;
    out.textureCoordinate = uvs[vid];
    
    return out;
}



fragment float4 fragment_main0(RendererData in [[stage_in]], texture2d<float> tex [[texture(0)]]) {
    constexpr sampler textureSampler(mag_filter::linear, min_filter::linear, mip_filter::none);
   
    float4 finalColor;

    finalColor = tex.sample(textureSampler, in.textureCoordinate);

    return finalColor;
}