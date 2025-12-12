using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.AI;
using System.Linq;
using Unity.Barracuda;
using TMPro;
using System.IO.Abstractions;
using UnityEditor;
using System.IO;
using System;
using System.Numerics;
using Vector3 = UnityEngine.Vector3;

public class RobotBrain : Agent
{
    //环境变量
    public EnvControl myEnv;
    // 机器人本体
    public GameObject robot;

    // 机器人的移动目的地
    public Vector3 robotDestinationCache;

    // 机器人的NavMeshAgent组件
    [HideInInspector] public NavMeshAgent robotNavMeshAgent;

    // 机器人的刚体组件
    [HideInInspector] public Rigidbody robotRigidbody;

    // 机器人的脚本类
    [HideInInspector] public RobotControl robotInfo;
 
    // 当前所在楼层
    public int currentFloor=1;

    // 机器人卡死计数器,会被火焰卡死
    public int stuckCounter;

    // 人类剩余血量观测值
    public float _humanHealthObservation;
    //人类总体血量衰减速率
    public float TotalHealthDecayRate = 0; 
    // 人类平均血量衰减速率
    public  float HumanHealthDecayRate = 0;

    //当前楼层人数
    public int floor_human;

    public bool RobotIsInitialized = false;//用来控制初始化函数先执行

    public Vector3 robotPosition;

   public  Vector3 targetPosition;//机器人的决策目的地，但不一定朝向它移动，要经过一系列判断，比如是否可达？



    //观测值填充占位使用
    // 在EnvController中定义常量
    public const int MAX_HUMANS = 10; //最大人类数量， 与课程学习上限一致
    public const int MAX_ROOMS = 20; // 与建筑设计上限一致
    public const float INVALID_MARKER = -2f; // 超出[-1,1]范围的无效标记

    public float SignalcostTime;//记录单次运行花费的时间
    public float TotalcostTime;

    
    private void FixedUpdate()
    {
        if (RobotIsInitialized)//机器人已经初始化完成
        {
            SignalcostTime += Time.deltaTime;
            //先计算场景中的人数
            floor_human = 0;
            //计算场景中有几个人类，对场景运行没有影响
            foreach (HumanControl human in myEnv.personList)//统计当前楼层的人数
            {
                if (human.isActiveAndEnabled)
                {
                    floor_human++;
                }
            }
            //先计算场景中的人数

            if (!IsOnNavMesh(transform.position)) { //不在导航地图上//向右移动一个单位
            
                this.transform.position +=Vector3.right;
            }

                //print("当前楼层人数为:" + floor_human);
                robotPosition.y = 0.5f;

            // 如果不是训练模式，机器人就自己进行移动，暂时不使用训练收集的数据

            //Debug.Log("每一帧更新");
            // 每个时间步都要求决策,决策后才会收集信息以及执行操作,后续函数执行的前置条件

           // AddReward(-0.01f*floor_human);//人类停留在火灾场景的惩罚
            //LogReward("人类停留在火灾场景的惩罚", -0.01f*floor_human);
            
           
            //根据逻辑运行时，通过侦察该层的人数，来决定是否继续移动！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！
            if (myEnv.isTraining is false)
                {
                print("非训练模式");
                    //print("当前楼层人数为:" + floor_human);
                    int lonelyHumanLeaderCounter = (from human in myEnv.personList
                                                    let humanPosition = human.transform.position - new Vector3(0, 0.5f, 0)
                                                    where human.isActiveAndEnabled && Mathf.Abs(humanPosition.y - robotPosition.y) < 0.5f
                                                    select human).Count(human => human.myBehaviourMode is "Leader" && human.transform.position.z > 0);

                    //print("孤独人类领导者的数量为："+lonelyHumanLeaderCounter);

                    if (lonelyHumanLeaderCounter <= 10)
                    {//人类领导者数量（lonelyHumanLeaderCounter）小于等于4，并且机器人跟随者数量（robotInfo.robotFollowerCounter）等于0时，条件1为真;人类领导者数量（lonelyHumanLeaderCounter）等于0时，条件2为真
                        robot.GetComponent<RobotControl>().isRunning = true;//机器人开始工作,人类开始跟随机器人
                        GMoveAgent();
                        return;
                    }
                }
                //根据逻辑运行时，通过侦察该层的人数，来决定是否继续移动！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！
           
            //训练开启，且机器人现在的位置与记忆中的位置距离小于1
            if (myEnv.isTraining) {
                //print("训练模式，请求决策");

                //print("场景中当前人数为："+myEnv.currentFloorhuman);

                foreach (HumanControl human in myEnv.personList)//统计当前存活人类的血量衰减速度
                {
                    if (human.isActiveAndEnabled)
                    {

                            TotalHealthDecayRate+= human.damagePerSecond;
                        HumanHealthDecayRate = TotalHealthDecayRate / floor_human;
                    }
                  TotalHealthDecayRate = 0;
                }

                AddReward(-myEnv.currentFloorhuman*HumanHealthDecayRate);//计算场景中人类的健康衰减速率的平均值
                LogReward("人类停留场景惩罚", -myEnv.currentFloorhuman * HumanHealthDecayRate);
                _humanHealthObservation -= HumanHealthDecayRate/10;

                if (Vector3.Distance(robotPosition, robotDestinationCache) < 2) { //距离目的2m（考虑到y轴高度），认为已经到达目的地。已经到达目的地之后，再次前往下一个目的地
                   // print("发送请求");
                    RequestDecision();
                }
            }
        }
    }//定帧更新
    public override void OnEpisodeBegin()
    {
        print("机器人一个新的回合开始了");

    }

    public Vector3  NormalizedPos(Vector3 pos)
    {
        float maxX = myEnv.complexityControl.buildingGeneration.totalWidth;
        float maxZ = myEnv.complexityControl.buildingGeneration.totalHeight;
        // 归一化到 [-1, 1] 范围
        float normalizedX = (pos.x / maxX) * 2 - 1;
        float normalizedZ = (pos.z / maxZ) * 2 - 1;

        return new Vector3(normalizedX, 0.5f, normalizedZ);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        //建议先固定观测值数量并确保严格归一化到[0, 1]范围，这是PPO算法稳定训练的前提条件
        //在RequestDecision函数执行后，会先执行该函数来收集环境观测值
        //观测值需要添加：
        //每一个人类的位置，来学习人类的移动逻辑
        //每一个机器人的位置，来学习其他机器人的移动逻辑，但目前只有一个机器人
        //总区域的面积，房间的数量/位置，每一个门的位置， 来学习建筑的生成逻辑
        // 计算环境边界（与Human观测保持一致）

        if (myEnv.useRobotBrain is false)
            return;

        //Debug.Log("CollectObservations called."); 
        if (myEnv == null || myEnv.useRobotBrain is false)
        {
            Debug.Log("myEnv is null or useRobotBrain is false.");
            return;
        }
        //机器人位置观测值：2 * n（其中 n 为机器人的数量）,人类位置观测值：60,,房间位置观测值：30,出口位置观测值：2,火源位置观测值：6
        // 添加 Agent 观测值





        // 使用场景对角线长度归一化，确保所有坐标∈[0,1]
        float sceneDiagonal = Mathf.Sqrt(
            Mathf.Pow(myEnv.complexityControl.buildingGeneration.totalWidth, 2) +
            Mathf.Pow(myEnv.complexityControl.buildingGeneration.totalHeight, 2)
        );
        //  print("场景对角线为长度为："+sceneDiagonal);
        sensor.AddObservation(floor_human); //   场景中人类数量，1个
        sensor.AddObservation(_humanHealthObservation); //场景中人类健康衰减速率，1个
        // 归一化 Agent 位置 ，           2个
        foreach (RobotBrain agent in myEnv.RobotBrainList)
        {
            sensor.AddObservation(NormalizedPos(agent.robot.transform.position).x);
            sensor.AddObservation(NormalizedPos(agent.robot.transform.position).z);
             //Debug.Log("机器人的位置为" + normalizedPos);
        }

        // 归一化 Human 位置，人类最多10个            40个,添加人类位置人类状态和人类状态持续时间的观测值

        // 固定观测维度为 MAX_HUMANS * 2
        for (int i = 0; i < MAX_HUMANS; i++)
        {
            if (i < myEnv.personList.Count)
            {
                // 填充实际人类位置
                HumanControl human = myEnv.personList[i];
                sensor.AddObservation(NormalizedPos(human.transform.position).x);
                sensor.AddObservation(NormalizedPos(human.transform.position).z);
                sensor.AddObservation(human.CurrentState);
                sensor.AddObservation(human.stateTime);
                sensor.AddObservation(human.health);
            }
            else
            {
                // 填充占位值（推荐使用无效坐标）
                sensor.AddObservation(-1f); // x
                sensor.AddObservation(-1f); // z
                sensor.AddObservation(-1f); // z
            }
        }
         
        // 添加房间位置（相对Agent） 最大20个房间，  40//
        int maxRooms = 20;
        for (int i = 0; i < maxRooms; i++)
        {
            if (i < myEnv.cachedRoomPositions.Count)
            {
                Vector3 roomPos = myEnv.cachedRoomPositions[i];
                sensor.AddObservation(NormalizedPos(roomPos).x);
                sensor.AddObservation(NormalizedPos(roomPos).z);
            }
            else
            {
                // 填充占位值（推荐使用无效坐标）
                sensor.AddObservation(-1f); // x
                sensor.AddObservation(-1f); // z
            }
        }

        //添加出口位置   只有1个出口         39+[24,45]=[63,84]     2个
        sensor.AddObservation(NormalizedPos(myEnv.Exits[0].transform.position).x );
        sensor.AddObservation(NormalizedPos(myEnv.Exits[0].transform.position).z);
        //Debug.Log("出口的位置为" + (myEnv.Exits[0].transform.position) / Mathf.Max(myEnv.complexityControl.buildingGeneration.totalWidth, myEnv.complexityControl.buildingGeneration.totalHeight));

        //添加火源位置，目前火源只设置了三个      6个
        for (int i = 0;i<3;i++)
        {
            Vector3 firePos = myEnv.FirePosition[i];
            {
                // 位置归一化（相对于环境中心）
                sensor.AddObservation(NormalizedPos(firePos).x);
                sensor.AddObservation(NormalizedPos(firePos).z);
                // Debug.Log("火源的位置为" + normalizedPos);
            }
        }
    }
    public override void OnActionReceived(ActionBuffers actions)
    {
        if (myEnv.useRobotBrain is false)
            return;
        //print("接收到了动作");
        MoveAgent(actions);  // 移动Agent
    }



   
    /// <summary>
    /// 利用模型决策结果移动机器人本体
    /// </summary>
    /// <param name="actions"></param>
    private void MoveAgent(ActionBuffers actions)
    {
        ActionSegment<float> continuousActions = actions.ContinuousActions;
        Vector3 robotPosition = robot.transform.position;
        robotPosition.y = 0.5f + 4 * (currentFloor - 1);
        Vector3 positionExit = myEnv.Exits[0].gameObject.transform.position;

        //将目标位置映射在整个房间区域内部
       // Debug.Log("区域的宽度为"+myEnv.complexityControl.buildingGeneration.totalWidth);
        //Debug.Log("区域的高度为" + myEnv.complexityControl.buildingGeneration.totalHeight);
       // Debug.Log($"Actions: [{continuousActions[0]}, {continuousActions[1]}]");
        float targetX = Mathf.Clamp(continuousActions[0], -1, 1) * (myEnv.complexityControl.buildingGeneration.totalWidth / 2f) + (myEnv.complexityControl.buildingGeneration.totalWidth / 2f);
        float targetZ = Mathf.Clamp(continuousActions[1], -1, 1) * (myEnv.complexityControl.buildingGeneration.totalHeight / 2f) + (myEnv.complexityControl.buildingGeneration.totalHeight / 2f);
        /*// 输出上下界到控制面板
        Debug.Log($"targetX 上下界: [0, {myEnv.complexityControl.buildingGeneration.totalWidth}]");
        Debug.Log($"targetZ 上下界: [0, {myEnv.complexityControl.buildingGeneration.totalHeight}]");*/

        targetPosition = new(targetX, 0.5f, targetZ);
        //print("目的地是："+targetPosition);


        if (!IsReachable(targetPosition))//无效目的地，返回
        {
            //print("决策目的地不可达");//到这里是个死胡同了！！！！！！！！！9.26，15:00

            GMoveAgent();
            stuckCounter++;//目的地无效
            return;
        }

        if(Vector3.Distance(targetPosition, myEnv.Exits[0].gameObject.transform.position) < 10 && this.robot.GetComponent<RobotControl>().robotFollowerCounter > 0)
        {
            targetPosition = myEnv.Exits[0].gameObject.transform.position;
        }
        //定位到了出口附近一定范围内，且跟随者数量大于0，将目的地设置为出口


        //Debug.Log("这一帧的目的地是："+targetPosition);
        if (myEnv.isTraining is false)
        {
            if (robotInfo.robotFollowerCounter > 0)
            {

                if (currentFloor == 1 && robotDestinationCache == positionExit && Vector3.Distance(targetPosition, positionExit) < 40 && robotInfo.robotCommand is "LightOn")
                    return;
            }
            else if (robotInfo.robotFollowerCounter <= 0)
            {

                if (targetPosition == positionExit)
                    return;
            }
        }


        if (IsReachable(targetPosition))
        {
            // 如果目标可达则前往目标，不可达则继续上一步动作
            stuckCounter = 0;
            //
            if (robotDestinationCache == targetPosition)
            {
               // print("无效目的地，使用贪心移动算法1");
                GMoveAgent();
            }
            else
            {
                //print("决策目的地可达，并朝向决策目的地进行移动");
                robotDestinationCache = targetPosition;
                robotNavMeshAgent.SetDestination(robotDestinationCache);
            }
        }
        }

        bool IsReachable(Vector3 targetPosition)//不可达是false
    {
        NavMeshPath path = new NavMeshPath();
        if (robot.GetComponent<NavMeshAgent>().CalculatePath(targetPosition, path))
        {
            if (path.status == NavMeshPathStatus.PathComplete)
            {
                return true; // 完整路径，可以到达
            }
        }
        return false; // 路径不完整，可能被墙或者其他障碍挡住了
    }


    private void GMoveAgent()
    {
        //print("机器人的跟随者数量为：" + robotInfo.myDirectFollowers.Count);
        if (robotInfo.robotFollowerCounter > 0)//如果当前机器人当前跟随者大于0个，前往出口
        {
          //  print("跟随人类数量大于0");
            //随机一个出口，将人送到出口
            //print("2机器人检测到的出口数量为："+myEnv.Exits.Count);
            targetPosition = targetPosition = myEnv.Exits[0].gameObject.transform.position + new Vector3(1.5f, 0, 0);//往右边去一点，省的堵门
        }
        else
        {
            //print("没有人类跟随，寻找距离最近的人类");
            //找到离机器人最近的人类，并朝其进行移动
            float minDist = int.MaxValue;
            //float minDist = 18f;
            foreach (HumanControl human in myEnv.personList)
            {
                Vector3 humanPosition = human.transform.position - new Vector3(0, 0.5f, 0);
                if (human.isActiveAndEnabled is false
                    /*|| Mathf.Abs(humanPosition.y - robotPosition.y) > 1 || humanPosition.x < -20 || humanPosition.z > 20*/)
                    continue;
                if (Vector3.Distance(humanPosition, robotPosition) < minDist)
                {
                    minDist = Vector3.Distance(humanPosition, robotPosition);
                    targetPosition = humanPosition + human.transform.forward ;
                }
            }

            robotDestinationCache = targetPosition;
            robotNavMeshAgent.SetDestination(robotDestinationCache);
            return;
        }

        if (targetPosition != Vector3.zero)
        {
            robotDestinationCache = targetPosition;
            robotNavMeshAgent.SetDestination(robotDestinationCache);
            return;
        }

    }//贪心算法移动机器人

    private Vector3 GetCrossDoorDestination(GameObject targetDoor)//去到门前的位置，该函数是给机器人使用，放置发生拥堵
    {
        Vector3 myPosition = transform.position;

        if (targetDoor.CompareTag("Door") || targetDoor.CompareTag("Exit"))
        {
            string doorDirection = targetDoor.GetComponent<DoorControl>().doorDirection;
            Vector3 doorPosition = targetDoor.transform.position + new Vector3(0, -1.5f, 0);
            switch (doorDirection)
            {
                case "Horizontal": //水平
                    if (myPosition.z < doorPosition.z)
                        return doorPosition + new Vector3(0, 0, -1);
                    return doorPosition - new Vector3(0, 0, -1);
                case "Vertical"://垂直
                    if (myPosition.x < doorPosition.x)
                        return doorPosition + new Vector3(-1, 0, 0);
                    return doorPosition - new Vector3(-1, 0, 0);
                default:
                    return myPosition;
            }
        }
        else
        {
            return myPosition;
        }
    }

    private bool IsOnNavMesh(Vector3 targetPosition)
    {
        return NavMesh.SamplePosition(targetPosition, out NavMeshHit _, 0.1f, 1);
    }


    // 奖励统计可视化
    private Dictionary<string, float> rewardLog = new Dictionary<string, float>();

    [System.Serializable]
    private class RewardData
    {
        public string timestamp;
        public Dictionary<string, float> rewards;
    }
    public void LogReward(string type, float value)
    {
        rewardLog.TryGetValue(type, out float current);
        rewardLog[type] = current + value;
    }

    void OnDestroy()
    {


        // 定义保存路径（使用persistentDataPath）
        string directoryPath = Path.Combine(Application.persistentDataPath, "10.15");
        string filePath = Path.Combine(directoryPath, $"Robot_Reward_log_{DateTime.Now:yyyyMMdd_HHmmss}.json");

        try
        {
            // 确保目录存在
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            string result="";



            foreach(var kv in rewardLog)
         {
                Debug.Log($"{kv.Key}: {kv.Value}");
                result += $"{kv.Key}: {kv.Value}\n";
            }
            // 写入文件
            File.WriteAllText(filePath, result);

            // 日志输出
            Debug.Log($"奖励数据已保存到: {filePath}\n{result}");
        }
        catch (Exception e)
        {
            Debug.LogError($"保存奖励数据失败: {e.Message}");
        }
    }


    public override void Heuristic(in ActionBuffers actionsOut) // 这里的代码没什么用
    {
        if (myEnv.useRobotBrain is false)
            return;

        if (currentFloor != 3)
            return;

        Vector3 robotPosition = robot.transform.position;
        robotPosition.y = 0.5f + 4 * (currentFloor - 1);
        ActionSegment<int> discreteActions = actionsOut.DiscreteActions;
        ActionSegment<float> continuousActions = actionsOut.ContinuousActions;

        if (Input.GetKey(KeyCode.UpArrow))
        {
            discreteActions[0] = 1;
            Vector3 targetPosition = robotPosition + new Vector3(0, 0, 1);
            continuousActions[0] = targetPosition.x / 18.0f;
            continuousActions[1] = targetPosition.z / 18.0f;
        }
        else if (Input.GetKey(KeyCode.DownArrow))
        {
            discreteActions[0] = 1;
            Vector3 targetPosition = robotPosition + new Vector3(0, 0, -1);
            continuousActions[0] = targetPosition.x / 18.0f;
            continuousActions[1] = targetPosition.z / 18.0f;
        }
        else if (Input.GetKey(KeyCode.LeftArrow))
        {
            discreteActions[0] = 1;
            Vector3 targetPosition = robotPosition + new Vector3(-1, 0, 0);
            continuousActions[0] = targetPosition.x / 18.0f;
            continuousActions[1] = targetPosition.z / 18.0f;
        }
        else if (Input.GetKey(KeyCode.RightArrow))
        {
            discreteActions[0] = 1;
            Vector3 targetPosition = robotPosition + new Vector3(1, 0, 0);
            continuousActions[0] = targetPosition.x / 18.0f;
            continuousActions[1] = targetPosition.z / 18.0f;
        }
        else if (Input.GetKey(KeyCode.A))
        {
            discreteActions[0] = 2;
            continuousActions[0] = robotPosition.x / 18.0f;
            continuousActions[1] = robotPosition.z / 18.0f;
        }
        else if (Input.GetKey(KeyCode.B))
        {
            discreteActions[0] = 3;
            continuousActions[0] = robotPosition.x / 18.0f;
            continuousActions[1] = robotPosition.z / 18.0f;
        }
    }

}
