Shader "Custom/InstancedSmokeShader"
{
    Properties
    {
        _Color ("Base Color", Color) = (0,0,0,1) // 默认黑色
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
        // 开启透明混合模式
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 200

        CGPROGRAM
        // Surface Shader 设置：也就是告诉Unity我们要用透明度(alpha)
        #pragma surface surf Standard fullforwardshadows alpha:fade
        #pragma target 3.0

        // 关键指令：开启 GPU Instancing 支持
        #pragma multi_compile_instancing

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;

        // 定义 Instancing 缓冲区
        UNITY_INSTANCING_BUFFER_START(Props)
            // 这里定义我们需要从 C# 接收的数组属性 "_Density"
            UNITY_DEFINE_INSTANCED_PROP(float, _Density)
        UNITY_INSTANCING_BUFFER_END(Props)

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;

            // 核心逻辑：读取当前实例的 _Density 值
            float density = UNITY_ACCESS_INSTANCED_PROP(Props, _Density);

            // 将浓度映射到透明度 (你可以调整这个公式)
            // 比如：浓度 * 0.8，保证最浓的时候也不会完全不透明，看起来像烟
            o.Alpha = clamp(density * 0.6, 0, 1); 
        }
        ENDCG
    }
    FallBack "Diffuse"
}