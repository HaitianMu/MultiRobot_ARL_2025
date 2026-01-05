Shader "Custom/InstancedFire_Debug"
{
    Properties
    {
        _MainTex ("Noise Texture (R)", 2D) = "white" {}
        [HDR] _CoreColor ("Core Color", Color) = (4, 2, 0.5, 1)
        [HDR] _EdgeColor ("Edge Color", Color) = (1, 0.2, 0, 1)
        _ScrollSpeed ("Scroll Speed", Float) = 1.0
        
        // 1. 新增：暴露基础透明度参数，默认给个极小值
        _BaseAlpha ("Base Alpha", Range(0, 1)) = 0.05 
    }

    SubShader
    {
        // 2. 修改：把队列稍微往后放一点，或者保持 Transparent
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        // 3. 调试建议：如果想看清内部，可以暂时改用标准混合，而不是叠加变亮
        // Blend SrcAlpha OneMinusSrcAlpha // 标准透明混合（不会越叠越亮）
        Blend SrcAlpha One // 保持你原来的叠加模式

        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _CoreColor;
            float4 _EdgeColor;
            float _ScrollSpeed;
            float _BaseAlpha; // 声明变量

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float, _Density)
                UNITY_DEFINE_INSTANCED_PROP(float, _Seed)
            UNITY_INSTANCING_BUFFER_END(Props)

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                float seed = UNITY_ACCESS_INSTANCED_PROP(Props, _Seed);
                float2 scroll = float2(0, _Time.y * _ScrollSpeed + seed);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex) + scroll;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                float density = UNITY_ACCESS_INSTANCED_PROP(Props, _Density);
                float noise = tex2D(_MainTex, i.uv).r;
                float finalIntensity = noise * density;

                fixed4 col = lerp(_EdgeColor, _CoreColor, clamp(finalIntensity * 1.5, 0, 1));
                
                // 4. 使用参数控制透明度，并乘以 density 动态调整
                col.a = _BaseAlpha * clamp(density, 0, 1); 

                return col;
            }
            ENDCG
        }
    }
}