using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;
using UnityEngine;
using UnityEngine.AI;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.VisualScripting;

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

    [Header("Decision Frequency")]
    // [建议] 0.2f ~ 0.5f。太小导致动作抖动，太大导致反应迟钝。
    public float decisionInterval = 0.5f;
    private float _decisionTimer = 0f;

    [Header("Loitering Detection (徘徊检测)")]
    public float loiterCheckInterval = 2.0f; // 每 2 秒检查一次位移
    public float loiterRadius = 10.0f;        // 如果 5 秒内移动没超过 5 米，视为徘徊

    private Vector3 _loiterAnchorPos;        // 上一次检查时的位置锚点
    private float _loiterTimer = 0f;         // 徘徊计时器
    private bool _isLoitering = false;       // 是否处于徘徊状态

    // --- 内部变量：用于计算奖励 ---
    private float _lastDistToExit = 9999f;
    private float _lastDistToHuman = -1f; // 用于记录上一帧到最近人类的距离
    private bool _isUsingGreedy = false;  // 记录当前是否处于贪心保底状态 (状态锁)

    // ---------------------------------------------------------
    // 1. 观测空间配置 (Total Size: 80)
    // ---------------------------------------------------------
    public const int MAX_ROBOTS = 5;
    public const int OBS_NEAREST_HUMANS = 10;
    public const int MAX_EXITS = 3;
    public const int MAX_FIRES = 3;
    public const int TOTAL_OBS_SIZE = 80;
    private NavMeshPath _tempPath;

    public override void Initialize()
    {
        _tempPath = new NavMeshPath();
    }

    // =========================================================
    // 核心生命周期：动态绑定
    // =========================================================

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
        if (robot != null)
        {
            robotDestinationCache = robot.transform.position;
            _loiterAnchorPos = robot.transform.position;
        }

        // 随机初始化计时器，错峰决策
        _decisionTimer = UnityEngine.Random.Range(0f, decisionInterval);
        _loiterTimer = 0f;
        _isLoitering = false;
        _isUsingGreedy = false;

        RobotIsInitialized = true;
    }

    public void UnbindRobotBody()
    {
        RobotIsInitialized = false;
        robot = null;
        robotNavMeshAgent = null;
        robotInfo = null;
        robotRigidbody = null;
    }

    // =========================================================
    // 游戏循环 (优化后的决策请求逻辑 + 徘徊检测)
    // =========================================================

    private void FixedUpdate()
    {
        // 【关键保护】如果还没绑定身体，直接跳过
        if (!RobotIsInitialized || robot == null) { return; }

        if (myEnv.isTraining)
        {
            float dt = Time.fixedDeltaTime;

            // --- 1. 徘徊检测逻辑 ---
            _loiterTimer += dt;

            // 如果还没判定为徘徊，定期检查
            if (!_isLoitering && _loiterTimer >= loiterCheckInterval)
            {
                float distMoved = Vector3.Distance(robot.transform.position, _loiterAnchorPos);
                if (distMoved < loiterRadius)
                {
                    _isLoitering = true; // 判定为原地打转
                }
                else
                {
                    _loiterAnchorPos = robot.transform.position; // 更新锚点
                    _loiterTimer = 0f;
                }
            }
            // 如果已经在徘徊，检查是否跑出了圈
            else if (_isLoitering)
            {
                float distFromAnchor = Vector3.Distance(robot.transform.position, _loiterAnchorPos);
                if (distFromAnchor > loiterRadius)
                {
                    _isLoitering = false; // 解除徘徊状态
                    _loiterAnchorPos = robot.transform.position;
                    _loiterTimer = 0f;
                }
            }

            // --- 2. 决策请求逻辑 ---
            _decisionTimer += dt;

            // A. 时间到了
            bool timeUp = _decisionTimer >= decisionInterval;

            // B. 确实到达了目的地
            // [优化] 阈值设为 2.0f，避免过早切换目标导致还没走到就回头
            float dist = Vector3.Distance(robot.transform.position, robotDestinationCache);
            bool reached = dist < 2.0f;

            // C. 物理卡死 (NavMesh 无法移动)
            bool stuck = stuckCounter > 30;

            // --- [状态锁] 核心优化 ---
            // 如果正在执行贪心保底，且还没到达目标，且没有卡死，则【不请求】新决策
            // 这能防止神经网络每隔 0.5秒 就打断贪心算法的路径
            if (_isUsingGreedy && !reached && !stuck)
            {
                return;
            }

            // 满足任一条件即请求决策
            if (timeUp || reached || stuck)
            {
                RequestDecision();
                _decisionTimer = 0f; // 重置计时器
            }
        }
    }

    public override void OnEpisodeBegin()
    {
        stuckCounter = 0;
        if (robot != null)
        {
            robotDestinationCache = robot.transform.position;
            _loiterAnchorPos = robot.transform.position;

            // 重置上一帧距离，防止回合开始时的位置跳变产生错误奖励
            _lastDistToExit = GetDistanceToNearestExit(robot.transform.position);
        }
        _decisionTimer = UnityEngine.Random.Range(0f, decisionInterval);
        _loiterTimer = 0f;
        _isLoitering = false;
        _isUsingGreedy = false;
        _lastDistToHuman = -1f;
    }

    // =========================================================
    // 观测收集 (CollectObservations)
    // =========================================================
    public override void CollectObservations(VectorSensor sensor)
    {
        if (myEnv == null || !RobotIsInitialized || robot == null)
        {
            for (int i = 0; i < TOTAL_OBS_SIZE; i++) sensor.AddObservation(0f);
            return;
        }

        Vector3 myPos = robot.transform.position;

        // A. 自我状态 [4]
        Vector3 selfPosNorm = NormalizedPos(myPos);
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

        // D. 全局环境优化
        sensor.AddObservation(myEnv.CachedAliveHumans / 50f);
        sensor.AddObservation(myEnv.CachedAvgHealthDecay);

        // E. 出口排序
        var sortedExits = myEnv.Exits
            .Where(e => e != null)
            .OrderBy(e => Vector3.SqrMagnitude(e.transform.position - myPos))
            .ToList();
        AddObjListObservation(sensor, sortedExits, MAX_EXITS);

        // F. 火源排序
        var sortedFires = myEnv.FirePosition
            .OrderBy(f => Vector3.SqrMagnitude(f - myPos))
            .ToList();
        AddPosListObservation(sensor, sortedFires, MAX_FIRES);

        // 指向最近人类的相对方向
        HumanControl closestH = GetClosestUnrecruitedHuman();
        if (closestH != null)
        {
            Vector3 dirToHuman = (closestH.transform.position - myPos).normalized;
            sensor.AddObservation(dirToHuman.x);
            sensor.AddObservation(dirToHuman.z);
        }
        else
        {
            sensor.AddObservation(0f); sensor.AddObservation(0f);
        }

        // 指向最近出口的相对方向
        Vector3 exitPos = GetNearestExitPos();
        Vector3 dirToExit = (exitPos - myPos).normalized;
        sensor.AddObservation(dirToExit.x);
        sensor.AddObservation(dirToExit.z);
    }

    // =========================================================
    // 动作执行与综合奖励逻辑 (Updated)
    // =========================================================
    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!RobotIsInitialized || robot == null) return;

        // 默认重置贪心锁 (如果在下面没触发贪心，说明这次是模型自主控制)
        _isUsingGreedy = false;

        // 1.解析动作 (神经网络输出)
        float moveX = actions.ContinuousActions[0]; // -1 到 1
        float moveZ = actions.ContinuousActions[1]; // -1 到 1

        // 2. 计算增量式目标点
        Vector3 brainTargetPos = robot.transform.position + new Vector3(moveX, 0, moveZ) * 10.0f;

        // --- 3. 吸附逻辑 (Local Greedy: 只有在有跟随者且靠近出口时才激活) ---
        if (robotInfo.myDirectFollowers.Count > 0 && myEnv.Exits.Count > 0)
        {
            float snapThreshold = 30f;
            Vector3? bestExit = null;
            float minSnapDist = snapThreshold;

            foreach (var exit in myEnv.Exits)
            {
                if (exit == null) continue;
                float d = Vector3.Distance(brainTargetPos, exit.transform.position);
                if (d < minSnapDist)
                {
                    minSnapDist = d;
                    bestExit = exit.transform.position;
                }
            }

            if (bestExit.HasValue)
            {
                NavMeshHit hit;
                if (NavMesh.SamplePosition(bestExit.Value, out hit, 5.0f, NavMesh.AllAreas))
                {
                    brainTargetPos = hit.position;
                }
            }
        }
        targetPosition = brainTargetPos;

        // --- 4. 【核心改动】贪心算法全权接管 (Global Greedy) ---
        bool needsGreedy = false;

        // 检查 A: 路径是否完全不可达 (NavMesh 算不出路)
        if (!IsReachable(targetPosition))
        {
            needsGreedy = true;
        }
        // 检查 B: 物理卡死 (原地撞墙很久)
        if (stuckCounter > 20)
        {
            needsGreedy = true;
        }
        // 检查 C: 逻辑徘徊 (在小范围内打转)
        if (_isLoitering)
        {
            needsGreedy = true;
        }

        if (needsGreedy)
        {
            Vector3 greedyPos = GetGreedyTarget();

            // 特殊处理：如果是因为徘徊(_isLoitering)触发的贪心，
            // 且这个贪心目标(比如最近的人)就在徘徊圈里，那去了也没用。
            // 此时强制去出口，打破死循环。
            if (_isLoitering)
            {
                float distToGreedy = Vector3.Distance(greedyPos, _loiterAnchorPos);
                if (distToGreedy < loiterRadius && myEnv.Exits.Count > 0)
                {
                    greedyPos = GetNearestExitPos();
                }
            }

            // 尝试向贪心目标移动
            if (IsReachable(greedyPos))
            {
                targetPosition = greedyPos;
                _isUsingGreedy = true; // 锁定状态，防止 FixedUpdate 立即请求新决策
                stuckCounter = 0;      // 重置卡死计数，给贪心路径一点时间

                // 引导性惩罚：教育 AI 不要依赖贪心算法
                float penalty = _isLoitering ? -0.01f : -0.005f;
                AddSafeReward(penalty);
            }
        }

        // --- 5. 执行导航 ---
        if (IsReachable(targetPosition))
        {
            stuckCounter = 0;
            // 更新缓存，用于 FixedUpdate 里的距离判断
            robotDestinationCache = targetPosition;
            robotNavMeshAgent.SetDestination(targetPosition);
        }
        else
        {
            stuckCounter++;
            // 终极兜底：如果连贪心点都去不了，找最近的合法点
            NavMeshHit hit;
            if (NavMesh.SamplePosition(targetPosition, out hit, 10.0f, NavMesh.AllAreas))
            {
                robotNavMeshAgent.SetDestination(hit.position);
            }
        }

        // --- 6. 奖励计算 ---
        ExecuteRewardShaping();
    }

    private void ExecuteRewardShaping()
    {
        int followerCount = robotInfo.myDirectFollowers.Count;

        if (followerCount > 0)
        {
            // --- 阶段 A: 领航模式 (带人去出口) ---

            // 1. 距离惩罚 (防跑太快)
            foreach (var human in robotInfo.myDirectFollowers)
            {
                if (human == null) continue;
                float dist = Vector3.Distance(robot.transform.position, human.transform.position);
                if (dist > 5.0f)
                {
                    AddSafeReward(-(dist - 5.0f) * 0.001f);  // 1. 距离惩罚 (防跑太快)
                }
            }

            // 2. 逃生进度奖励
            float currentDistToExit = GetDistanceToNearestExit(robot.transform.position);
            if (IsValidValue(currentDistToExit) && IsValidValue(_lastDistToExit) && _lastDistToExit > 0)
            {
                float diff = _lastDistToExit - currentDistToExit;
                float progressReward = Mathf.Clamp(diff, -1.0f, 1.0f);
                if (progressReward > 0)
                {
                    AddSafeReward(progressReward * 0.1f * followerCount);  // 2. 逃生进度奖励
                }
            }
            _lastDistToExit = currentDistToExit;
            _lastDistToHuman = -1f; // 切换模式时重置寻人距离
        }
        else
        {
            // --- 阶段 B: 搜索模式 (去找人) ---

            // 1. 存活时间成本
            AddSafeReward(-1.0f / MaxStep); // 1. 存活时间成本

            // 2. 寻人引导逻辑
            HumanControl closestHuman = null;
            float minHumanDist = float.MaxValue;

            foreach (var h in myEnv.personList)
            {
                if (h == null || h.myLeader != null) continue;

                float d = Vector3.Distance(robot.transform.position, h.transform.position);
                if (d < minHumanDist)
                {
                    minHumanDist = d;
                    closestHuman = h;
                }
            }

            if (closestHuman != null)
            {
                if (_lastDistToHuman > 0 && minHumanDist < _lastDistToHuman)
                {
                    float diff = _lastDistToHuman - minHumanDist;
                    AddSafeReward(Mathf.Clamp(diff, 0f, 1f) * 0.01f); // 靠近人类给小奖
                }
                _lastDistToHuman = minHumanDist;
            }
            else
            {
                _lastDistToHuman = -1f;
            }

            _lastDistToExit = -1f; // 切换模式时重置出口距离
        }
    }

    // =========================================================
    // 贪心算法核心
    // =========================================================
    private Vector3 GetGreedyTarget()
    {
        // 优先级 1: 如果有跟随者，贪心目标是最近的出口
        if (robotInfo.myDirectFollowers.Count > 0)
        {
            return GetNearestExitPos();
        }

        // 优先级 2: 如果没有跟随者，贪心目标是最近的“野生”人类
        HumanControl targetHuman = GetClosestUnrecruitedHuman();
        if (targetHuman != null)
        {
            return targetHuman.transform.position;
        }

        // 优先级 3: 兜底逻辑 - 去最近的出口
        return GetNearestExitPos();
    }

    // =========================================================
    // 辅助工具函数
    // =========================================================

    private HumanControl GetClosestUnrecruitedHuman()
    {
        if (myEnv == null || myEnv.personList == null || myEnv.personList.Count == 0) return null;

        HumanControl closest = null;
        float minDir = float.MaxValue;

        foreach (var h in myEnv.personList)
        {
            if (h == null || h.myLeader != null || !h.isActiveAndEnabled) continue;

            float dist = Vector3.Distance(robot.transform.position, h.transform.position);
            if (dist < minDir)
            {
                minDir = dist;
                closest = h;
            }
        }
        return closest;
    }

    private float GetDistanceToNearestExit(Vector3 pos)
    {
        if (myEnv.Exits == null || myEnv.Exits.Count == 0) return 9999f;

        float minD = float.MaxValue;
        foreach (var exit in myEnv.Exits)
        {
            if (exit == null) continue;
            float d = Vector3.Distance(pos, exit.transform.position);
            if (d < minD) minD = d;
        }
        return minD;
    }

    private Vector3 GetNearestExitPos()
    {
        if (myEnv.Exits == null || myEnv.Exits.Count == 0)
            return robot != null ? robot.transform.position : Vector3.zero;

        Vector3 bestPos = myEnv.Exits[0].transform.position;
        float minDir = float.MaxValue;

        foreach (var exit in myEnv.Exits)
        {
            if (exit == null) continue;
            float dist = Vector3.Distance(robot.transform.position, exit.transform.position);
            if (dist < minDir)
            {
                minDir = dist;
                bestPos = exit.transform.position;
            }
        }
        return bestPos;
    }

    private bool IsReachable(Vector3 target)
    {
        if (robotNavMeshAgent == null) return false;
        // 计算路径但不实际移动，用于预判
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

    private void AddSafeReward(float value)
    {
        if (float.IsInfinity(value) || float.IsNaN(value)) return;
        AddReward(value);
    }

    private bool IsValidValue(float value)
    {
        return !float.IsInfinity(value) && !float.IsNaN(value) && value >= 0;
    }

    // 奖励日志
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

    // 调试辅助：在 Scene 视图画出徘徊检测圈
    void OnDrawGizmos()
    {
        if (robot != null)
        {
            Gizmos.color = _isLoitering ? Color.red : Color.green;
            Gizmos.DrawWireSphere(_loiterAnchorPos, loiterRadius);
        }
    }
}