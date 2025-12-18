using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
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
                this.visionLimit = Mathf.Clamp((int)(visibilityM / 3f) + 10, 5, 15);

                stateTime = 0; // 重置状态计时
            }
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
        const float BASE_DAMAGE = 0.5f;
        const float CO_DMG_MULT = 2.0f;
        const float TEMP_DMG_MULT = 1.0f;

        // 只有当环境参数超过安全阈值时才开始扣血
        // 这里假设 CSV 中的 mol/mol * 10^6 即为 CO PPM

        float coDanger = Mathf.InverseLerp(0f, MAX_CO, coPPM);
        float tempDanger = Mathf.InverseLerp(20f, MAX_TEMP, temp);

        // 如果环境很安全，不扣血或者只扣很少
        if (coDanger <= 0.05f && tempDanger <= 0.05f)
        {
            damagePerSecond = 0f;
        }
        else
        {
            damagePerSecond = BASE_DAMAGE
                              + (coDanger * CO_DMG_MULT)
                              + (tempDanger * TEMP_DMG_MULT);
        }
    }

    private void UpdateBehaviorModel()
    {
        //人类状态转变：参考文献：[1]孙华锴.考虑恐慌情绪的人群疏散行为模型研究[D].中南大学,2022.DOI:10.27661/d.cnki.gzhnu.2022.000847.

        if (myEnv.usePanic)
        {
            if (panicLevel < 0.3) { CurrentState = 0; }
            else if (panicLevel <= 0.6 && panicLevel >= 0.3) { CurrentState = 1; }
            else if (panicLevel > 0.6 ) { CurrentState = 2; }

            switch (CurrentState)
            {
                case 0: MoveModel0(); break;
                case 1: MoveModel1(); break;
                case 2: robotDetectTime = 0; MoveModel2(); break;
            }

            // 在HumanControl的UpdateBehaviorModel()中追加：
            if (CurrentState == 2|| CurrentState ==1) // 恐慌状态或焦虑状态
            {
              /*  myEnv.RobotBrainList[0].AddReward(-0.02f * panicLevel);
                myEnv.RobotBrainList[0].LogReward("人类处于恐慌状态的惩罚", -0.02f * panicLevel);*/
            }
        }
        else MoveModel0();//如果不启用恐慌模式，默认使用正常模式

        /*if (myLeader != null)
        {
            //有领导者，就跟着领导者移动
            MoveModel0();
        }*/
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

    private void MoveModel2()
    {
        /*移动部分！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！*/
        // 条件1：达到间隔时间 或 条件2：接近目标点
        if (myLeader != null)
        {
            if (myLeader.tag == "Robot")//领导者是机器人
            {
                myLeader.GetComponent<RobotControl>().myDirectFollowers.Remove(gameObject.GetComponent<HumanControl>());
            }
            else//领导者是人类
            {
                myLeader.GetComponent<HumanControl>().myDirectFollowers.Remove(gameObject.GetComponent<HumanControl>());
            }
           
            myLeader = null;
        }
        myTargetDoor = null;
      
        myBehaviourMode = "Leader";
        
        _myNavMeshAgent.speed = 6;
        if (Time.time - lastPanicUpdateTime > panicMoveInterval ||
            Vector3.Distance(transform.position, myDestination) < 0.5f)
        {
            UpdatePanicDestination();
            lastPanicUpdateTime = Time.time;
        }
        /*移动部分！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！这一部分可以用来与机器人进行对抗，让人类自己决定自己的移动目的地？*/

        /*与机器人进行对抗的部分！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！*/
        HandleRobotInteraction();
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
        List<GameObject> leaderCandidates = GetCandidate(new List<string> { "Robot" }, 360, 30).Item1;
        
        if (leaderCandidates.Count > 0)//在视野里看到了机器人
        {
           // print("附近有个机器人，我好害怕");
            if (robotDetectTime == 0)
            {
                robotDetectTime = Time.time; // 记录首次检测时间,这里每一帧都会更新一次时间。你引导个P
              //  print("这个机器人是在"+robotDetectTime+"开始跟着我的");
            }
            GameObject leader = leaderCandidates[0];
            float robotDistance = Vector3.Distance(transform.position, leader.transform.position);
            // 对抗条件：恐慌度较高且机器人接近,只有恐慌度较高时才会进入该状态
          if ( robotDistance < 3f)
             {
                // 第一阶段：抗拒2s
                if (Time.time - robotDetectTime < 2  )
                {
                   // print("现在时间是"+"ta跟着我"+ (Time.time - robotDetectTime) + "s了，我要离他远一点");
                    // 推开行为
                    Vector3 pushDir = (transform.position - leader.transform.position).normalized;
                    
                    _myNavMeshAgent.velocity = pushDir * 2f;
                }
                // 第二阶段：屈服
                else
                {
                   // print("它好像是来救我的，我跟着他走吧");
                    robotDetectTime = 0;
                    CurrentState = 1;//将移动状态设置为1；
                    stateTime = 0;//将状态持续时长设置为0，便于下一次状态的转换   

                    if (myEnv.useRobotBrain)
                    {
                        myHumanBrain.AddReward(50);
                        myHumanBrain.LogReward("脱离恐慌状态的奖励", 50);
                    }
                }
            }
        }
    }
   
}


