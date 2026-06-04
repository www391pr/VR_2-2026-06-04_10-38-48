// ============================================================================
// PaintSurface.shader — Canvas paint rendering shader
// Swinging Paint Bucket Simulation
// ============================================================================
// Renders the canvas surface with paint texture overlay.
// Shows paint thickness variation through normal mapping.
// ============================================================================

Shader "SwingingPaintBucket/PaintSurface"
{
    Properties
    {
        _CanvasTex ("Canvas Base Texture", 2D) = "white" {}
        _PaintTex ("Paint Texture (Runtime)", 2D) = "black" {}
        _HeightTex ("Paint Height Map (Runtime)", 2D) = "black" {}
        _CanvasColor ("Canvas Base Color", Color) = (0.95, 0.93, 0.88, 1)
        _PaintGloss ("Paint Glossiness", Range(0, 1)) = 0.3
        _HeightScale ("Height Scale (for normals)", Range(0, 0.1)) = 0.01
        _CanvasRoughness ("Canvas Roughness", Range(0, 1)) = 0.8
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200
        
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma target 3.0
            
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                float3 worldTangent : TEXCOORD3;
                float3 worldBitangent : TEXCOORD4;
            };
            
            sampler2D _CanvasTex;
            sampler2D _PaintTex;
            sampler2D _HeightTex;
            float4 _CanvasColor;
            float _PaintGloss;
            float _HeightScale;
            float _CanvasRoughness;
            
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldTangent = UnityObjectToWorldDir(v.tangent.xyz);
                o.worldBitangent = cross(o.worldNormal, o.worldTangent) * v.tangent.w;
                return o;
            }
            
            // Compute normal from height map (finite differences)
            float3 ComputePaintNormal(float2 uv, float texelSize)
            {
                float h = tex2D(_HeightTex, uv).r;
                float hR = tex2D(_HeightTex, uv + float2(texelSize, 0)).r;
                float hU = tex2D(_HeightTex, uv + float2(0, texelSize)).r;
                
                float3 normal;
                normal.x = (h - hR) * _HeightScale;
                normal.y = 1.0;
                normal.z = (h - hU) * _HeightScale;
                
                return normalize(normal);
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                // Sample textures
                float4 canvasBase = tex2D(_CanvasTex, i.uv) * _CanvasColor;
                float4 paintColor = tex2D(_PaintTex, i.uv);
                float paintHeight = tex2D(_HeightTex, i.uv).r;
                
                // Paint alpha based on height (thickness)
                float paintAlpha = saturate(paintHeight * 100.0); // Scale up small heights
                
                // Blend canvas and paint
                float4 surfaceColor = lerp(canvasBase, paintColor, paintAlpha);
                
                // Lighting
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                
                // Compute perturbed normal from paint height
                float texelSize = 1.0 / 2048.0; // Assume 2048 texture
                float3 paintNormal = ComputePaintNormal(i.uv, texelSize);
                
                // Transform to world space
                float3x3 TBN = float3x3(
                    normalize(i.worldTangent),
                    normalize(i.worldBitangent),
                    normalize(i.worldNormal)
                );
                float3 worldNormal = mul(paintNormal, TBN);
                worldNormal = normalize(worldNormal);
                
                // Diffuse
                float NdotL = max(dot(worldNormal, lightDir), 0.0);
                float3 diffuse = surfaceColor.rgb * NdotL;
                
                // Specular (Blinn-Phong) — stronger where paint is present
                float3 halfDir = normalize(lightDir + viewDir);
                float NdotH = max(dot(worldNormal, halfDir), 0.0);
                float glossiness = lerp(_CanvasRoughness, _PaintGloss, paintAlpha);
                float specPower = lerp(8.0, 64.0, glossiness);
                float spec = pow(NdotH, specPower) * glossiness;
                float3 specular = _LightColor0.rgb * spec;
                
                // Ambient
                float3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb * surfaceColor.rgb;
                
                float3 finalColor = ambient + diffuse + specular;
                
                return fixed4(finalColor, 1.0);
            }
            
            ENDCG
        }
    }
    
    FallBack "Diffuse"
}
