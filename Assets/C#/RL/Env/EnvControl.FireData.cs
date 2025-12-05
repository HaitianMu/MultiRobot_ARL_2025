using System.Collections.Generic;
using System.Globalization;
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
    public TextAsset fireCsvFile; // 把 FireData1.csv 拖到这里
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

    private void LoadFireData()
    {
        if (fireCsvFile == null) return;

        string[] lines = fireCsvFile.text.Split('\n');
        // 跳过表头，从第1行开始
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            string[] cols = lines[i].Split(',');
            // CSV格式: X(0), Y(1), Z(2), mol(3), C(4), m(5), time(6)

            float x = float.Parse(cols[0], CultureInfo.InvariantCulture);
            float y_csv = float.Parse(cols[1], CultureInfo.InvariantCulture); // CSV的Y通常是Unity的Z
            float z_height = float.Parse(cols[2], CultureInfo.InvariantCulture);
            float density = float.Parse(cols[3], CultureInfo.InvariantCulture) * 1000000;//将mol/mol换位ppm
            float time = float.Parse(cols[6], CultureInfo.InvariantCulture);

            // 过滤掉浓度太低的（省内存，省渲染）
            if (density < 0.01f) continue;

            // 1. 获取或创建这一帧的数据容器
            if (!_allSmokeFrames.ContainsKey(time))
            {
                _allSmokeFrames[time] = new SmokeFrame();
                // 初始化Grid (假设地图最大 100x100)
                _allSmokeFrames[time].DensityGrid = new float[widthX * lengthZ];
            }
            SmokeFrame frame = _allSmokeFrames[time];

            print(frame.ToString());

            // 2. 存入渲染数据 (坐标转换: CSV Y -> Unity Z, CSV Z -> Unity Y)
            Vector3 pos = new Vector3(x, z_height, y_csv);
            frame.RenderMatrices.Add(Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one * gridStep));
            frame.RenderDensities.Add(density);

            // 3. 存入逻辑数据 (网格化)
            // 计算数组索引
            int gridX = Mathf.RoundToInt((x - minX) / gridStep);
            int gridZ = Mathf.RoundToInt((y_csv - minZ) / gridStep);

            if (gridX >= 0 && gridX < widthX && gridZ >= 0 && gridZ < lengthZ)
            {
                int index = gridZ * widthX + gridX;
                // 如果同一个格子有多个高度的数据，取最大的那个作为伤害参考
                if (frame.DensityGrid[index] < density)
                {
                    frame.DensityGrid[index] = density;
                }
            }
        }
        Debug.Log($"火灾数据加载完成！共加载 {lines.Length} 行数据。");
       
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