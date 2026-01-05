using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

// 定义单帧的烟雾数据结构
public class SmokeFrame
{
    // 逻辑查询网格 (一维数组优化性能)
    public float[] DensityGrid; // 原始烟雾
    public float[] GridC;       // 新增: C
    public float[] GridM;       // 新增: m

    // 渲染用数据 (只渲染烟雾，C和m通常不需要渲染，只需要逻辑判断)
    public List<Matrix4x4> RenderMatrices = new List<Matrix4x4>();
    public List<float> RenderDensities = new List<float>();
}
// 定义返回值结构体，方便人类一次性获取所有环境数据
public struct FireEnvData
{
    public float SmokeDensity; // 烟雾浓度
    public float ValueC;       // 第4列 C (如温度/毒性)
    public float ValueM;       // 第5列 m (如能见度/质量)
}

public partial class EnvControl : MonoBehaviour
{
    [Header("Fire Data Settings")]
    public TextAsset fireBinaryFile; // 把 FireData1.csv 拖到这里
    private Dictionary<int, SmokeFrame> _smokeFramesByIndex = new Dictionary<int, SmokeFrame>();
    public float gridStep = 0.5f; // CSV里的网格步长，你的是0.5
    public float minX = -10f; // 根据你的CSV数据调整地图边界
    public float minZ = -10f;
    public int widthX = 100;  // 网格宽
    public int lengthZ = 100; // 网格长

    // 存储所有时间步的数据：索引 = 时间 / 0.5
    private Dictionary<float, SmokeFrame> _allSmokeFrames = new Dictionary<float, SmokeFrame>();

    // 当前这一帧的数据引用
    public SmokeFrame CurrentFrameData;

   
    public void LoadFireData()
    {
        if (fireBinaryFile == null) return;

        using (MemoryStream ms = new MemoryStream(fireBinaryFile.bytes))
        using (BinaryReader reader = new BinaryReader(ms))
        {
            while (ms.Position < ms.Length)
            {
                // 1. 严格按写入顺序读取 7 个 float
                float time = reader.ReadSingle();
                float x = reader.ReadSingle();
                float unity_z = reader.ReadSingle();
                float unity_y = reader.ReadSingle();
                float density = reader.ReadSingle();
                float valC = reader.ReadSingle(); // 读取 C
                float valM = reader.ReadSingle(); // 读取 m

                // 2. 初始化帧容器
                if (!_allSmokeFrames.ContainsKey(time))
                {
                    _allSmokeFrames[time] = new SmokeFrame();
                    int totalCells = widthX * lengthZ;
                    // 初始化三个数组
                    _allSmokeFrames[time].DensityGrid = new float[totalCells];
                    _allSmokeFrames[time].GridC = new float[totalCells];
                    _allSmokeFrames[time].GridM = new float[totalCells];
                }
                SmokeFrame frame = _allSmokeFrames[time];

                // 3. 存入渲染列表 (仅烟雾)
                Vector3 pos = new Vector3(x, unity_y, unity_z);
                // 性能提示：如果是几十万个点，建议在这里做个阈值判断，只有浓度>0.1才加入渲染列表，否则渲染压力太大
                if (density > 0.1f)
                {
                    frame.RenderMatrices.Add(Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one * gridStep));
                    frame.RenderDensities.Add(density);
                }

                // 4. 存入逻辑网格 (全部存入，用于计算恐慌)
                int gridX = Mathf.RoundToInt((x - minX) / gridStep);
                int gridZ = Mathf.RoundToInt((unity_z - minZ) / gridStep);

                if (gridX >= 0 && gridX < widthX && gridZ >= 0 && gridZ < lengthZ)
                {
                    int index = gridZ * widthX + gridX;

                    // 逻辑：如果同一个格子有多个高度的数据 (3D -> 2D投影)，取"最严重"的那个
                    // 这里假设密度最大时，C和m也最严重。如果不是，你可能需要单独判断
                    if (frame.DensityGrid[index] < density)
                    {
                        frame.DensityGrid[index] = density;
                        frame.GridC[index] = valC; // 存入 C
                        frame.GridM[index] = valM; // 存入 m
                    }
                }
            }
        }
        Debug.Log("二进制全量火灾数据(含C/m)加载完成！");
    }

    // ---------------------------------------------------------
    // 新的查询接口：供 HumanControl 调用
    // ---------------------------------------------------------
    public FireEnvData GetEnvironmentData(Vector3 pos, float time)
    {
        FireEnvData data = new FireEnvData();

        // 简单的时间映射
        int frameIndex = Mathf.FloorToInt(time * 2f); // 假设是0.5s一步
        // 如果你的时间是浮点数key，最好用 Mathf.Round 找最近的key，或者用 _smokeFramesByIndex 优化
        // 这里假设 _allSmokeFrames 的 key 已经是 0.0, 0.5, 1.0...
        float timeKey = Mathf.Round(time * 2f) / 2f;

        if (_allSmokeFrames.TryGetValue(timeKey, out SmokeFrame frame))
        {
            int gridX = Mathf.RoundToInt((pos.x - minX) / gridStep);
            int gridZ = Mathf.RoundToInt((pos.z - minZ) / gridStep);

            if (gridX >= 0 && gridX < widthX && gridZ >= 0 && gridZ < lengthZ)
            {
                int index = gridZ * widthX + gridX;
                data.SmokeDensity = frame.DensityGrid[index];
                data.ValueC = frame.GridC[index];
                data.ValueM = frame.GridM[index];
                return data;
            }
        }

        return data; // 返回空数据 (0,0,0)
    }

    // 在 FixedUpdate 更新当前帧指针
    private void UpdateSmokeFrame()
    {
        // 找到最近的时间点 (0.5的倍数)
        float snapTime = Mathf.Round(runtime /10f) / 2f;

        if (_allSmokeFrames.ContainsKey(snapTime))
        {
            CurrentFrameData = _allSmokeFrames[snapTime];
           // print("当前火焰数据为"+CurrentFrameData.ToString());
        }
    }
}