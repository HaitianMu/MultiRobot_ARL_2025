using System;
using System.Collections.Generic;
using System.IO;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class HumanBrain : Agent
{
    [Header("References")]
    public EnvControl myEnv;
    public HumanControl myHuman;
    private RayPerceptionSensorComponent3D _raySensor;

    [Header("Decision Frequency")]
    [Tooltip("多久做一次决定（秒）。值越大，行为越稳定，收敛越容易。")]
    public float decisionInterval = 1.0f; // 建议设置为 0.5 ~ 1.0 秒
    private float _decisionTimer = 0f;

    [Header("Status")]
    public bool HumanIsInitialized = false;
    public int HumanState; // 当前执行的状态

    // ---------------------------------------------------------
    // 1. 初始化
    // ---------------------------------------------------------
    public override void Initialize()
    {
        if (myHuman == null) myHuman = GetComponent<HumanControl>();
        _raySensor = GetComponent<RayPerceptionSensorComponent3D>();

        // 随机化初始计时器，防止所有人类在同一帧同时请求决策（分散计算压力）
        _decisionTimer = UnityEngine.Random.Range(0f, decisionInterval);
    }

    // ---------------------------------------------------------
    // 2. 游戏循环 (控制决策频率)
    // ---------------------------------------------------------
    public void FixedUpdate()
    {
        if (!myEnv.useHumanAgent || myHuman == null) return;

        // 累加时间
        _decisionTimer += Time.fixedDeltaTime;

        // 只有当时间到了，才请求新的决策
        if (_decisionTimer >= decisionInterval)
        {
            _decisionTimer = 0f;
            RequestDecision(); // 主动请求决策 -> 触发 CollectObservations -> OnActionReceived
        }

        // 注意：在两次决策之间，myHuman.CurrentState 会保持上一次的值不变
        // 这就是我们想要的“状态保持”效果

        // 可选：每一帧给一点微小的生存奖励 (即便不做决策)
        if (myEnv.isTraining)
        {
            AddReward(0.0001f);
        }
    }

    // ---------------------------------------------------------
    // 3. 拟真观测
    // ---------------------------------------------------------
    // ---------------------------------------------------------
    // 核心修改：细化身体感受观测
    // ---------------------------------------------------------
    public override void CollectObservations(VectorSensor sensor)
    {
        // 1. 安全检查与补齐 (Total Size = 5)
        if (myEnv == null || myHuman == null || !myEnv.useHumanAgent)
        {
            sensor.AddObservation(0f); // Health
            sensor.AddObservation(0f); // CO
            sensor.AddObservation(0f); // Temp
            sensor.AddObservation(0f); // Panic
            sensor.AddObservation(0f); // State
            return;
        }

        // 2. 获取环境数据
        var envData = myEnv.GetEnvironmentData(transform.position, myEnv.runtime);

        // 3. 视觉模拟 (更新射线长度)
        if (_raySensor != null)
        {
            // 能见度越低(m)，看得越近
            float visualRange = Mathf.Clamp(envData.ValueM, 2f, 20f);
            _raySensor.RayLength = visualRange;
        }

        // ===================================================
        // 4. 身体感受 (Vector Observation Size: 5)
        // ===================================================

        // [1] 生命值 (归一化 0-1)
        sensor.AddObservation(myHuman.health / 100f);

        // [2] CO 浓度 (归一化)
        // 假设危险范围是 0 - 1000 ppm
        // SmokeDensity 就是我们解析的 CO 浓度
        sensor.AddObservation(Mathf.Clamp01(envData.SmokeDensity / 1000f));

        // [3] 温度感知 (归一化)
        // 假设常温 20度，火场高温 800度
        // 使用 InverseLerp 将 20-800 映射到 0-1
        sensor.AddObservation(Mathf.InverseLerp(20f, 800f, envData.ValueC));

        // [4] 恐慌等级 (0-1)
        sensor.AddObservation(myHuman.panicLevel);

        // [5] 当前执行的状态 (记忆)
        // 0,1,2 -> 归一化到 0-1
        sensor.AddObservation(HumanState / 2f);
    }

    // ---------------------------------------------------------
    // 4. 动作接收 (只在 RequestDecision 后调用一次)
    // ---------------------------------------------------------
    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!myEnv.useHumanAgent || myHuman == null) return;

        // 获取离散动作 (0:冷静, 1:焦虑, 2:恐慌)
        int actionState = actions.DiscreteActions[0];

        // 更新状态
        HumanState = actionState;

        // 同步给身体脚本 (这一步很重要，身体脚本根据这个变量去执行具体的移动逻辑)
        // 只有当 myEnv.useHumanAgent 为 true 时，HumanControl 才会听这里的
        myHuman.CurrentState = HumanState;

        // -----------------------------------------------------
        // 奖励计算 (针对这一次决策的好坏)
        // -----------------------------------------------------
        if (myEnv.isTraining)
        {
            var envData = myEnv.GetEnvironmentData(transform.position, myEnv.runtime);
            float danger = envData.SmokeDensity;

            // 逻辑惩罚：
            // 环境安全(烟雾<100)却选择恐慌(2) -> 浪费体力，给惩罚
            if (danger < 100f && actionState == 2)
            {
                AddReward(-0.01f);
            }
            // 环境危险(烟雾>1000)却选择冷静(0) -> 反应迟钝，给大惩罚
            else if (danger > 1000f && actionState == 0)
            {
                AddReward(-0.02f);
            }
            // 环境一般危险，且选择了焦虑(1) -> 给予鼓励 (可选)
            else if (danger >= 100f && danger <= 1000f && actionState == 1)
            {
                AddReward(0.005f);
            }
        }
    }

    // ---------------------------------------------------------
    // 5. 手动测试
    // ---------------------------------------------------------
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;

        // 安全检查
        if (discreteActions.Length == 0) return;

        if (Input.GetKey(KeyCode.Alpha1)) discreteActions[0] = 0;
        else if (Input.GetKey(KeyCode.Alpha2)) discreteActions[0] = 1;
        else if (Input.GetKey(KeyCode.Alpha3)) discreteActions[0] = 2;
        // 如果没有按键，保持当前状态 (这在 Heuristic 中比较难实现完全保持，默认通常是0)
    }

    // ---------------------------------------------------------
    // 6. 奖励日志 (保持不变)
    // ---------------------------------------------------------
    private Dictionary<string, float> rewardLog = new Dictionary<string, float>();

    public void LogReward(string type, float value)
    {
        if (rewardLog.ContainsKey(type)) rewardLog[type] += value;
        else rewardLog[type] = value;
    }

    void OnDestroy()
    {
        if (myHuman != null) SaveRewardLog();
    }

    private void SaveRewardLog()
    {
        string directoryPath = Path.Combine(Application.persistentDataPath, "HumanReward");
        // 使用 Guid 防止文件名重复冲突
        string fileName = $"Human_{Guid.NewGuid().ToString().Substring(0, 8)}_{DateTime.Now:HHmmss}.txt";
        string filePath = Path.Combine(directoryPath, fileName);

        try
        {
            if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);

            using (StreamWriter writer = new StreamWriter(filePath))
            {
                writer.WriteLine("Type,Value");
                foreach (var kv in rewardLog)
                {
                    writer.WriteLine($"{kv.Key},{kv.Value}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"保存奖励失败: {e.Message}");
        }
    }
}