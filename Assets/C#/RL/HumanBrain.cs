using System;
using System.Collections.Generic;
using System.IO;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.AI;

public class HumanBrain : Agent
{
    [Header("References")]
    public EnvControl myEnv;
    public HumanControl myHuman;
    private RayPerceptionSensorComponent3D _raySensor;

    [Header("Reward Tuning")]
    [Tooltip("每靠近/远离出口1米的奖励权重")]
    public float distanceMultiplier = 0.1f;
    [Tooltip("每损失1点血量的惩罚权重")]
    public float healthPenaltyMultiplier = 0.05f;
    [Tooltip("每秒生存的固定惩罚(时间成本)")]
    public float timePenalty = -0.001f;

    [Header("Decision Frequency")]
    public float decisionInterval = 3f;
    private float _decisionTimer = 0f;

    // --- 内部追踪变量 ---
    private NavMeshPath _path;
    private float _lastDistanceToExit;
    private float _lastHealth;
    public int HumanState;

    // ---------------------------------------------------------
    // 1. 初始化与回合开始
    // ---------------------------------------------------------
    public override void Initialize()
    {
        if (myHuman == null) myHuman = GetComponent<HumanControl>();
        _raySensor = GetComponent<RayPerceptionSensorComponent3D>();
        _path = new NavMeshPath();
    }

    public override void OnEpisodeBegin()
    {
        // 回合开始时重置状态追踪，防止上一局的数据污染奖励计算
        _lastHealth = (myHuman != null) ? myHuman.health : 100f;
        _lastDistanceToExit = GetDistanceToNearestExit();
        _decisionTimer = UnityEngine.Random.Range(0f, decisionInterval);
    }

    // ---------------------------------------------------------
    // 2. 游戏循环 (控制决策频率)
    // ---------------------------------------------------------
    public void FixedUpdate()
    {
        if (!myEnv.useHumanAgent || myHuman == null) return;

        // 1. 基础生存奖励：时间惩罚（鼓励尽快逃生，而不是在原地刷分）
        if (myEnv.isTraining)
        {
            AddReward(timePenalty);
        }

        // 2. 决策计时器
        _decisionTimer += Time.fixedDeltaTime;
        if (_decisionTimer >= decisionInterval)
        {
            _decisionTimer = 0f;
            RequestDecision();
        }
    }

    // ---------------------------------------------------------
    // 3. 拟真观测 (Vector Observation Size: 5)
    // ---------------------------------------------------------
    public override void CollectObservations(VectorSensor sensor)
    {
        if (myEnv == null || myHuman == null)
        {
            sensor.AddObservation(new float[5]);
            return;
        }

        var envData = myEnv.GetEnvironmentData(transform.position, myEnv.runtime);

        // [1] 生命值归一化 (0-1)
        sensor.AddObservation(myHuman.health / 100f);
        // [2] 危险感知：CO浓度 (0-1000ppm -> 0-1)
        sensor.AddObservation(Mathf.Clamp01(envData.SmokeDensity / 1000f));
        // [3] 温度感知 (20-200度映射到0-1)
        sensor.AddObservation(Mathf.InverseLerp(20f, 200f, envData.ValueC));
        // [4] 当前身体恐慌等级 (0-1)
        sensor.AddObservation(myHuman.panicLevel);
        // [5] 记忆：当前正在执行的状态
        sensor.AddObservation(HumanState / 2f);

        // 视觉模拟：动态调整射线长度（能见度影响视野）
        if (_raySensor != null)
        {
            _raySensor.RayLength = Mathf.Clamp(envData.ValueM, 3f, 25f);
        }
    }

    // ---------------------------------------------------------
    // 4. 动作与奖励 (OnActionReceived)
    // ---------------------------------------------------------
    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!myEnv.useHumanAgent || myHuman == null) return;

        // --- 1. 执行动作 ---
        int actionState = actions.DiscreteActions[0];
        HumanState = actionState;
        myHuman.CurrentState = actionState; // 同步给身体脚本

        if (!myEnv.isTraining) return;

        // --- 2. 距离增量奖励 (核心：Path-finding Reward) ---
        float currentDist = GetDistanceToNearestExit();
        // 确保距离计算是有效的（非初始值）
        if (_lastDistanceToExit < 998f)
        {
            float distDelta = _lastDistanceToExit - currentDist;
            // 限制单步奖励上限，防止NavMesh重算导致的跳变
            AddReward(Mathf.Clamp(distDelta, -2f, 2f) * distanceMultiplier);
        }
        _lastDistanceToExit = currentDist;

        // --- 3. 健康惩罚 (核心：直接反馈) ---
        // 修正：使用成员变量 _lastHealth 进行跨决策追踪
        float healthDelta = myHuman.health - _lastHealth;
        if (healthDelta < 0)
        {
            AddReward(healthDelta * healthPenaltyMultiplier); // 掉血是负数，AddReward 负值
        }
        _lastHealth = myHuman.health;

        // --- 4. 环境匹配逻辑惩罚 (引导 Agent 学习状态含义) ---
        var envData = myEnv.GetEnvironmentData(transform.position, myEnv.runtime);

        // 致命错误：极度危险却选择冷静
        if (envData.SmokeDensity > 800f && actionState == 0)
            AddReward(-0.05f);

        // 效率错误：完全安全却选择极度恐慌
        if (envData.SmokeDensity < 50f && actionState == 2)
            AddReward(-0.02f);
    }

    // ---------------------------------------------------------
    // 5. 辅助方法 (NavMesh 路径距离)
    // ---------------------------------------------------------
    private float GetDistanceToNearestExit()
    {
        if (myEnv.Exits == null || myEnv.Exits.Count == 0) return 999f;

        float minPathDist = float.MaxValue;
        Vector3 currentPos = transform.position;

        foreach (GameObject exit in myEnv.Exits)
        {
            if (exit == null || !exit.activeInHierarchy) continue;

            // 计算路径距离（比直线距离更能引导Agent穿过房门）
            if (NavMesh.CalculatePath(currentPos, exit.transform.position, NavMesh.AllAreas, _path))
            {
                if (_path.status == NavMeshPathStatus.PathComplete)
                {
                    float pathLength = CalculatePathLength(_path);
                    if (pathLength < minPathDist) minPathDist = pathLength;
                }
            }
        }

        // 如果路径不可达，保底使用直线距离
        return minPathDist == float.MaxValue ? Vector3.Distance(currentPos, myEnv.Exits[0].transform.position) : minPathDist;
    }

    private float CalculatePathLength(NavMeshPath path)
    {
        float length = 0;
        for (int i = 1; i < path.corners.Length; i++)
        {
            length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        }
        return length;
    }

    // ---------------------------------------------------------
    // 6. 启发式手动控制
    // ---------------------------------------------------------
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;
        if (Input.GetKey(KeyCode.Alpha1)) discreteActions[0] = 0;
        else if (Input.GetKey(KeyCode.Alpha2)) discreteActions[0] = 1;
        else if (Input.GetKey(KeyCode.Alpha3)) discreteActions[0] = 2;
    }
}