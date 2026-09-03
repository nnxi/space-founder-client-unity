Shader "Custom/ProceduralPlanet"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _Seed ("Seed", Float) = 0.0
        _PlanetType ("Planet Type (0:Rocky, 1:Gas, 2:Ice, 3:Lava, 4:Star)", Int) = 0
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
                float3 worldPos : TEXCOORD2;
            };

            float4 _BaseColor;
            float _Seed;
            int _PlanetType;
            
            uniform float4 _LightDirs[3]; 
            uniform float4 _LightColors[3];

            float hash(float3 p) 
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

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

            float fbm(float3 p, int planetType) 
            {
                float v = 0.0;
                float a = 0.5;
                float3 shift = float3(100.0, 100.0, 100.0);
                
                if (planetType != 1) 
                {
                    for (int i = 0; i < 6; i++) 
                    {
                        v += a * noise(p);
                        p = p * 2.0 + shift;
                        a *= 0.5;
                    }
                } 
                else 
                {
                    float3 gasP = float3(p.x * 0.5, p.y * 6.0, p.z * 0.5);
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
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float safeSeed = fmod(abs(_Seed), 1000.0);
                float rand1 = frac(safeSeed * 0.123);
                float rand2 = frac(safeSeed * 0.456);
                float rand3 = frac(safeSeed * 0.789);

                float3 p = normalize(i.localPos) * (2.0 + rand1) + float3(safeSeed, safeSeed, safeSeed);
                
                float3 warp1 = float3(
                    fbm(p + float3(1.0, 2.0, 3.0), _PlanetType), 
                    fbm(p + float3(4.0, 5.0, 6.0), _PlanetType), 
                    fbm(p + float3(7.0, 8.0, 9.0), _PlanetType)
                );
                
                float n = fbm(p + warp1 * (1.0 + rand2), _PlanetType);
                float3 myBaseColor = saturate(_BaseColor.rgb + (rand3 * 0.2 - 0.1));
                
                float3 finalColor = float3(0,0,0);
                float3 atmosphereColor = float3(0,0,0);
                float3 emissionColor = float3(0,0,0);

                if (_PlanetType == 0) 
                {
                    float waterLevel = 0.2 + (rand1 * 0.4); 
                    float snowLevel = waterLevel + 0.2 + (rand2 * 0.3);  
                    
                    float3 deepWater = saturate(float3(0.05, 0.15, 0.4) + (rand1 * 0.2));
                    float3 shallowWater = saturate(float3(0.1, 0.5, 0.7) + (rand2 * 0.2));
                    float3 sand = lerp(float3(0.8, 0.7, 0.5), myBaseColor, 0.3); 
                    float3 land = myBaseColor;
                    float3 snow = float3(0.95, 0.95, 1.0);

                    float3 waterColor = lerp(deepWater, shallowWater, smoothstep(0.0, waterLevel, n));
                    float3 landColor = lerp(sand, land, smoothstep(waterLevel, waterLevel + 0.1, n));
                    landColor = lerp(landColor, snow, smoothstep(snowLevel - 0.1, snowLevel + 0.1, n));

                    finalColor = lerp(waterColor, landColor, smoothstep(waterLevel - 0.02, waterLevel + 0.02, n));
                    atmosphereColor = lerp(float3(0.4, 0.7, 1.0), myBaseColor, 0.3);
                } 
                else if (_PlanetType == 1) 
                {
                    float band = sin(p.y * (10.0 + rand1 * 15.0) + n * 5.0);
                    float3 color1 = myBaseColor * 0.3;
                    float3 color2 = myBaseColor * 1.5;
                    float3 color3 = lerp(color1, float3(1,1,1), rand2 * 0.5);
                    
                    finalColor = lerp(color1, color2, smoothstep(0.2, 0.8, n));
                    finalColor = lerp(finalColor, color3, smoothstep(0.5, 1.0, band));
                    atmosphereColor = myBaseColor;
                } 
                else if (_PlanetType == 2) 
                {
                    float3 deepIce = myBaseColor * 0.3;
                    float3 snowSurface = float3(0.9, 0.95, 1.0);
                    float crack = smoothstep(0.45, 0.5, n) - smoothstep(0.5, 0.55, n);
                    
                    finalColor = lerp(deepIce, snowSurface, smoothstep(0.2, 0.8, n));
                    finalColor = lerp(finalColor, deepIce * 0.5, crack * rand1);
                    atmosphereColor = float3(0.7, 0.9, 1.0);
                }
                else if (_PlanetType == 3)
                {
                    float3 crust = myBaseColor * 0.15;
                    float3 magma = float3(1.0, 0.3, 0.0) * 1.5;
                    float heat = smoothstep(0.3, 0.6, n);
                    float cracks = smoothstep(0.4, 0.5, n) - smoothstep(0.5, 0.6, n);
                    
                    finalColor = lerp(crust, magma, saturate(cracks * 2.0 + (heat * rand1)));
                    
                    emissionColor = magma * saturate(cracks * 2.5);
                    atmosphereColor = float3(1.0, 0.2, 0.0);
                }
                else if (_PlanetType == 4)
                {
                    float plasma = noise(p * 1.5 + float3(rand1, rand2, rand3)); 
                    float microDetail = noise(p * 15.0) * 0.15; 
                    float surfacePattern = smoothstep(0.2, 0.8, plasma + microDetail);
                    
                    float3 starBase = lerp(myBaseColor, float3(1.0, 1.0, 1.0), 0.4) * 1.5;
                    float3 superHotSpot = lerp(myBaseColor, float3(1.0, 1.0, 1.0), 0.9) * 2.5; 
                    
                    finalColor = lerp(starBase, superHotSpot, surfacePattern);
                    
                    emissionColor = finalColor * 1.5; 
                    atmosphereColor = lerp(myBaseColor, float3(1.0, 1.0, 1.0), 0.7);
                }

                // --- [다중 광원(최대 3개) 누적 연산부] ---
                float3 N = normalize(i.normal);
                float3 diffuseAccumulation = float3(0,0,0);
                
                float totalIllumination = 0.0; 

                if (_PlanetType == 4) 
                {
                    diffuseAccumulation = finalColor;
                    totalIllumination = 1.0;
                }
                else
                {
                    for (int j = 0; j < 3; j++)
                    {
                        float intensity = _LightDirs[j].w;
                        if (intensity > 0.0)
                        {
                            float3 lDir = normalize(_LightDirs[j].xyz);
                            float NdotL = max(0.0, dot(N, lDir));
                            
                            diffuseAccumulation += finalColor * _LightColors[j].rgb * NdotL * intensity;
                            totalIllumination += NdotL * intensity;
                        }
                    }
                    
                    diffuseAccumulation = max(diffuseAccumulation, finalColor * 0.02);
                }

                finalColor = diffuseAccumulation + emissionColor;

                // 림 라이트 처리
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float rim = 1.0 - max(0.0, dot(N, viewDir));
                rim = smoothstep(0.5, 1.0, rim);
                
                if (_PlanetType == 4) 
                {
                    // 항성의 코로나(빛 번짐) 연출을 살짝 더 강하게 조정
                    finalColor += atmosphereColor * rim * 2.0;
                }
                else 
                {
                    finalColor += atmosphereColor * rim * 0.6 * saturate(totalIllumination);
                }

                return fixed4(finalColor, 1.0);
            }
            ENDCG
        }
    }
}