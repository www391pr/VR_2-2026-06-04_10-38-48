// ============================================================================
// ParticleInstanced.shader — GPU Instanced particle rendering
// Swinging Paint Bucket Simulation
// ============================================================================
// Renders up to 1M particles using GPU instancing.
// Each particle is drawn as a billboard quad with per-instance color and size.
// ============================================================================

Shader "SwingingPaintBucket/ParticleInstanced"
{
    Properties
    {
        _MainTex ("Particle Texture", 2D) = "white" {}
        _BaseSize ("Base Particle Size", Float) = 0.005
        _AlphaMultiplier ("Alpha Multiplier", Float) = 1.0
    }
    
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 4.5
            
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            sampler2D _MainTex;
            float _BaseSize;
            float _AlphaMultiplier;
            
            // ── Instance Data Buffers ────────────────────────────────────
            #if defined(SHADER_API_D3D11) || defined(SHADER_API_GLCORE) || defined(SHADER_API_VULKAN)
            StructuredBuffer<float3> _ParticlePositions;
            StructuredBuffer<float4> _ParticleColors;
            StructuredBuffer<float> _ParticleRadii;
            StructuredBuffer<int> _ParticleStates;
            #endif
            
            v2f vert(appdata v, uint instanceID : SV_InstanceID)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                
                #if defined(SHADER_API_D3D11) || defined(SHADER_API_GLCORE) || defined(SHADER_API_VULKAN)
                
                // Skip dead particles
                int state = _ParticleStates[instanceID];
                if (state == 0) // Dead
                {
                    o.pos = float4(0, 0, 0, 0); // Degenerate — won't render
                    o.uv = float2(0, 0);
                    o.color = float4(0, 0, 0, 0);
                    return o;
                }
                
                float3 worldPos = _ParticlePositions[instanceID];
                float4 color = _ParticleColors[instanceID];
                float radius = _ParticleRadii[instanceID];
                
                // Billboard: orient quad toward camera
                float3 camRight = UNITY_MATRIX_V[0].xyz;
                float3 camUp = UNITY_MATRIX_V[1].xyz;
                
                float size = max(radius * 2.0, _BaseSize);
                
                // Offset vertex to create billboard quad
                float3 vertexPos = worldPos 
                    + camRight * v.vertex.x * size 
                    + camUp * v.vertex.y * size;
                
                o.pos = UnityWorldToClipPos(float4(vertexPos, 1.0));
                o.uv = v.uv;
                o.color = color;
                o.color.a *= _AlphaMultiplier;
                
                // Different alpha for different states
                if (state == 1) // InBucket — force full opacity so it's clearly visible
                    o.color.a = 1.0;
                else if (state == 2) // FreeFall — full opacity
                    o.color.a = color.a;
                else if (state == 3) // OnCanvas — slightly transparent for layering
                    o.color.a = color.a * 0.8;
                
                #else
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = float4(1, 0, 0, 1);
                #endif
                
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 texColor = tex2D(_MainTex, i.uv);
                
                // Circular soft particle (if no texture)
                float2 centered = i.uv - 0.5;
                float dist = length(centered) * 2.0;
                float alpha = saturate(1.0 - dist);
                alpha = alpha * alpha; // Soft edge
                
                fixed4 finalColor = i.color * texColor;
                finalColor.a *= alpha;
                
                return finalColor;
            }
            
            ENDCG
        }
    }
    
    FallBack "Diffuse"
}
