using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;
using UnityEngine;
using UnityEngine.AI;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class RobotBrain : Agent
{
    [Header("Environment References")]
    public EnvControl myEnv;

    [Header("Runtime Binding (Auto-Filled)")]
    public GameObject robot;
    [HideInInspector] public NavMeshAgent robotNavMeshAgent;
    [HideInInspector] public RobotControl robotInfo;
    [HideInInspector] public Rigidbody robotRigidbody;

    [Header("Runtime State")]
    public bool RobotIsInitialized = false;
    public Vector3 robotDestinationCache;
    public int stuckCounter;
    public Vector3 targetPosition;

    // ---------------------------------------------------------
    // 1. 观测空间配置 (Total Size: 110)
    // ---------------------------------------------------------
    public const int MAX_ROBOTS = 3;
    public const int OBS_NEAREST_HUMANS = 10;
    public const int MAX_ROOMS = 20;
    public const int MAX_EXITS = 2;
    public const int MAX_FIRES = 3;
    public const int TOTAL_OBS_SIZE = 110; // 总观测长度，用于Padding

    private NavMeshPath _tempPath;

    public override void Initialize()
    {
        _tempPath = new NavMeshPath();
    }

    // =========================================================
    // 核心生命周期：动态绑定
    // =========================================================

    /// <summary>
    /// 当 EnvControl 生成机器人实体后调用此函数进行连接
    /// </summary>
    public void BindRobotBody(GameObject newRobotBody)
    {
        if (newRobotBody == null) return;

        robot = newRobotBody;
        robotNavMeshAgent = robot.GetComponent<NavMeshAgent>();
        robotInfo = robot.GetComponent<RobotControl>();
        robotRigidbody = robot.GetComponent<Rigidbody>();

        // 双向绑定
        if (robotInfo != null) robotInfo.myAgent = this;

        // 重置状态
        stuckCounter = 0;
        if (robot != null) robotDestinationCache = robot.transform.position;

        RobotIsInitialized = true;
    }

    /// <summary>
    /// 当回合结束或机器人销毁时调用
    /// </summary>
    public void UnbindRobotBody()
    {
        RobotIsInitialized = false;
        robot = null;
        robotNavMeshAgent = null;
        robotInfo = null;
        robotRigidbody = null;
    }

    // =========================================================
    // 游戏循环
    // =========================================================

    private void FixedUpdate()
    {

        //print("sadas");
        // 【关键保护】如果还没绑定身体，直接跳过
        if (!RobotIsInitialized || robot == null) return;

        // 决策请求逻辑 (训练模式)
        if (myEnv.isTraining)
        {
            float dist = Vector3.Distance(robot.transform.position, robotDestinationCache);
            // 距离小于 2m 或卡住时请求决策
            if (dist < 2.0f || stuckCounter > 50)
            {
                RequestDecision();
            }
        }
    }

    public override void OnEpisodeBegin()
    {
        stuckCounter = 0;
        if (robot != null) robotDestinationCache = robot.transform.position;
    }

    // =========================================================
    // 观测与动作
    // =========================================================

    public override void CollectObservations(VectorSensor sensor)
    {
        // 【关键保护】如果没有身体，必须填充占位数据，否则 ML-Agents 会报错
        if (myEnv == null || !RobotIsInitialized || robot == null)
        {
            for (int i = 0; i < TOTAL_OBS_SIZE; i++) sensor.AddObservation(0f);
            return;
        }

        // A. 自我状态 [4]
        Vector3 selfPosNorm = NormalizedPos(robot.transform.position);
        sensor.AddObservation(selfPosNorm.x);
        sensor.AddObservation(selfPosNorm.z);
        sensor.AddObservation(Mathf.Clamp01(robotInfo.robotFollowerCounter / 10f));
        sensor.AddObservation(stuckCounter > 0 ? 1f : 0f);

        // B. 队友信息 [4]
        int teammateCount = 0;
        foreach (var agent in myEnv.RobotBrainList)
        {
            if (agent == this || !agent.RobotIsInitialized || agent.robot == null) continue;
            if (teammateCount < MAX_ROBOTS - 1)
            {
                Vector3 tmPos = NormalizedPos(agent.robot.transform.position);
                sensor.AddObservation(tmPos.x);
                sensor.AddObservation(tmPos.z);
                teammateCount++;
            }
        }
        for (int i = 0; i < (MAX_ROBOTS - 1) - teammateCount; i++)
        {
            sensor.AddObservation(-1f); sensor.AddObservation(-1f);
        }

        // C. 最近的10个人 [50]
        Vector3 myPos = robot.transform.position;
        var nearestHumans = myEnv.personList
            .Where(h => h != null && h.isActiveAndEnabled)
            .OrderBy(h => (h.transform.position - myPos).sqrMagnitude)
            .Take(OBS_NEAREST_HUMANS)
            .ToList();

        for (int i = 0; i < OBS_NEAREST_HUMANS; i++)
        {
            if (i < nearestHumans.Count)
            {
                var h = nearestHumans[i];
                Vector3 hPos = NormalizedPos(h.transform.position);
                sensor.AddObservation(hPos.x);
                sensor.AddObservation(hPos.z);

                float stateVal = 0f;
                if (h.myLeader != null) stateVal = 0.66f;
                else if (h.CurrentState == 1) stateVal = 0.33f;

                sensor.AddObservation(stateVal);
                sensor.AddObservation(Mathf.Clamp01(h.stateTime / 30f));
                sensor.AddObservation(h.health / 100f);
            }
            else
            {
                sensor.AddObservation(-1f); sensor.AddObservation(-1f);
                sensor.AddObservation(0f); sensor.AddObservation(0f); sensor.AddObservation(0f);
            }
        }

        // D. 全局环境 [52]
        sensor.AddObservation(myEnv.CachedAliveHumans / 50f);
        sensor.AddObservation(myEnv.CachedAvgHealthDecay);
        AddPosListObservation(sensor, myEnv.cachedRoomPositions, MAX_ROOMS);
        AddObjListObservation(sensor, myEnv.Exits, MAX_EXITS);
        AddPosListObservation(sensor, myEnv.FirePosition, MAX_FIRES);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // 【关键保护】
        if (!RobotIsInitialized || robot == null) return;

        // 解析动作
        float actionX = actions.ContinuousActions[0];
        float actionZ = actions.ContinuousActions[1];

        // 坐标映射
        float mapW = myEnv.complexityControl.buildingGeneration.totalWidth;
        float mapH = myEnv.complexityControl.buildingGeneration.totalHeight;
        float targetX = (Mathf.Clamp(actionX, -1f, 1f) * 0.5f + 0.5f) * mapW;
        float targetZ = (Mathf.Clamp(actionZ, -1f, 1f) * 0.5f + 0.5f) * mapH;

        targetPosition = new Vector3(targetX, 0.5f, targetZ);

        // 出口吸附逻辑
        if (robotInfo.robotFollowerCounter > 0 && myEnv.Exits.Count > 0)
        {
            if (Vector3.Distance(targetPosition, myEnv.Exits[0].transform.position) < 5f)
            {
                targetPosition = myEnv.Exits[0].transform.position;
            }
        }

        // 导航执行
        if (IsReachable(targetPosition))
        {
            stuckCounter = 0;
            robotDestinationCache = targetPosition;
            robotNavMeshAgent.SetDestination(targetPosition);
        }
        else
        {
            stuckCounter++;
        }
    }

    // =========================================================
    // 辅助工具函数
    // =========================================================

    private bool IsReachable(Vector3 target)
    {
        if (robotNavMeshAgent == null) return false;
        if (robotNavMeshAgent.CalculatePath(target, _tempPath))
        {
            return _tempPath.status == NavMeshPathStatus.PathComplete;
        }
        return false;
    }

    private Vector3 NormalizedPos(Vector3 pos)
    {
        if (myEnv == null) return Vector3.zero;
        float w = myEnv.complexityControl.buildingGeneration.totalWidth;
        float h = myEnv.complexityControl.buildingGeneration.totalHeight;
        return new Vector3((pos.x / w) * 2f - 1f, 0.5f, (pos.z / h) * 2f - 1f);
    }

    private void AddPosListObservation(VectorSensor sensor, List<Vector3> list, int maxCount)
    {
        for (int i = 0; i < maxCount; i++)
        {
            if (list != null && i < list.Count)
            {
                Vector3 p = NormalizedPos(list[i]);
                sensor.AddObservation(p.x); sensor.AddObservation(p.z);
            }
            else { sensor.AddObservation(-1f); sensor.AddObservation(-1f); }
        }
    }

    private void AddObjListObservation(VectorSensor sensor, List<GameObject> list, int maxCount)
    {
        for (int i = 0; i < maxCount; i++)
        {
            if (list != null && i < list.Count && list[i] != null)
            {
                Vector3 p = NormalizedPos(list[i].transform.position);
                sensor.AddObservation(p.x); sensor.AddObservation(p.z);
            }
            else { sensor.AddObservation(-1f); sensor.AddObservation(-1f); }
        }
    }

    // 奖励日志 (略有清理)
    private Dictionary<string, float> rewardLog = new Dictionary<string, float>();
    public void LogReward(string type, float value)
    {
        if (rewardLog.ContainsKey(type)) rewardLog[type] += value;
        else rewardLog[type] = value;
    }

    void OnDestroy()
    {
        if (robotInfo != null) SaveRewardLog();
    }

    private void SaveRewardLog()
    {
        string dirPath = Path.Combine(Application.persistentDataPath, "TrainingLogs");
        string fileName = $"Reward_{gameObject.name}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        // 实际写入逻辑略...
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        if (!RobotIsInitialized || robot == null) return;

        var continuousActions = actionsOut.ContinuousActions;
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 currentNorm = NormalizedPos(robot.transform.position);
        continuousActions[0] = Mathf.Clamp(currentNorm.x + h * 0.1f, -1f, 1f);
        continuousActions[1] = Mathf.Clamp(currentNorm.z + v * 0.1f, -1f, 1f);
    }
}