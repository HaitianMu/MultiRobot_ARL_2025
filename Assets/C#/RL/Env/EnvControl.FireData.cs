using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

// 定义单帧的烟雾数据结构
public class SmokeFrame
{
    // 用于逻辑查询 (Agent用): 
    // Key = 坐标产生的Hash或者索引, Value = 浓度
    // 为了极致性能，我们用一维数组 + 坐标映射，而不是Dictionary
    public float[] DensityGrid;

    // 用于渲染 (Renderer用): 
    // 预先计算好的矩阵，直接丢给GPU
    public List<Matrix4x4> RenderMatrices = new List<Matrix4x4>();
    public List<float> RenderDensities = new List<float>();
}

public partial class EnvControl : MonoBehaviour
{
    [Header("Fire Data Settings")]
    public TextAsset fireBinaryFile; // 把 FireData1.csv 拖到这里
    public float gridStep = 0.5f; // CSV里的网格步长，你的是0.5
    public float minX = -10f; // 根据你的CSV数据调整地图边界
    public float minZ = -10f;
    public int widthX = 100;  // 网格宽
    public int lengthZ = 100; // 网格长

    // 存储所有时间步的数据：索引 = 时间 / 0.5
    private Dictionary<float, SmokeFrame> _allSmokeFrames = new Dictionary<float, SmokeFrame>();

    // 当前这一帧的数据引用
    public SmokeFrame CurrentFrameData;

    private void Awake()
    {
        LoadFireData();
    }

    public void LoadFireData()
    {
        // 假设你通过Inspector引用了 .bytes 文件
        // public TextAsset fireBinaryFile; 
        if (fireBinaryFile == null) return;

        // 使用 MemoryStream 读取二进制内容，速度极快
        using (MemoryStream ms = new MemoryStream(fireBinaryFile.bytes))
        using (BinaryReader reader = new BinaryReader(ms))
        {
            // 循环直到读取完所有流
            while (ms.Position < ms.Length)
            {
                // 必须按写入的顺序读取！
                float time = reader.ReadSingle();
                float x = reader.ReadSingle();
                float y_csv = reader.ReadSingle();
                float z_height = reader.ReadSingle();
                float density = reader.ReadSingle();

                // 下面是原本的逻辑，直接复制过来即可
                if (!_allSmokeFrames.ContainsKey(time))
                {
                    _allSmokeFrames[time] = new SmokeFrame();
                    _allSmokeFrames[time].DensityGrid = new float[widthX * lengthZ];
                }
                SmokeFrame frame = _allSmokeFrames[time];

                // 存入渲染数据
                Vector3 pos = new Vector3(x, z_height, y_csv);
                // 注意：Matrix4x4.TRS 计算量很大，如果有几十万个点，这里依然会卡
                // 如果可能，建议只存pos和density，在渲染时再构建矩阵
                frame.RenderMatrices.Add(Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one * gridStep));
                frame.RenderDensities.Add(density);

                // 存入逻辑数据
                int gridX = Mathf.RoundToInt((x - minX) / gridStep);
                int gridZ = Mathf.RoundToInt((y_csv - minZ) / gridStep);

                if (gridX >= 0 && gridX < widthX && gridZ >= 0 && gridZ < lengthZ)
                {
                    int index = gridZ * widthX + gridX;
                    if (frame.DensityGrid[index] < density)
                    {
                        frame.DensityGrid[index] = density;
                    }
                }
            }
        }
        Debug.Log("二进制火灾数据加载完成！");
    }

    // 在 FixedUpdate 更新当前帧指针
    private void UpdateSmokeFrame()
    {
        // 找到最近的时间点 (0.5的倍数)
        float snapTime = Mathf.Round(runtime /3f) / 2f;

        if (_allSmokeFrames.ContainsKey(snapTime))
        {
            CurrentFrameData = _allSmokeFrames[snapTime];
            print("当前火焰数据为"+CurrentFrameData.ToString());
        }
    }
}