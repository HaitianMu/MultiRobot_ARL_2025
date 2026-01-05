using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using static BuildingGeneratiion;
using Random = UnityEngine.Random;

public partial class HumanControl : MonoBehaviour
{
    [Header("References")]
    public EnvControl myEnv;
    public HumanBrain myHumanBrain;

    [Header("Navigation")]
    public Transform targetPosition;
    public int visionLimit = 10;
    private NavMeshAgent _myNavMeshAgent;
    public Queue<GameObject> _doorMemoryQueue;
    public GameObject myTargetDoor = null;
    public GameObject lastDoorWentThrough;
    public Vector3 myDestination;
    public bool isReturnFromLastDoor;
    public String myBehaviourMode;

    [Header("Social")]
    public int myFollowerCounter;
    public List<GameObject> RbtList;
    public GameObject myLeader;
    private const float FOLLOWER_DISTANCE_THRESHOLD = 0.5f;
    public GameObject myTopLevelLeader; // 补充缺失的变量定义
    private bool isFounded;

    [Header("Health & Status")]
    private float optimalHealthRange = 40f;
    public float health;
    private float DelayRate = 0.01f;

    // 恐慌相关
    [SerializeField] float exitDistance;
    [SerializeField] float startDistanceToExit;
    [SerializeField] float startdesiredSpeed;
    [SerializeField] float MaxSpeed;
    public float panicLevel;

    // 核心状态变量
    public int CurrentState;  // 0:理性, 1:焦虑, 2:恐慌

    // 辅助计时变量
    float LastDistanceToExit;
    public float PanicChangeTime;
    public float stateTime;
    public bool UsePanic; // 补充缺失的变量定义
    public float dazingCountDown; // 补充缺失
    public float robotDetectTime; // 补充缺失

    public void Start()
    {
        isFounded = false;
        PanicChangeTime = 5;
        stateTime = 4;

        myLeader = null;
        myBehaviourMode = "Leader";
        _myNavMeshAgent = GetComponent<NavMeshAgent>();
        myDestination = new Vector3();
        _doorMemoryQueue = new Queue<GameObject>();
        myTargetDoor = null;
        lastDoorWentThrough = null;
        health = 100.0f;
    }

    private void FixedUpdate()
    {
        stateTime += Time.deltaTime;

        // 1. 全局状态更新 (无论是否使用AI，都要算恐慌值和扣血)
        if (myEnv.usePanic)
        {
            UpdatePanicLevelAndHealth();
        }

        // 2. 行为控制逻辑
        if (!myEnv.useHumanAgent)
        {
            // === 传统算法模式 ===
            // 依据 panicLevel 计算 CurrentState
            UpdateBehaviorModel();
        }
        else
        {
            // === AI 强化学习模式 ===
            // 关键修改：
            // 1. 删除了 myHumanBrain.RequestDecision() -> 这里的请求权交给 Brain 的计时器
            // 2. 删除了 AddReward -> 奖励权交给 Brain 的 OnActionReceived
            // 3. 删除了 stateTime 判断 -> 只要 Brain 更新了 State，身体立刻执行

            // 额外规则：即使是AI控制，如果血量过低，强制进入焦虑/恐慌状态 (可选)
            // 这属于"规则覆盖"(Rule Override)，可以保留作为最后防线
            if (this.health < 30 && CurrentState == 0)
            {
                // CurrentState = 1; // 如果你想让AI完全接管，就把这行注释掉
            }

            // 执行移动逻辑
            switch (CurrentState)
            {
                case 0: MoveModel0(); break; // 理性
                case 1: MoveModel1(); break; // 焦虑
                case 2: MoveModel2(); break; // 恐慌
            }
        }

        // 3. 死亡判定
        if (health <= 0)
        {
            HandleDeath();
        }
    }

    // 将死亡逻辑封装，保持 FixedUpdate 干净
    private void HandleDeath()
    {
        if (myLeader != null)
        {
            if (myLeader.CompareTag("Robot"))
            {
                myLeader.GetComponent<RobotControl>().myDirectFollowers.Remove(this);
            }
            else
            {
                myLeader.GetComponent<HumanControl>().myDirectFollowers.Remove(this);
            }
        }

        if (myEnv.useRobotBrain && myEnv.RobotBrainList.Count > 0)
        {
            // 机器人团队惩罚：作为保护者，行人死亡是严重的失败
            // 建议：惩罚值应与行人逃生奖励对等
            myEnv.AddRobotGroupReward(-1.0f);
        }

        if (myEnv.useHumanAgent)
        {
            // 人类端：除了基础死亡分，还可以根据距离出口的远近额外扣分（惩罚没跑多远就死了）
            // float distPenalty = Mathf.Clamp01(exitDistance / 100f) * -0.5f;
            myHumanBrain.SetReward(-1.0f);
            myHumanBrain.EndEpisode();
        }

        gameObject.SetActive(false);

        if (myEnv.useHumanAgent)
        {
            myHumanBrain.AddReward(-1.0f); // 给予明确的死亡惩罚
            myHumanBrain.EndEpisode();     // 死亡即结束回合
        }

        gameObject.SetActive(false);
    }

    private List<Vector3> GetVision(int visionWidth, int visionDiff)
    {
        List<Vector3> myVisions = new();
        int visionBias = visionWidth / (2 * visionDiff);
        for (int visionIndex = -visionBias; visionIndex <= visionBias; visionIndex++)
        {
            Vector3 vision = Quaternion.AngleAxis(visionDiff * visionIndex, Vector3.up) * transform.forward;
            myVisions.Add(vision);
        }
        return myVisions;
    }

    private void OnTriggerEnter(Collider trigger)
    {
        GameObject triggerObject = trigger.gameObject;

        switch (trigger.transform.tag)
        {
            case "Door":
                // --- 探索奖励逻辑 ---
                // 如果这个门不在最近的记忆队列里，说明是“新区域探索”
                if (!_doorMemoryQueue.Contains(triggerObject))
                {
                    if (myEnv.useHumanAgent)
                    {
                        myHumanBrain.AddReward(0.05f); // 给予微量探索奖励，鼓励通过门
                                                       // myHumanBrain.LogReward("探索新区域", 0.05f);
                    }

                    _doorMemoryQueue.Enqueue(triggerObject);
                    if (_doorMemoryQueue.Count > 3) _doorMemoryQueue.Dequeue();
                }
                lastDoorWentThrough = triggerObject;
                break;

            case "Exit":
                HandleEscape();
                break;

            case "Fire":
                // --- 火源避障惩罚 ---
                if (myEnv.useHumanAgent)
                {
                    // 瞬时惩罚，让Agent对“火”这个标签产生恐惧，而不只是对掉血恐惧
                    myHumanBrain.AddReward(-0.01f);
                }
                break;

            case "Robot":
                // 可选：如果行人主动靠近机器人，可以给一点点奖励，鼓励受引导
                if (myEnv.useHumanAgent && CurrentState != 0)
                {
                    myHumanBrain.AddReward(0.01f);
                }
                break;
        }
    }

    private void HandleEscape()
    {
        if (myLeader != null)
        {
            if (myLeader.CompareTag("Robot"))
                myLeader.GetComponent<RobotControl>().myDirectFollowers.Remove(this);
            else
                myLeader.GetComponent<HumanControl>().myDirectFollowers.Remove(this);
            myLeader = null;
        }

        this.gameObject.SetActive(false);
        myEnv.sumhealth += health;
        myEnv.EscapeHuman++;

        // === 机器人奖励修改 (修复群体奖励为0的问题) ===
        if (myEnv.useRobotBrain)
        {
            // 原代码：myEnv.RobotBrainList[0].AddReward(1.0f); 
            // 缺点：只奖励第0个机器人，且不计入 Group Reward

            // --- 修改后 ---
            // 优点：所有机器人平分这份荣誉，促进合作
            myEnv.AddRobotGroupReward(1.0f);
        }

        // === 人类奖励 ===
        if (myEnv.useHumanAgent)
        {
            // 逃生奖励：基于剩余血量给予
            // 归一化奖励通常在 0 ~ 1 或 -1 ~ 1 之间，原来的 (health)*10 = 1000 太大了
            float normalizedHealth = health / 100f;
            myHumanBrain.AddReward(1.0f + normalizedHealth); // 基础奖励1 + 血量加成
            myHumanBrain.EndEpisode(); // 成功逃生，回合结束
        }

        // myEnv.LogReward("逃生", 1);
    }
}