using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using static BuildingGeneratiion;
using Random = UnityEngine.Random;

public partial class HumanControl: MonoBehaviour
{
    public EnvControl myEnv;
    public HumanBrain myHumanBrain;

    public Transform targetPosition; // 目标位置
    public int visionLimit = 10; // 射线检测距离
    private NavMeshAgent _myNavMeshAgent; // 导航代理组件
    public Queue<GameObject> _doorMemoryQueue;//用于记录人看到的门
    public GameObject myTargetDoor = null;  // 当前计划前往的门                              
    public GameObject lastDoorWentThrough;// 上一扇经过的门
    public Vector3 myDestination;     // 移动的目的地 
    public bool isReturnFromLastDoor;  // 是否从死胡同返回
    public String myBehaviourMode; //该人类的行为模式

    public int myFollowerCounter;  //跟随者的数量

    public List<GameObject> RbtList;  //发现的机器人列表
    public GameObject myLeader;//领导者目前只能是机器人 1.19

    private const float FOLLOWER_DISTANCE_THRESHOLD = 0.5f; //切换状态的最小距离

    private bool isFounded;

    private float optimalHealthRange = 40f; // 目标生命值,希望人类在该生命值时逃生
    //奖励相关参数
    public float health;//人类血量
    private float DelayRate = 0.01f;//人类血量衰减速率

    //人类恐慌状态相关参数
    [SerializeField] float exitDistance;//距离出口的距离
    [SerializeField] float startDistanceToExit;//场景开始时距离出口的距离

    [SerializeField] float startdesiredSpeed;//人类冷静状态下的期望速度
    [SerializeField] float MaxSpeed;         //人类恐慌状态下的期望速度

    public float panicLevel;  //恐慌等级
    public int CurrentState;    //人类的行为状态，
                                //0是理性模式：跟随机器人或自主导航
                                //1是焦虑模式：随机路径偏移
                                //2是恐慌模式：完全随机移动
     //用来记录距离出口的距离
    float LastDistanceToExit;
    public float PanicChangeTime;
    public float stateTime;//用来记录人类状态的持续时间
    public void Start()
    {
        isFounded = false;//初始是没有被机器人发现过的
        PanicChangeTime = 5;//三秒切换一次状态
        stateTime = 4;

        myLeader = null;
        myBehaviourMode = "Leader";
        _myNavMeshAgent = GetComponent<NavMeshAgent>();
        myDestination = new Vector3();
        _doorMemoryQueue = new Queue<GameObject>();
        myTargetDoor = null;
        lastDoorWentThrough = null;
        health = 100.0f;
        myTopLevelLeader = gameObject;
        dazingCountDown = Random.Range(2, 8);
        //恐慌值计算的参数!!!!
        UsePanic =true;
        CurrentState = 0;  //初始状态设置为理性模式
        //恐慌移动!!!!!!!
        // 每个个体初始化不同的延迟偏移量（0 - 2秒）
         robotDetectTime = 0;


    }
    private void FixedUpdate()
    {
        stateTime += Time.deltaTime;
        if (myEnv.usePanic)//找到机器人后就把这个禁用了
        {
            //print("拉拉拉，更新恐慌等级");
            UpdatePanicLevelAndHealth();    //无论是否使用人类智能体，更新人类的恐慌等级和健康值，主要进行火焰数据的读取工作
        }


        if (!myEnv.useHumanAgent)
        {  //这边是依据场景数据计算得到的panicLevel进行移动，每一帧都进行panicLevel的计算和状态的改变，
           //然后在Founction_ UpdateBehaviorModel()中，依据panicLevel计算得到currentState并进行相应状态的移动。但panicLevel的计算只有每5s能计算一次，也就是状态最多5s转变一次
           //每个人刚开始都是独立的领导者，但是随着程序的进行，
           //当看到机器人时，人类会进行跟随
           //print("人类不使用大脑，依据当前环境来决定自己的行为状态");
            UpdateBehaviorModel(); //更新行为模式
            print("不使用人类智能体");
        }
        else
        {//这边则依据人类大脑提供的CurrentState进行移动，也是每一帧进行状态的改变。


            // print("使用人类大脑决定自己移动状态");
            // CurrentState = myHumanBrain.HumanState;
            if (myEnv.useHumanAgent)
            {
                myHumanBrain.RequestDecision();

                //请求决策网络支援
                if (stateTime >PanicChangeTime)
                {
                    CurrentState = myHumanBrain.HumanState;
                    stateTime = 0;
         
                    if (this.health < 30)
                    {
                        CurrentState = 1;
                    }
                }
            }
            switch (CurrentState)
            {
                case 0: MoveModel0(); break;
                case 1: MoveModel1(); break;
                case 2: MoveModel2(); break;
            }
        }


        //在这里修改人类的生命值,人类生命值的变动方式也要修改！！！9.3  已删除//10.5
        if(health>0) {
            if (myEnv.useHumanAgent)
            {
                float healthDeviation = Mathf.Abs(health - optimalHealthRange);
                if (healthDeviation < 10f) // 生命值在30-50之间
                {
                    myHumanBrain.AddReward(0.3f); // 接近目标，给予奖励
                    myHumanBrain.LogReward("生命值接近40，给予大量奖励", 0.3f); // 接近目标，给予奖励
                }
                else if (health > 60f) // 生命值太高，逃生不够"刺激"
                {
                    myHumanBrain.AddReward(-0.1f); // 生命值过高
                    myHumanBrain.LogReward("生命值过高,给予少量奖励", -0.1f);
                }
                else if (health < 30f) // 生命值太低，太危险
                {
                    myHumanBrain.AddReward(-0.5f); // 生命值过低
                    myHumanBrain.LogReward("生命值过低，大量惩罚", -0.5f);
                }
            }
        }

        else if(health <= 0)
        {
            if (myLeader is not null)
            {
                if (myLeader.tag == "Robot")//领导者是机器人
                {
                    myLeader.GetComponent<RobotControl>().myDirectFollowers.Remove(gameObject.GetComponent<HumanControl>());
                }
                else//领导者是人类
                {
                    myLeader.GetComponent<HumanControl>().myDirectFollowers.Remove(gameObject.GetComponent<HumanControl>());
                }
            }

            Debug.Log("人类死亡");
            if (myEnv.useRobot)
            {
               myEnv.RobotBrainList[0].AddReward(-300f);
               myEnv.RobotBrainList[0].LogReward("人类死亡对机器人的惩罚", -300);
            }
            if (myEnv.useHumanAgent)
            {
                myHumanBrain.AddReward(-100f);
                myHumanBrain.LogReward("人类死亡对自己的惩罚",-100f);
            }
            gameObject.SetActive(false);
        }
    }

    private List<Vector3> GetVision(int visionWidth, int visionDiff)//生成人类视线，本质是一个向量数组
    {
        List<Vector3> myVisions = new();

        int visionBias = visionWidth / (2 * visionDiff);
        for (int visionIndex = -visionBias; visionIndex <= visionBias; visionIndex++)
        {
            Vector3 vision = Quaternion.AngleAxis(visionDiff * visionIndex, Vector3.up) * transform.forward;
            // 将生成的视线方向向量添加到列表中
            myVisions.Add(vision);
            //Debug.DrawRay(transform.position, vision , Color.green);
            // 调试用的代码，用于在场景中绘制视线方向（可注释掉）
        }
        return myVisions;
    }

    
    private void OnTriggerEnter(Collider trigger)
    {
        // Debug.Log("碰撞发生，碰撞体的标签是：" + trigger.transform.tag);
        GameObject triggerObject = trigger.gameObject;
        isReturnFromLastDoor = triggerObject == lastDoorWentThrough;
        switch (trigger.transform.tag)
        {
            case "Door":
                lastDoorWentThrough = triggerObject;
                //print("上一扇经过的门是："+ triggerObject.name);
                if (_doorMemoryQueue.Count>0&&!_doorMemoryQueue.Contains(triggerObject))
                    _doorMemoryQueue.Enqueue(triggerObject);
              // print(triggerObject.name + "已经被加入记忆");
                if (_doorMemoryQueue.Count > 3)
                    _doorMemoryQueue.Dequeue();
                break;

            case "Exit":

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
                // print("我成功逃离了");
                /*  myEnv.personList.Remove(this);*/
                this.gameObject.SetActive(false);
                myEnv.sumhealth += health;

                if (myEnv.useRobot)
                {
                    myEnv.RobotBrainList[0].AddReward((health) * 10);//单个人类逃生奖励,但人类有可能自己导航到出口，可能会影响训练结果，所以不能太大
                    myEnv.RobotBrainList[0].LogReward("单个人类逃生奖励", (health)*10);

                    //!!!!!!!!!!!!!!!!!逃生率计算
                    myEnv.RobotBrainList[0].LogReward("逃生人数", 1);
                    //！！！！！人类逃生时的生命值计算
                    myEnv.RobotBrainList[0].LogReward("逃生人类的总生命值",health);
                }
                if (myEnv.useHumanAgent)
                {

                    float Exitreward = health <= 40 ? 3f * health : 2f * (100 - health);
                    myHumanBrain.AddReward(Exitreward);//"人类逃脱奖励"
                    myHumanBrain.LogReward("人类逃脱对自己的奖励", Exitreward);
                    myHumanBrain.EndEpisode();
                }
               
                    myEnv.LogReward("逃生人数", 1);
                    myEnv.LogReward("人类逃生的生命值", (health));


                myEnv.EscapeHuman++;
                //在这里给予机器人奖励
                break;

            case "Fire":
                this.health -= 5;  //人类健康值-5
                if (myEnv.useHumanAgent)
                {
                    float FireReward = health > 40 ? -0.5f : -0.2f;
                    //myHumanBrain.AddReward(FireReward);//"人类碰火惩罚"
                    //myHumanBrain.LogReward("人类碰火惩罚", FireReward);
                }
                break;
        }
    }
}


