using UnityEngine;
using UnityEditor;
using System.IO;

public class SmokeTextureGen : EditorWindow
{
    [MenuItem("Tools/Generate Smoke Texture")]
    public static void GenerateTexture()
    {
        int size = 512; // 纹理大小
        Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);

        // 随机种子，让每次生成的烟雾都不一样
        float seed = Random.Range(0f, 100f);
        float noiseScale = 5.0f; // 噪声的缩放，越大烟雾越细碎

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 1. 归一化坐标 (0 到 1)
                float u = x / (float)size;
                float v = y / (float)size;

                // 2. 计算径向渐变 (让中心最亮，边缘透明，形成圆形粒子)
                float dx = u - 0.5f;
                float dy = v - 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                // 边缘羽化：距离中心超过 0.4 开始急剧变淡
                float radialAlpha = Mathf.Clamp01(1.0f - (dist * 2.0f));
                radialAlpha = Mathf.Pow(radialAlpha, 2.0f); // 让衰减更平滑

                // 3. 计算柏林噪声 (Perlin Noise) 提供烟雾的云雾感
                float noise = Mathf.PerlinNoise(seed + u * noiseScale, seed + v * noiseScale);

                // 叠加第二层噪声让细节更丰富 (FBM 简化版)
                float noise2 = Mathf.PerlinNoise(seed + u * noiseScale * 2 + 10, seed + v * noiseScale * 2 + 10);
                noise = (noise + noise2 * 0.5f) / 1.5f;

                // 4. 混合逻辑：基础白色 * (径向遮罩 * 噪声)
                // 核心：Alpha 通道决定了形状
                float finalAlpha = radialAlpha * noise;

                // 稍微增强一点中心亮度
                finalAlpha = Mathf.Clamp01(finalAlpha * 1.5f);

                texture.SetPixel(x, y, new Color(1f, 1f, 1f, finalAlpha));
            }
        }

        texture.Apply();

        // 5. 保存为 PNG
        byte[] bytes = texture.EncodeToPNG();
        string path = Application.dataPath + "/GeneratedSmoke.png";
        File.WriteAllBytes(path, bytes);

        // 刷新资源管理器
        AssetDatabase.Refresh();

        // 选中生成的图片
        Object obj = AssetDatabase.LoadAssetAtPath<Object>("Assets/GeneratedSmoke.png");
        Selection.activeObject = obj;
        EditorGUIUtility.PingObject(obj);

        Debug.Log("烟雾纹理已生成: " + path);
    }
}