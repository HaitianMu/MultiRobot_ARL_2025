using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using Unity.MLAgents;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public partial class EnvControl : MonoBehaviour
{
    public float runtime = 0;
    //人类列表
    public List<HumanControl> personList = new();
    //人类大脑列表

    public List<HumanBrain> HumanBrainList = new();
    //机器人列表
    public List<RobotControl> RobotList = new();
    // 机器人大脑列表
    public List<RobotBrain> RobotBrainList = new();

    public List<FireControl>FireList = new();

    //环境中的出口
    public List<GameObject> Exits=new();
    //存储房间的位置信息
    public List<Vector3> cachedRoomPositions;
 
    /*生成场景和导航时用到的组件 !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!*/
    public ComplexityControl complexityControl;//生成环境用到的组件
    public NavMeshSurface surface;//生成导航的组件
    public GameObject HumanPrefab;//生成人类用到的组件
    public GameObject RobotPrefab;//添加机器人用到的组件
    public GameObject BrainPrefab;//机器人大脑预制体
    public GameObject FirePrefab; //火焰预制体
   

    //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
    public GameObject RobotParent;//机器人的父物体，减少性能消耗
    public GameObject FireParent;//机器人的父物体，减少性能消耗

    public GameObject RobotBrainParent;//机器人大脑的父物体，减少性能消耗，但目前这个用不到

    public GameObject HumanBrainParent;//人类大脑父物体，

    public GameObject humanParent;//机器人的父物体，减少性能消耗

    public int EpisodeNum=0;

    public float TotalSize;//区域总大小
    public int RoomNum;//房间数目、

    public bool isTest;//与训练完全一致，多了轮次的限制
    // 是否在训练
    public bool isTraining;
    // 是否使用机器人
    public bool useRobot;
    //是否使用火焰
    public bool useFire;
    //是否使用火焰智能体
    public bool useHumanAgent;
    public bool usePanic; //是否启用人类恐慌

    /*展示Demo使用，用于场景重置!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!*/
    public int currentFloorhuman=0;
    public int FireStep;//计数器，用来生成火焰
    public int StepCount;//计数器，用来步数过多时重置场景
    public int MaxStep;//最大步数
    public int layoutNum;

   //引入火焰机制4.20 ！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！
    //手动添加火焰时的火焰位置缓存
    public List<Vector3> FirePosition;
    //添加的火焰数量
    public int FireNum;


    //用于奖励计算
    public float sumhealth;//用于记录人类逃生时的整体健康值
    public float startTime;//记录每个回合开始的时间
    //
    public int EnpisodeNum=0;//用于记录回合数，以用来进行阶段性训练
    public int HumanNum = 1;//每一回合添加的人类数量，逐步增加，不能一开始就放10个


    public int EscapeHuman;//成功逃脱的人类

    public float EnpisodeTime;//记录每一回合的时间

    public int humanBrainNum=10;//添加人类大脑的数量

    public CSVRead CsvRead;//读取火焰数据
    private void Start()
    {
        CsvRead = new CSVRead();
        CsvRead.TestFireDataLoading();//加载火焰数据
        EnpisodeNum = 0;
        HumanNum = 10;
        EnpisodeTime = 0;
     
        //预览模式，机器人使用贪心算法，人类使用自由移动，火焰人为控制生成地点!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        if (!isTraining||isTraining)
        {
            Debug.Log("Env的start函数");

/*            if (currentFloorhuman == 0)//第二次场景开始条件，检测场景中的人类数量
            {*/
                CleanTheScene();
                string filename = "layout";//layout,layout_900_15
            string[] name = { "layout1", "layout2", "layout3", "layout4",
                "apartment", "family_house", "clinic", "elementary_school", "hospital_er","tech_office", "shopping_mall" };
                //string layoutname = name[layoutNum];

                string layoutname = "layout_9";//office_floor_1
                                               //print("读取的布局名称为：" + layoutname);

              //!!!!!!!!!!!!!!!!!!!!!!!!场景实现
                complexityControl.BeginGenerationJsonLoad(filename, layoutname); //在这里指定加载数据的文件
                AddExits();//添加出口，以便于后续机器人导航使用
                surface.BuildNavMesh();//生成导航
                                       //!!!!!!!!!!!!!!!!!!!!!!!!场景实现

                AddPerson(10);

               if (useHumanAgent) {
                AddHumanBrain(humanBrainNum);
                }

                currentFloorhuman = 0;
                //计算场景中有几个人类，对场景运行没有影响
                foreach (HumanControl human in personList)//统计当前楼层的人数
                {
                    if (human.isActiveAndEnabled)
                    {
                        currentFloorhuman++;
                    }
                    //Debug.Log(currentFloorhuman);
                }

            if (useRobot)
            {
                AddRobot();//添加机器人
                AddRobotBrain();//添加机器人大脑
            }
                /*  !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!*/
                FireStep = 0;//训练决策数目
                MaxStep = 12000;

                /*  !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!*/
                //手动添加的火焰位置
                AddFirePosition();
                FireNum = 0;
            }
        /*}*/
        //预览模式，机器人使用贪心算法，人类使用自由移动，火焰人为控制生成地点!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
    }

    private void FixedUpdate()
    {
        runtime += Time.deltaTime;
        EnpisodeTime += Time.deltaTime;
        //预览模式，机器人使用贪心算法，人类使用自由移动，火焰人为控制生成地点!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        if (!isTraining)
        {
            if (useFire)
            {
                FireStep++;
                if (FireStep % 100 == 0)
                {
                    if (FireNum < FirePosition.Count)
                    {
                        AddFire(FirePosition[FireNum]);
                        FireNum++;
                        FireStep++;
                    }
                    else { FireStep++; }
                }
                if (FireStep % 100 == 0)//每过一定帧更新导航地图
                {
                    var surface = FindObjectOfType<NavMeshSurface>();
                    if (surface != null)
                    {
                        surface.UpdateNavMesh(surface.navMeshData);
                    }
                    FireStep++;
                }
            }
        }


        else if (isTraining)
        {

            //先计算场景中的人数
            currentFloorhuman = 0;
            //计算场景中有几个人类，对场景运行没有影响
            foreach (HumanControl human in personList)//统计当前楼层的人数
            {
                if (human.isActiveAndEnabled)
                {
                    currentFloorhuman++;
                }
            }
            //先计算场景中的人数

            if (useRobot)
            {
                if (currentFloorhuman == 0 || runtime > 90.0f )  //当场景中的人类数量为0时或训练时长大于90s 重新构建一轮测试场景
                {
                    EnpisodeNum++;
                    if (isTest && EnpisodeNum > 100)
                    {
                        CleanTheScene();
                        return;
                    }
                    print(EnpisodeNum + "个回合");
         

                    this.LogReward("总人数", 10);
                    this.LogReward("回合数", 1);
                    this.LogReward("运行花费的总时间", EnpisodeTime);
                    EnpisodeTime = 0;

                    foreach (HumanControl human in personList)//统计当前楼层的人数
                    {
                        if (human.isActiveAndEnabled)
                        {
                            RobotBrainList[0].LogReward("未逃生但存活人类的总生命值", human.health);
                        }
                    }

                    RobotBrainList[0].LogReward("总人数", 10);//每成功逃脱一个人，额外给予100点奖励

                    //RobotBrainList[0].AddReward(EscapeHuman * 50);//每成功逃脱一个人，额外给予100点奖励
                    // RobotBrainList[0].LogReward("回合最终奖励", EscapeHuman * 50);//每成功逃脱一个人，额外给予100点奖励



                    foreach (HumanBrain humanBrain in HumanBrainList)
                    {
                        humanBrain.EpisodeInterrupted();
                    }

                    RobotBrainList[0].EndEpisode();

                    print("当前运行时间是：" + runtime + "回合终止");
                    RobotBrainList[0].LogReward("场景总运行时长", runtime);
                    runtime = 0;//将场景运行时间归零
                    string filename = "layout";
                    //string layoutname = name[layoutNum];

                    string layoutname = "layout_9";
                    //print("读取的布局名称为：" + layoutname);
                    CleanTheScene();
                    //UnityEngine.Random.Range(8, 15); // 划分的房间数量
                    complexityControl.BeginGenerationJsonLoad(filename, layoutname); //在这里指定加载数据的文件
                    RecordRoomPosition(complexityControl.buildingGeneration.roomList);//记录房间的位置，用于后续的归一化训练

                    AddExits();//添加出口，以便于后续机器人导航使用


                    surface.BuildNavMesh();//生成导航

                    AddPerson(HumanNum);
                    if (useHumanAgent)
                    {
                        AddHumanBrain(humanBrainNum);
                    }

                    foreach (HumanControl human in personList)//初始化统计当前楼层的人数，
                                                              //后续通过控制currentFloorhuman的增减，来决定是否开始新的回合，避免每一帧进行查找，影响性能
                    {
                        if (human.isActiveAndEnabled)
                        {
                            currentFloorhuman++;
                        }
                        //Debug.Log(currentFloorhuman);
                    }
                    AddRobot();//添加机器人
                    AddRobotBrain();//添加机器人大脑



                    AddFirePosition();
                    FireNum = 0;
                    FireStep = 0;
                    StepCount = 0;
                    MaxStep = 10000;
                    startTime = Time.time;
                    EscapeHuman = 0;
                    HumanNum = 10;
                    useFire = true;
                    usePanic = true;
                    //myEnv.cachedDoorPositions=myEnv.GetAllDoorPositions();//添加门的位置信息
                    //myEnv.cachedRoomPositions = myEnv.GetAllRoomPositions();//添加房间的位置信息
                }
            }
            else if (!useRobot) //不添加机器人的场景重构
            {
                if (currentFloorhuman == 0)
                {
                    this.LogReward("总人数", 10);
                    this.LogReward("回合数", 1);
                    this.LogReward("运行花费的总时间", EnpisodeTime);
                    EnpisodeTime = 0;

                    string filename = "layout";
                    string layoutname = "layout_9";
                    CleanTheScene();
                    //UnityEngine.Random.Range(8, 15); // 划分的房间数量
                    complexityControl.BeginGenerationJsonLoad(filename, layoutname); //在这里指定加载数据的文件
                    RecordRoomPosition(complexityControl.buildingGeneration.roomList);//记录房间的位置，用于后续的归一化
                    AddExits();//添加出口，以便于后续机器人导航使用
                    surface.BuildNavMesh();//生成导航
                    AddPerson(HumanNum);
                    if (useHumanAgent)
                    {
                        AddHumanBrain(humanBrainNum);
                    }      //

                    foreach (HumanControl human in personList)//初始化统计当前楼层的人数，
                                                              //后续通过控制currentFloorhuman的增减，来决定是否开始新的回合，避免每一帧进行查找，影响性能
                    {
                        if (human.isActiveAndEnabled)
                        {
                            currentFloorhuman++;
                        }
                        //Debug.Log(currentFloorhuman);
                    }
                    FireNum = 0;
                    FireStep = 0;
                    StepCount = 0;
                    MaxStep = 10000;
                    startTime = Time.time;
                    EscapeHuman = 0;
                    HumanNum = 10;
                    useFire = true;
                    usePanic = true;
                }
            }


            // 添加火焰！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！
            if (useFire)
            {
                FireStep++;
                if (FireStep % 100 == 0)
                {
                    if (FireNum < FirePosition.Count)
                    {
                        AddFire(FirePosition[FireNum]);
                        FireNum++;
                        FireStep++;
                    }
                    else { FireStep++; }
                }
                if (FireStep % 100 == 0)//每过一定帧更新导航地图
                {
                    surface.UpdateNavMesh(surface.navMeshData);
                    FireStep++;
                }
            }
            // ！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！

            // 步数超时重置环境！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！
            StepCount++;
        }
    }
    public void CleanTheScene()
    {
        // print("执行CleanScene函数");
        // 1. 先停止所有火焰的传播
        foreach (var fire in FindObjectsOfType<FireControl>())
        {
            fire.StopAllCoroutines();
        }
        // 2. 执行常规清理
        ResetAgentandClearList();

        // 3. 强制垃圾回收（可选）
        System.GC.Collect();
        Resources.UnloadUnusedAssets();
    }
    /// <summary>
    /// 重置所有机器人控制智能体
    /// </summary>
}
