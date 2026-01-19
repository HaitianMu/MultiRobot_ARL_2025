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
    public TextAsset fireBinaryFile;
    private Dictionary<int, SmokeFrame> _smokeFramesByIndex = new Dictionary<int, SmokeFrame>();

    [Header("Map Settings (时间映射设置)")]
    public float originalDataDuration = 30f; // 原始数据只有30秒
    public float targetRunDuration = 150f;   // 你希望它运行150秒
    public bool clampTime = true;            // 如果超过150秒，是否卡在最后一帧

    [Header("Grid Settings")]
    public float gridStep = 0.5f;
    public float minX = -10f;
    public float minZ = -10f;
    public int widthX = 100;
    public int lengthZ = 100;

    private Dictionary<float, SmokeFrame> _allSmokeFrames = new Dictionary<float, SmokeFrame>();
    // 【新增】简单的缓存变量，避免每帧都去查字典
    private float _lastFoundTimeKey = -1f;
    private SmokeFrame _cachedFrame = null;

    // 【配置】根据您的CSV数据设定
    private const float DATA_TIME_STEP = 0.5f; // 数据每0.5秒一行
    private const float MIN_DATA_TIME = 0.5f;  // 数据起始时间
    public SmokeFrame CurrentFrameData;

    // 缓存时间缩放比例
    private float _timeScaleRatio;
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
    // 核心工具函数：将 真实运行时间 映射为 数据时间
    // ---------------------------------------------------------
    private float GetMappedDataTime(float realTime)
    {
        // 1. 缩放时间 (例如 realTime=75s -> dataTime=15s)
        float mappedTime = realTime * _timeScaleRatio;

        // 2. 限制范围 (防止超出30秒导致报错，或者让其停留在最后一帧)
        if (clampTime)
        {
            mappedTime = Mathf.Clamp(mappedTime, 0f, originalDataDuration);
        }

        // 3. 对齐到最近的 0.5 (你的数据Key步长)
        // 比如 15.1s 应该取 15.0s 的数据，15.4s 应该取 15.5s 的数据
        float snappedTime = Mathf.Round(mappedTime * 2f) / 2f;

        return snappedTime;
    }

    // ---------------------------------------------------------
    // 新的查询接口：供 HumanControl 调用
    // ---------------------------------------------------------
    // ---------------------------------------------------------
    // 修改后的查询接口：HumanControl 调用时传入当前游戏时间
    // ---------------------------------------------------------
    // 【配置】根据您的CSV数据设定
    public FireEnvData GetEnvironmentData(Vector3 pos, float realTime)
    {
        FireEnvData data = new FireEnvData();

        // ---------------------------------------------------------
        // 1. 时间映射优化：数学取整替代搜索
        // ---------------------------------------------------------
        // 逻辑：将当前时间除以步长，四舍五入后再乘回来。
        // 例如 realTime=1.2, Step=0.5 -> 1.2/0.5=2.4 -> Round=2 -> 2*0.5 = 1.0 (Key)
        // 例如 realTime=1.3, Step=0.5 -> 1.3/0.5=2.6 -> Round=3 -> 3*0.5 = 1.5 (Key)
        float timeKey = Mathf.Round(realTime / DATA_TIME_STEP) * DATA_TIME_STEP;

        // 修正：如果算出来的时间小于数据的起始时间，就用起始时间
        if (timeKey < MIN_DATA_TIME) timeKey = MIN_DATA_TIME;

        // ---------------------------------------------------------
        // 2. 缓存优化：如果时间 Key 没变，直接用上一帧的数据对象
        // ---------------------------------------------------------
        SmokeFrame frame = null;

        // 判断当前 Key 是否和上次查的一样 (使用极小误差比较浮点数)
        if (Mathf.Abs(timeKey - _lastFoundTimeKey) < 0.01f && _cachedFrame != null)
        {
            frame = _cachedFrame;
        }
        else
        {
            // 只有时间跨度变了，才真正去查字典
            if (_allSmokeFrames.TryGetValue(timeKey, out frame))
            {
                _cachedFrame = frame;
                _lastFoundTimeKey = timeKey;
            }
            else
            {
                // 如果查不到（比如时间超出了 CSV 范围），保持使用最后一次缓存的帧
                // 这样避免时间超限后数据归零
                frame = _cachedFrame;
            }
        }

        // ---------------------------------------------------------
        // 3. 空间取值
        // ---------------------------------------------------------
        if (frame != null)
        {
            // 因为您的数据是整数步进 (-24, -23...)，gridStep 应该是 1
            // 直接 RoundToInt 即可，比除法快且准
            int gridX = Mathf.RoundToInt(pos.x - minX);
            int gridZ = Mathf.RoundToInt(pos.z - minZ);

            // 范围检查
            if (gridX >= 0 && gridX < widthX && gridZ >= 0 && gridZ < lengthZ)
            {
                int index = gridZ * widthX + gridX;

                // 确保索引安全
                if (index < frame.DensityGrid.Length)
                {
                    data.SmokeDensity = frame.DensityGrid[index];
                    data.ValueC = frame.GridC[index];
                    data.ValueM = frame.GridM[index];
                }
            }
        }

        return data;
    }


// ---------------------------------------------------------
// 修改后的渲染更新：FixedUpdate 中使用 runtime
// ---------------------------------------------------------

private void UpdateSmokeFrame()
    {
        // 使用映射逻辑获取当前应该渲染哪一帧
        float snapTime = GetMappedDataTime(runtime);

        if (_allSmokeFrames.ContainsKey(snapTime))
        {
            CurrentFrameData = _allSmokeFrames[snapTime];
        }
    }
}