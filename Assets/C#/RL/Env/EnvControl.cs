using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using Unity.MLAgents;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public partial class EnvControl : MonoBehaviour
{
    [Header("Runtime Stats")]
    public float runtime = 0;
    public int EpisodeNum = 0; // 总回合数
    public float EnpisodeTime; // 当前回合耗时
    public int StepCount;      // 当前回合物理步数
    public int FireStep;       // 火焰生成计数器

    [Header("Agents Lists")]
    public List<HumanControl> personList = new();
    public List<HumanBrain> HumanBrainList = new();
    public List<RobotControl> RobotList = new();
    public List<RobotBrain> RobotBrainList = new();
    public List<FireControl> FireList = new();

    [Header("Environment Objects")]
    public List<GameObject> Exits = new();
    public List<Vector3> cachedRoomPositions;
    public List<Vector3> FirePosition;

    [Header("Components & Prefabs")]
    public ComplexityControl complexityControl;
    public NavMeshSurface surface;
    public GameObject HumanPrefab;
    public GameObject RobotPrefab;
    public GameObject BrainPrefab;
    public GameObject FirePrefab;

    [Header("Parent Objects")]
    public GameObject RobotParent;
    public GameObject FireParent;
    public GameObject RobotBrainParent;
    public GameObject HumanBrainParent;
    public GameObject humanParent;

    [Header("Settings")]
    public float TotalSize;
    public int RoomNum;
    public bool isTest;
    public bool isTraining;
    public bool useRobotBrain;
    public bool useFire;
    public bool useHumanAgent;
    public bool usePanic;
    public bool isMultiARL;

    [Header("Demo Settings")]
    public int currentFloorhuman = 0;
    public int MaxStep;
    public int layoutNum;
    public int FireNum;
    public int HumanNum = 1;
    public int EscapeHuman;
    public int humanBrainNum = 10;
    public float sumhealth;
    public float startTime;
    public int robotCount = 3;

    public CSVRead CsvRead;

    private void Start()
    {
        CsvRead = new CSVRead();
        CsvRead.TestFireDataLoading();//加载火焰数据
        EpisodeNum = 0;
        HumanNum = 50;
        EnpisodeTime = 0;

        // 无论是训练还是非训练，Start时的逻辑基本一致，这里进行简化
        Debug.Log("Env的start函数");

        // 初始化场景
        StartNewEpisode(true);
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime; // 缓存一下，微小优化
        runtime += dt;
        EnpisodeTime += dt;

        UpdateSmokeFrame(); // 更新当前时间，便于读取烟雾数据

        // ----------- 1. 统计当前幸存人类数量 -----------
        // 性能优化：尽量避免在Update中使用Linq，这里保留手动循环但写得更紧凑
        currentFloorhuman = 0;
        for (int i = 0; i < personList.Count; i++)
        {
            if (personList[i].isActiveAndEnabled)
            {
                currentFloorhuman++;
            }
        }

        // ----------- 2. 训练/测试 逻辑循环 -----------
        if (isTraining)
        {
            bool timeOut = runtime > 90.0f; // 90秒超时
            bool allDead = currentFloorhuman == 0;

            // 触发重置的条件
            if (allDead || timeOut)
            {
                HandleEpisodeReset(timeOut);
                return; // 重置后直接返回，等待下一帧
            }
        }

        // ----------- 3. 火焰生成逻辑 -----------
        // 逻辑保持不变：训练和非训练模式下都执行这部分，只要 useFire 为 true
        if (useFire)
        {
            HandleFireLogic();
        }

        StepCount++;
    }

    /// <summary>
    /// 处理回合重置的主要入口
    /// </summary>
    private void HandleEpisodeReset(bool isTimeout)
    {
        EpisodeNum++;

        // 如果是测试模式且超过100回合，停止测试
        if (isTest && EpisodeNum > 100)
        {
            CleanTheScene();
            return;
        }

        print(EpisodeNum + "个回合");

        // --- 记录奖励 (仅在有机器人的情况下记录给 RobotBrain) ---
        if (useRobotBrain && RobotBrainList.Count > 0)
        {
            LogRewardStats();
            // 结束当前Agent的回合
            RobotBrainList[0].EndEpisode();
            // 对人类大脑进行中断
            foreach (var hb in HumanBrainList) hb.EpisodeInterrupted();
        }
        else if (!useRobotBrain)
        {
            // 没有机器人时仅记录简单的Env奖励
            this.LogReward("总人数", 10);
            this.LogReward("回合数", 1);
            this.LogReward("运行花费的总时间", EnpisodeTime);
        }

        if (useRobotBrain)
        {
            print("当前运行时间是：" + runtime + "回合终止");
            if (RobotBrainList.Count > 0)
                RobotBrainList[0].LogReward("场景总运行时长", runtime);
        }

        // 重置时间
        runtime = 0;
        EnpisodeTime = 0;

        // --- 开始新回合 ---
        StartNewEpisode(false);
    }

    /// <summary>
    /// 统一的开启新回合/重置环境方法
    /// </summary>
    /// <param name="isFirstStart">是否是程序启动时的第一次加载</param>
    private void StartNewEpisode(bool isFirstStart)
    {
        CleanTheScene(); // 清理旧物体

        string filename = "layout";
        string layoutname;

        if (!isMultiARL)
        {
            layoutname = "layout_9";
        }
        else
        {
            layoutname = "layout_50";
        }

        // 生成地图
        complexityControl.BeginGenerationJsonLoad(filename, layoutname);

        // 记录房间位置
        if (complexityControl.buildingGeneration != null)
        {
            RecordRoomPosition(complexityControl.buildingGeneration.roomList);
        }

        AddExits();

        // 生成导航网格
        if (surface != null)
        {
            surface.BuildNavMesh();
        }

        // 添加单位
        // 如果是重置阶段，HumanNum应该重置为10 (参考了您原代码中的逻辑：Start时 AddPerson(10), 重置时 AddPerson(HumanNum) 但随后 HumanNum=10)
        // 这里直接统一逻辑
        int countToSpawn = isFirstStart ? 50 : HumanNum;
        AddPerson(countToSpawn);

        if (useHumanAgent)
        {
            AddHumanBrain(humanBrainNum);
        }

        // 重新统计人数 (避免下一帧 FixedUpdate 之前的空窗期)
        currentFloorhuman = countToSpawn;

       
            AddRobot();
            AddRobotBrain();

        // 重置参数
        AddFirePosition();
        FireNum = 0;
        FireStep = 0; // 原代码 Start里置0，重置里也置0
        StepCount = 0;
        MaxStep = isFirstStart ? 12000 : 10000; // 原代码 Start里是12000，重置里是10000，保留此逻辑
        startTime = Time.time;
        EscapeHuman = 0;
        HumanNum = 50;
    }

    /// <summary>
    /// 记录奖励统计数据的辅助方法
    /// </summary>
    private void LogRewardStats()
    {
        this.LogReward("总人数", 10);
        this.LogReward("回合数", 1);
        this.LogReward("运行花费的总时间", EnpisodeTime);

        // 统计未逃生者的剩余生命值
        foreach (HumanControl human in personList)
        {
            if (human.isActiveAndEnabled)
            {
                RobotBrainList[0].LogReward("未逃生但存活人类的总生命值", human.health);
            }
        }
        RobotBrainList[0].LogReward("总人数", 10);
    }

    /// <summary>
    /// 处理火焰生成与导航网格更新
    /// </summary>
    private void HandleFireLogic()
    {
        FireStep++;

        // 每100步尝试生成火焰
        if (FireStep % 100 == 0)
        {
            if (FireNum < FirePosition.Count)
            {
                AddFire(FirePosition[FireNum]);
                FireNum++;
                FireStep++; // 增加一步，避免在同一帧重复进入逻辑
            }
            else
            {
                FireStep++;
            }
        }

        // 每100步更新导航网格 (注意：这里的模运算需要确保逻辑正确，因为上面改变了FireStep)
        // 优化建议：UpdateNavMesh 比较耗时，确保 surface 不为空再调用
        if (FireStep % 100 == 0)
        {
            if (surface != null)
            {
                // 性能优化：直接使用成员变量 surface，移除 FindObjectOfType
                surface.UpdateNavMesh(surface.navMeshData);
            }
            FireStep++;
        }
    }

    public void CleanTheScene()
    {
        // 1. 停止所有火焰的传播
        // 性能优化：直接遍历 FireList，而不是 FindObjectsOfType (全场景查找)
        // 假设 FireControl 在销毁前没有从 FireList 移除，或者 FireList 是此时准确的引用
        if (FireList != null)
        {
            for (int i = FireList.Count - 1; i >= 0; i--)
            {
                if (FireList[i] != null)
                {
                    FireList[i].StopAllCoroutines();
                }
            }
        }

        // 如果 FireList 维护不准，退而求其次可以用 FireParent
        // 但绝对不要用 FindObjectsOfType<FireControl>()

        // 2. 执行常规清理 (假设此方法在partial类的另一部分定义)
        ResetAgentandClearList();

        // 3. 性能优化：移除 System.GC.Collect() 和 Resources.UnloadUnusedAssets()
        // 原因：频繁调用GC会导致严重的CPU尖峰（掉帧），ML-Agents训练时这会显著降低吞吐量。
        // Unity 的内存管理机制会自动处理，除非您有非常大的纹理在每一轮被加载且不再使用。
    }

    // 假设这些方法在 partial 类的另一部分中定义，为了编译通过，这里不作修改
    // private void AddExits() { ... }
    // private void RecordRoomPosition(...) { ... }
    // private void UpdateSmokeFrame() { ... }
    // private void LogReward(...) { ... }
    // ... 其他 AddXxx 方法
}