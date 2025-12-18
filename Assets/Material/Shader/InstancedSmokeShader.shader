Shader "Custom/SmokeInstanced"
{
    Properties
    {
        _MainTex ("Smoke Texture", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1,1,1,1)
        _Softness ("Softness", Range(0.1, 5.0)) = 1.0 // 软粒子强度
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha // 或者 One OneMinusSrcAlpha (Premultiplied)
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing // 开启 Instancing 宏
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 projPos : TEXCOORD1; // 用于软粒子计算
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Softness;
            fixed4 _BaseColor;
            
            // 声明 Instancing 属性块
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float, _Density) // 对应 C# 中的 block.SetFloatArray("_Density", ...)
            UNITY_INSTANCING_BUFFER_END(Props)

            // 核心：在顶点着色器中实现公告板 (Billboard)
            // 让 Quad 永远面朝摄像机，忽略自身的旋转
            float4 Billboard(float4 vertex)
            {
                float3 worldPos = unity_ObjectToWorld._m03_m13_m23; // 获取实例的世界坐标位置
                float3 viewPos = mul(UNITY_MATRIX_V, float4(worldPos, 1.0)).xyz; // 转到观察空间
                
                // 在观察空间直接加上顶点的偏移（这样就总是面朝摄像机了）
                // 注意：这里假设 Mesh 是 Quad，且面向 Z 轴或 Y 轴，需要根据 Quad 制作方向微调
                // 通常 Quad 默认顶点也是平铺的，直接加 xy 分量即可
                viewPos += vertex.xyz * float3(unity_ObjectToWorld._m00, unity_ObjectToWorld._m11, 1.0f); 

                return mul(UNITY_MATRIX_P, float4(viewPos, 1.0));
            }

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                // 1. 标准 MVP 计算 (如果不是 Billboard)
                // o.vertex = UnityObjectToClipPos(v.vertex);

                // 2. Billboard 计算 (替换上面的)
                // 简单的 Billboard 实现：
                // 获取物体的世界坐标原点
                float3 centerWorldPos = float3(unity_ObjectToWorld[0].w, unity_ObjectToWorld[1].w, unity_ObjectToWorld[2].w);
                // 获取摄像机位置
                float3 viewDir = normalize(_WorldSpaceCameraPos - centerWorldPos);
                float3 upDir = float3(0, 1, 0);
                float3 rightDir = normalize(cross(upDir, viewDir));
                upDir = normalize(cross(viewDir, rightDir));
                
                // 重新构建顶点位置（基于 Quad 的本地坐标展开）
                float3 localPos = v.vertex.xyz;
                // 注意：这里应用了缩放 (Matrix 的 scale)
                float scaleX = length(float3(unity_ObjectToWorld[0].x, unity_ObjectToWorld[1].x, unity_ObjectToWorld[2].x));
                
                float3 finalPos = centerWorldPos + (rightDir * localPos.x * scaleX) + (upDir * localPos.y * scaleX);
                o.vertex = mul(UNITY_MATRIX_VP, float4(finalPos, 1.0));

                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.projPos = ComputeScreenPos(o.vertex); // 计算屏幕坐标用于深度采样
                
                return o;
            }

            // 声明深度纹理 (用于软粒子)
            sampler2D _CameraDepthTexture;

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                
                // 获取 C# 传入的浓度
                float density = UNITY_ACCESS_INSTANCED_PROP(Props, _Density);

                // 1. 采样纹理
                fixed4 col = tex2D(_MainTex, i.uv) * _BaseColor;

                // 2. 应用浓度到 Alpha
                col.a *= density;

                // 3. 软粒子计算 (Soft Particles)
                // 计算当前像素的场景深度
                float sceneZ = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.projPos)));
                // 计算当前片元的深度
                float partZ = i.projPos.z;
                // 计算差值并进行淡出
                float fade = saturate((sceneZ - partZ) * _Softness);
                col.a *= fade;

                return col;
            }
            ENDCG
        }
    }
}