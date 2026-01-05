using UnityEngine;
using System.Collections.Generic;

public class SmokeRenderer : MonoBehaviour
{
    public EnvControl envControl;

    [Header("设置")]
    public Mesh mesh;           // 建议拖入 Unity 自带的 Sphere (球体)
    public Material material;   // 拖入使用下方 Shader 制作的材质

    private MaterialPropertyBlock block;

    // 缓存数组：专门用于批量传输数据，避免每帧 new 数组
    private Matrix4x4[] batchMatrices = new Matrix4x4[1023];
    private float[] batchDensities = new float[1023];
    // 新增：随机种子数组，让每个火球闪烁不一样
    private float[] batchSeeds = new float[1023];

    void Start()
    {
        block = new MaterialPropertyBlock();

        // 预先生成随机种子
        for (int i = 0; i < 1023; i++)
        {
            batchSeeds[i] = Random.Range(0f, 100f);
        }
    }

    void Update()
    {
        // 1. 性能检查
        if (envControl.CurrentFrameData == null) return;

        var frame = envControl.CurrentFrameData;
        var sourceMatrices = frame.RenderMatrices;
        var sourceDensities = frame.RenderDensities;
        int totalCount = sourceMatrices.Count;

        if (totalCount == 0) return;

        // 2. 分批渲染 (每批最多1023个)
        for (int i = 0; i < totalCount; i += 1023)
        {
            // 计算当前这批的数量
            int count = Mathf.Min(1023, totalCount - i);

            // 3. 数据拷贝
            sourceMatrices.CopyTo(i, batchMatrices, 0, count);
            sourceDensities.CopyTo(i, batchDensities, 0, count);
            // batchSeeds 不需要拷贝，直接复用 Start 里生成的即可

            // 4. 将数组传给 Shader
            // 注意：字符串必须和 Shader 里的属性名完全一致
            block.SetFloatArray("_Density", batchDensities);
            block.SetFloatArray("_Seed", batchSeeds);

            // 5. 绘制
            Graphics.DrawMeshInstanced(mesh, 0, material, batchMatrices, count, block);
        }
    }
}