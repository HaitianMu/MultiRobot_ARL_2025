using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Barracuda;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;
using static BuildingGeneratiion;

public partial class HumanControl : MonoBehaviour
{


    [Header("Panic Settings")]
    public float damagePerSecond = 0;

    // 缓存一些不需要每帧计算的常量
    private const float MAX_PANIC_TIME = 0.5f; // 恐慌更新间隔（秒），避免每帧更新节省性能
    private float _panicUpdateTimer = 0f;

    // 优化：恐慌计算参数
    private const float CO_WEIGHT = 0.4f;
    private const float TEMP_WEIGHT = 0.3f;
    private const float VIS_WEIGHT = 0.3f;

    private const float MIN_CO = 65f;       // ppm
    private const float MAX_CO = 650f;
    private const float MIN_TEMP = 20f;     // °C
    private const float MAX_TEMP = 820f;
    private const float MIN_VIS = 0.5f;     // m
    private const float MAX_VIS = 30.5f;

    void UpdatePanicLevelAndHealth()
    {
        // 1. 性能优化：限制恐慌逻辑的更新频率 (例如每0.5秒更新一次，而不是每帧)
        // 掉血逻辑通常需要每帧计算（保持平滑），但恐慌值可以降低频率

        // -----------------------------------------------------
        // 核心优化：直接调用 EnvControl 的 O(1) 接口获取环境数据
        // -----------------------------------------------------
        // 这里的 GetEnvironmentData 返回 struct，没有任何 GC
        var envData = myEnv.GetEnvironmentData(transform.position, myEnv.runtime);
       
        // 获取解析好的数据
        float smokeDensity = envData.SmokeDensity; // 对应 mol/mol * 10^6
        float tempC = envData.ValueC;              // 对应 CSV 第4列 (Temperature/C)
        float visibilityM = envData.ValueM;        // 对应 CSV 第5列 (Visibility/m)
        //print($"环境数据为：烟雾浓度: {smokeDensity} mol/mol, 温度: {tempC} °C, 能见度: {visibilityM} m");
        // -----------------------------------------------------
        // 2. 健康值计算 (每帧执行)
        // -----------------------------------------------------
        // 计算伤害速率
        CalculateHealthDecay(smokeDensity, tempC);

        // 应用伤害
        if (damagePerSecond > 0)
        {
            this.health -= damagePerSecond * Time.fixedDeltaTime;
        }

        // -----------------------------------------------------
        // 3. 恐慌值更新 (低频执行)
        // -----------------------------------------------------
        _panicUpdateTimer += Time.fixedDeltaTime;
        if (_panicUpdateTimer >= MAX_PANIC_TIME)
        {
            _panicUpdateTimer = 0f;

            if (UsePanic && !myEnv.useHumanAgent && stateTime > PanicChangeTime)
            {
                // 更新恐慌值
                float newPanicLevel = CalculatePanicValue(smokeDensity, tempC, visibilityM);
                this.panicLevel = Mathf.Clamp01(newPanicLevel);

                // 更新视野 (根据能见度)
                // 视野最小 5m, 最大 15m. 可见度数据(m) 通常很大，需要缩放
                // 你的原逻辑: (int)Visibility/3 + 10
                this.visionLimit = Mathf.Clamp((int)(visibilityM / 3f) + 10, 5, 50);

                stateTime = 0; // 重置状态计时
            }
        }
        if (myEnv.useHumanAgent && damagePerSecond > 0)
        {
            // 持续性的微量惩罚，让 Agent 意识到“站在这里会持续痛苦”
            // 这比死掉的时候扣大分更能引导 Agent 移动
 
        }
    }
    /// <summary>
    /// 计算恐慌值 (纯数学计算，无GC)
    /// </summary>
    private float CalculatePanicValue(float coPPM, float temp, float visibility)
    {
        // 归一化计算 (Mathf.InverseLerp 比手动除法更安全且自带Clamp)
        float coFactor = Mathf.InverseLerp(MIN_CO, MAX_CO, coPPM);
        float tempFactor = Mathf.InverseLerp(MIN_TEMP, MAX_TEMP, temp);
        // 能见度越低，恐慌越高，所以是 1 - factor
        float visFactor = 1f - Mathf.InverseLerp(MIN_VIS, MAX_VIS, visibility);

        return (coFactor * CO_WEIGHT) + (tempFactor * TEMP_WEIGHT) + (visFactor * VIS_WEIGHT);
    }

    /// <summary>
    /// 计算掉血速率
    /// </summary>
    private void CalculateHealthDecay(float coPPM, float temp)
    {
        const float BASE_DAMAGE = 0.8f;
        const float CO_DMG_MULT = 5.0f;
        const float TEMP_DMG_MULT = 3.0f;

        // 只有当环境参数超过安全阈值时才开始扣血
        // 这里假设 CSV 中的 mol/mol * 10^6 即为 CO PPM

        float coDanger = Mathf.InverseLerp(0f, MAX_CO, coPPM);
        float tempDanger = Mathf.InverseLerp(20f, MAX_TEMP, temp);

        // 如果环境很安全，不扣血或者只扣很少
            damagePerSecond = BASE_DAMAGE
                              + (coDanger * CO_DMG_MULT)
                              + (tempDanger * TEMP_DMG_MULT);
    }

    // 在 UpdateBehaviorModel 中优化
    private void UpdateBehaviorModel()
    {
        stateTime += Time.deltaTime;

        // 只有状态持续时间超过阈值，才允许重新评估状态
        if (stateTime < PanicChangeTime) return;

        int newState = CurrentState;
        if (panicLevel < 0.25f) newState = 0;
        else if (panicLevel > 0.35f && panicLevel < 0.65f) newState = 1;
        else if (panicLevel > 0.75f) newState = 2;

        if (newState != CurrentState)
        {
            CurrentState = newState;
            stateTime = 0; // 状态改变后重置计时，强制锁定一段时间
        }
    }

    private void MoveModel0()//正常移动逻辑,具有独立的思考，只会对机器人进行跟随
    {
        _myNavMeshAgent.speed =4f;
        switch (myBehaviourMode)
        {
            case "Follower":
                FollowerUpdate_Clam();
                break;
            case "Leader":
                LeaderUpdate_Clam();
                break;
        }

    }
    private void MoveModel1() // 恐慌度大于0.3，小于0.7，焦虑模式：人类会加快移动速度

        //开始出现从众行为，此外逃生速度会进行加快
    {
       // print(this.gameObject.name+"正在以模式1移动");
        _myNavMeshAgent.speed = 6f;
        switch (myBehaviourMode)
        {
            case "Follower":
                FollowerUpdate_HF();
                break;
            case "Leader":
                LeaderUpdate_HF();
                break;
        }
    }

    private void MoveModel2() // 恐慌模式：高速移动 + 目标抖动
    {
        // 1. 基础设定：恐慌时切断社交连接（自私模式），提升速度
        if (myLeader != null)
        {
            if (myLeader.CompareTag("Robot"))
                myLeader.GetComponent<RobotControl>().myDirectFollowers.Remove(this);
            else
                myLeader.GetComponent<HumanControl>().myDirectFollowers.Remove(this);
            myLeader = null;
        }

        // 确保切换回 Leader 模式以便自己寻路
        myBehaviourMode = "Leader";
        _myNavMeshAgent.speed = 6f; // 高速

        // 2. 目标搜索逻辑：虽然恐慌，但仍有逃生本能
        // 如果当前没有目标，或者目标已经不可达，尝试搜索
        if (myTargetDoor == null || !myTargetDoor.activeInHierarchy)
        {
            TryFindPanicTarget(); // <--- 新增函数：恐慌时的搜索逻辑
        }

        // 3. 移动逻辑：
        if (myTargetDoor != null)
        {
            // --- 策略 A: 有目标，但是走得歪歪扭扭 ---

            // 只有每隔一段时间才更新一次目的地，避免每一帧都在抖动导致原地鬼畜
            if (Time.time - lastPanicUpdateTime > panicMoveInterval)
            {
                MoveToTargetWithJitter(); // <--- 新增函数：带抖动的导航
                lastPanicUpdateTime = Time.time;
            }

            // 检查是否到达目标附近，如果到了就清除目标，准备过门或寻找下一个
            if (Vector3.Distance(transform.position, myDestination) < 1.5f) // 恐慌模式判定范围大一点
            {
                myTargetDoor = null;
            }
        }
        else
        {
            // --- 策略 B: 实在找不到出口，才进行原本的随机游走 (作为保底) ---
            if (Time.time - lastPanicUpdateTime > panicMoveInterval ||
                Vector3.Distance(transform.position, myDestination) < 0.5f)
            {
                UpdatePanicDestination(); // 你原来的随机游走逻辑
                lastPanicUpdateTime = Time.time;
            }
        }

        // 4. 对抗逻辑 (推开阻挡的机器人)
        HandleRobotInteraction();
    }
    /// <summary>
    /// 恐慌时的搜索逻辑：只找出口和门，忽略社交
    /// </summary>
    private void TryFindPanicTarget()
    {
        // 复用你写好的扫描逻辑，但只关心 Exit 和 Door
        var scanResult = GetCandidate_Clam(new List<string> { "Exit", "Door" }, 360, 20);
        List<GameObject> candidates = scanResult.Item1;
        List<Vector3> unknownDirs = scanResult.Item2;

        if (candidates.Count > 0)
        {
            // 优先找 Exit
            GameObject exit = candidates.FirstOrDefault(c => c.CompareTag("Exit"));
            if (exit != null)
            {
                myTargetDoor = exit;
            }
            else
            {
                // 没出口就找门，排除掉刚才进来的那个门（防止来回鬼畜），除非只有那一个门
                var validDoors = candidates.Where(d => d != lastDoorWentThrough).ToList();
                if (validDoors.Count > 0)
                {
                    myTargetDoor = validDoors[Random.Range(0, validDoors.Count)]; // 恐慌时随机选一个门冲
                }
                else
                {
                    // 只有回头路，被迫回头
                    myTargetDoor = candidates[0];
                }
            }
        }
        // 如果连门都看不到，myTargetDoor 保持为 null，会在主逻辑里触发 UpdatePanicDestination (随机游走)
    }

    /// <summary>
    /// 带误差的导航
    /// </summary>
    private void MoveToTargetWithJitter()
    {
        if (myTargetDoor == null) return;

        // 1. 获取准确位置
        Vector3 accuratePos = GetCrossDoorDestination(myTargetDoor);

        // 2. 制造恐慌误差 (Jitter)
        // 在目标点周围 2.5米 范围内随机偏移
        // 这样会导致 Agent 可能会撞到门框，或者冲过头，模拟"慌不择路"
        Vector3 jitter = Random.insideUnitSphere * 2.5f;
        jitter.y = 0; // 保持在地面

        // 3. 设定目的地
        myDestination = accuratePos + jitter;

        // 4. 确保这个歪歪扭扭的点是在 NavMesh 上的 (防止抖到墙里去卡死)
        NavMeshHit hit;
        if (NavMesh.SamplePosition(myDestination, out hit, 3.0f, NavMesh.AllAreas))
        {
            myDestination = hit.position;
            _myNavMeshAgent.SetDestination(myDestination);
        }
        else
        {
            // 如果偏移点无效，就还是走准确点吧，总比卡死好
            _myNavMeshAgent.SetDestination(accuratePos);
        }
    }
    //人类的混乱移动！！！！！！！！！！！！！！！！！！！！！！！
    private float panicMoveInterval;  // 目标更新间隔

    private float panicMoveRadius = 5f;     // 随机移动半径
    private float lastPanicUpdateTime;      // 上次更新时间
    private Vector3 currentPanicDirection;  // 当前移动方向
    private Vector3 lastSafePosition;   //安全位置，用于随机目的地不可达时，原路返回
    private void UpdatePanicDestination()
    {
        lastSafePosition = transform.position;
        // 方向持续性：70%概率保持当前方向偏移
        if (currentPanicDirection != Vector3.zero && Random.value < 0.7f)
        {
            float angleOffset = Random.Range(-30f, 30f);
            Vector3 newDirection = Quaternion.Euler(0, angleOffset, 0) * currentPanicDirection;
            myDestination = transform.position + newDirection.normalized * panicMoveRadius;
        }
        else // 30%概率全新随机方向
        {
            currentPanicDirection = new Vector3(
                Random.Range(-1f, 1f),
                0,
                Random.Range(-1f, 1f)
            ).normalized;
            myDestination = transform.position + currentPanicDirection * panicMoveRadius;
        }

        // 导航验证，如果不可达，就在附近随机一个地点
        if (!ValidateNavDestination(myDestination))
        {
            myDestination = GetFallbackPosition();
        }

        _myNavMeshAgent.SetDestination(myDestination);
    }


    private bool ValidateNavDestination(Vector3 target)
    {
        NavMeshPath path = new NavMeshPath();
        if (_myNavMeshAgent.CalculatePath(target, path))
        {
            return path.status == NavMeshPathStatus.PathComplete;
        }
        return false;
    }
   private Vector3 GetFallbackPosition()
    {
        // 方案1：尝试返回上一个安全位置
    if (lastSafePosition != Vector3.zero &&
        Vector3.Distance(transform.position, lastSafePosition) > 2f)
        {
            if (ValidateNavDestination(lastSafePosition))
                return lastSafePosition;
        }
        // 最终方案：当前位置周围随机点
        return transform.position + Random.insideUnitSphere * 1f;
    }
    //人类的混乱移动！！！！！！！！！！！！！！！！！！！！！！！


    //与机器人的对抗行为：！！！！！！！！！！
    public void HandleRobotInteraction()
    {
        // 获取 3m 内的机器人
        var candidates = GetCandidate(new List<string> { "Robot" }, 360, 3).Item1;

        if (candidates.Count > 0)
        {
            GameObject leader = candidates[0];

            // 核心修复：只有当计时器为 0 时才初始化（代表刚进入视野或刚开始接触）
            if (robotDetectTime <= 0)
            {
                robotDetectTime = Time.time;
            }

            float contactTime = Time.time - robotDetectTime;

            // 第一阶段：抗拒期 (例如 2秒)
            if (contactTime < 2.0f)
            {
                // 推开行为：向机器人反方向施加力
                Vector3 pushDir = (transform.position - leader.transform.position).normalized;
                _myNavMeshAgent.velocity = pushDir * 3f; // 增加速度，表现出明显的“逃离感”
            }
            // 第二阶段：屈服期 (开始跟随)
            else
            {
                // 只有在恐慌状态下才需要切换回焦虑态来“屈服”
                if (CurrentState == 2)
                {
                    CurrentState = 1;
                    myBehaviourMode = "Follower";
                    myLeader = leader;

                    // 给 AI 发送成功引导奖励
                    if (myEnv.useHumanAgent) myHumanBrain.AddReward(0.05f);
                }
            }
        }
        else
        {
            // 视野内没机器人，重置计时器
            robotDetectTime = 0;
        }
    }

}


