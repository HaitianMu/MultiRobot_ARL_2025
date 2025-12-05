using UnityEngine;
using System.Collections.Generic;

public class SmokeRenderer : MonoBehaviour
{
    public EnvControl envControl;
    public Mesh mesh;       // 拖入 Cube
    public Material material; // 拖入下面创建的 SmokeMaterial

    private MaterialPropertyBlock block;

    // 缓存数组：专门用于批量传输数据，避免每帧 new 数组
    private Matrix4x4[] batchMatrices = new Matrix4x4[1023];
    private float[] batchDensities = new float[1023];

    void Start()
    {
        block = new MaterialPropertyBlock();
    }

    void Update()
    {
        // 1. 性能检查
        if (envControl.CurrentFrameData == null)
        {
            print("火焰数据为空");
            return;
        }

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

            // 3. 数据拷贝 (核心优化：将数据从 List 拷入缓存数组，不产生 GC)
            sourceMatrices.CopyTo(i, batchMatrices, 0, count);
            sourceDensities.CopyTo(i, batchDensities, 0, count);

            // 4. 将浓度数组传给 Shader
            // 注意：Shader 里必须有 "_Density" 这个属性
            block.SetFloatArray("_Density", batchDensities);

            // 5. 绘制
            // 使用缓存的 batchMatrices 进行绘制
            Graphics.DrawMeshInstanced(mesh, 0, material, batchMatrices, count, block);
        }
    }
}