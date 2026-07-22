Shader "Custom/ProceduralPlanet"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _Seed ("Seed", Float) = 0.0
        _PlanetType ("Planet Type (0:Rocky, 1:Gas, 2:Ice)", Int) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 localPos : TEXCOORD0;
                float3 normal : TEXCOORD1;
            };

            float4 _BaseColor;
            float _Seed;
            int _PlanetType;

            // 난수 생성
            float hash(float3 p) 
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            // 3D 노이즈
            float noise(float3 x) 
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);

                return lerp(
                    lerp(lerp(hash(i + float3(0,0,0)), hash(i + float3(1,0,0)), f.x),
                         lerp(hash(i + float3(0,1,0)), hash(i + float3(1,1,0)), f.x), f.y),
                    lerp(lerp(hash(i + float3(0,0,1)), hash(i + float3(1,0,1)), f.x),
                         lerp(hash(i + float3(0,1,1)), hash(i + float3(1,1,1)), f.x), f.y), f.z);
            }

            // FBM 연산
            float fbm(float3 p, int planetType) 
            {
                float v = 0.0;
                float a = 0.5;
                float3 shift = float3(100.0, 100.0, 100.0);
                
                if (planetType == 0 || planetType == 2) 
                {
                    for (int i = 0; i < 6; i++) 
                    {
                        v += a * noise(p);
                        p = p * 2.0 + shift;
                        a *= 0.5;
                    }
                } 
                else if (planetType == 1) 
                {
                    float3 gasP = float3(p.x * 0.1, p.y * 5.0, p.z * 0.1);
                    for (int j = 0; j < 6; j++) 
                    {
                        v += a * noise(gasP);
                        gasP = gasP * 2.0 + shift;
                        a *= 0.5;
                    }
                }
                return v;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.localPos = v.vertex.xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float safeSeed = fmod(_Seed, 1000.0);
                float3 p = normalize(i.localPos) * 2.5 + float3(safeSeed, safeSeed, safeSeed);
                
                float3 warp1 = float3(
                    fbm(p + float3(1.0, 2.0, 3.0), _PlanetType), 
                    fbm(p + float3(4.0, 5.0, 6.0), _PlanetType), 
                    fbm(p + float3(7.0, 8.0, 9.0), _PlanetType)
                );
                
                float n = fbm(p + warp1 * 1.5, _PlanetType);
                float tintValue = frac(_Seed * 0.512) * 0.2 - 0.1; 
                float3 myBaseColor = saturate(_BaseColor.rgb + float3(tintValue, tintValue, tintValue));
                float3 finalColor = float3(0,0,0);

                if (_PlanetType == 0) 
                {
                    float waterLevel = 0.45; 
                    float snowLevel = 0.75;  
                    
                    float3 deepWater = saturate(float3(0.05, 0.15, 0.4) + tintValue * 0.5);
                    float3 shallowWater = saturate(float3(0.1, 0.5, 0.7) + tintValue * 0.5);
                    float3 sand = float3(0.8, 0.7, 0.5); 
                    float3 land = myBaseColor;
                    float3 snow = float3(0.95, 0.95, 1.0);

                    float3 waterColor = lerp(deepWater, shallowWater, smoothstep(0.0, waterLevel, n));
                    float3 landColor = lerp(sand, land, smoothstep(waterLevel, waterLevel + 0.1, n));
                    landColor = lerp(landColor, snow, smoothstep(snowLevel - 0.1, snowLevel + 0.1, n));

                    finalColor = lerp(waterColor, landColor, smoothstep(waterLevel - 0.02, waterLevel + 0.02, n));
                } 
                else if (_PlanetType == 1) 
                {
                    finalColor = lerp(myBaseColor * 0.2, myBaseColor * 1.5, smoothstep(0.2, 0.8, n));
                } 
                else 
                {
                    float3 deepIce = myBaseColor * 0.4;
                    float3 snowSurface = float3(0.85, 0.95, 1.0);
                    finalColor = lerp(deepIce, snowSurface, smoothstep(0.3, 0.7, n));
                }

                // 림 라이트 처리
                float3 viewDir = normalize(_WorldSpaceCameraPos - mul(unity_ObjectToWorld, float4(i.localPos, 1.0)).xyz);
                float rim = 1.0 - max(0.0, dot(normalize(i.normal), viewDir));
                rim = smoothstep(0.5, 1.0, rim);
                
                float3 atmosphereColor = (_PlanetType == 0) ? float3(0.4, 0.7, 1.0) : (_PlanetType == 1 ? myBaseColor : float3(0.8, 0.9, 1.0));
                finalColor += atmosphereColor * rim * 0.6;

                return fixed4(finalColor, 1.0);
            }
            ENDCG
        }
    }
}