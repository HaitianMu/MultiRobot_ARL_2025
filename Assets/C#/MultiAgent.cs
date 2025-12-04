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

public class MultiAgent : Agent
{
    //环境变量
    // 机器人本体
    public GameObject robot;

    // 机器人的移动目的地
    public Vector3 robotDestinationCache;

    // 机器人的NavMeshAgent组件
    [HideInInspector] public NavMeshAgent robotNavMeshAgent;

    // 机器人的刚体组件
    [HideInInspector] public Rigidbody robotRigidbody;

    // 机器人的脚本类
    [HideInInspector] public Robot robotInfo;

    // 用于计算临时路径的"锚"的预制体
    public GameObject navMeshAnchorPrefab;

    // 当前所在楼层
    public int currentFloor;

    //ADD 原始楼层
    public int originFloor;

    // 决策计数器
    public int decisionCountDown;

    // 最大决策数
    public int maxDecisionCountDown;

    // 机器人卡死计数器
    public int stuckCounter;

    // 机器人里程计数器
    public float mileageCounter;

    // 机器人位置记录器
    public Vector3 mileageRecorder;

    public bool isTrans = false;//？？？是否移动？
    //环境变量
    public Env myEnv;
    // 人类血量衰减速率
    private const float HumanHealthDecayRate = 0.01f;
    //当前楼层人数
    public int floor_human;
    int previousActiveHumanCount;
    private void FixedUpdate()
    {

        int currentFloorhuman = 0;

        foreach (Person human in myEnv.personList)//统计当前楼层的人数
        {
            if (human.isActiveAndEnabled)
            {
                currentFloorhuman++;
            }
        }
        floor_human = currentFloorhuman;

        // 获取机器人当前楼层的位置
        Vector3 robotPosition = robot.transform.position;
        robotPosition.y = 0.5f;

        // 如果不是训练模式，机器人就自己进行移动，暂时不使用训练收集的数据

        //Debug.Log("每一帧更新");

        /*   !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
            !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!*/
        // 每个时间步都要求决策,决策后才会收集信息以及执行操作,后续函数执行的前置条件
        RequestDecision();


        if (myEnv.isTraining is false)
        {
            //print("当前楼层人数为:" + floor_human);
            int lonelyHumanLeaderCounter = (from human in myEnv.personList
                                            let humanPosition = human.transform.position - new Vector3(0, 0.5f, 0)
                                            where human.isActiveAndEnabled && Mathf.Abs(humanPosition.y - robotPosition.y) < 0.5f
                                            select human).Count(human => human.myBehaviourMode is "Leader" && human.transform.position.z > 0);

            //print("孤独人类领导者的数量为："+lonelyHumanLeaderCounter);

            if (lonelyHumanLeaderCounter <= 5)
            {//人类领导者数量（lonelyHumanLeaderCounter）小于等于4，并且机器人跟随者数量（robotInfo.robotFollowerCounter）等于0时，条件1为真;人类领导者数量（lonelyHumanLeaderCounter）等于0时，条件2为真
                robot.GetComponent<Robot>().isRunning = true;//机器人开始工作,人类开始跟随机器人
                GMoveAgent();
                return;
            }
        }
    }//定帧更新

    /*private void Awake()*///awake函数执行在start函数之前。
    public override void Initialize()
    {
        Debug.Log("我是初始化函数，我执行了!");
        // 激活 robot,并将机器人设置为工作状态，这样人类才会进行跟随
        robot.SetActive(true);
        robot.GetComponent<Robot>().isRunning = true;
        // 设置初始位置
        robotDestinationCache = robot.transform.position;
        robotDestinationCache.y = 0.5f + 4 * (currentFloor - 1);

        // 初始化其他变量
        decisionCountDown = maxDecisionCountDown;
        stuckCounter = 0;
        mileageCounter = 0;
        mileageRecorder = new Vector3();
        originFloor = currentFloor;
        foreach (Person human in myEnv.personList)
        {
            if (human.isActiveAndEnabled)
            {
                previousActiveHumanCount++;
            }
        }
        // 输出初始化信息
        /* Debug.Log("Robot initialized at position: " + robot.transform.position);
         Debug.Log("myEnv: " + myEnv.gameObject.name);

         // 打印 personList 和 robot 信息
         foreach (Person human in myEnv.personList)
         {
             Debug.Log("Human: " + human.transform.name);
         }
         Debug.Log("Robot: " + robot.transform.name);
         Debug.Log("myEnv.useRobot " + myEnv.useRobot);//myEnv.isTraining
         Debug.Log("myEnv.isTraining " + myEnv.isTraining);*/
    }
    public override void CollectObservations(VectorSensor sensor)
    {
        //在RequestDecision函数执行后，会先执行该函数来收集环境观测值

        if (myEnv.useRobot is false)
            return;

        //Debug.Log("CollectObservations called.");
        if (myEnv == null || myEnv.useRobot is false)
        {
            Debug.Log("myEnv is null or useRobot is false.");
            return;
        }
        int num = 0;

        // 添加 Agent 观测值
        foreach (MultiAgent agent in myEnv.agentList)
        {
            sensor.AddObservation((agent.robot.transform.position - new Vector3(5, 0, 5)) / 5.0f);
            num++;
        }

        // 添加 Human 观测值
        foreach (Person human in myEnv.personList)
        {
            //Debug.Log("Human position: " + human.transform.position);
            sensor.AddObservation((human.transform.position - new Vector3(5, 0, 5)) / 5.0f);
            num++;
        }

       // Debug.Log("Total observations added: " + RoomNum);
    }
    public override void OnActionReceived(ActionBuffers actions)
    {
        if (myEnv.useRobot is false)
            return;
        if (isTrans)  // 判断过渡状态
            return;

        MoveAgent(actions);  // 移动Agent
    }
    /*public override void OnActionReceived(ActionBuffers actions)
    {
        if (myEnv.useRobot is false)
            return;
        if (isTrans)  // 判断过渡状态
            return;

        MoveAgent(actions);  // 移动Agent

        // 基于楼层人数的变化进行奖励
        if (floor_human == 0)
        {
            EndEpisode();  // 结束训练
            Debug.Log("当前楼层人数为0，结束此次训练");
            SetReward(10f);  // 完成任务奖励
            return;
        }

        // 假设任务是减少楼层人数
        // 每减少一个人可以给一个正奖励
        SetReward(floor_human * -0.1f);  // 每个剩余的人物减少奖励

        // 机器人状态惩罚：如果机器人处于无效状态，则给予惩罚
        if (isTrans)  // 如果是过渡状态，给予负奖励
        {
            SetReward(-1f);  // 避免过度停滞
        }

        // 惩罚不必要的移动：如果机器人做了没有意义的动作
        if (IsIrrelevantMove(actions))  // 你可以根据具体情况定义无效动作的判断
        {
            SetReward(-0.5f);  // 惩罚无意义的移动
        }

        // 你还可以加入更复杂的奖励，比如基于时间或者效率的奖励
        float timePenalty = Time.timeSinceLevelLoad * -0.05f;  // 基于时间的惩罚
        SetReward(timePenalty);
    } //奖励函数V1.0*/

    //奖励相关的函数、、、、、、、、、、、、、、、、、、
    private bool IsIrrelevantMove(ActionBuffers actions)
    {
        // 获取归一化的目标坐标
        float targetX = Mathf.Clamp(actions.ContinuousActions[0], -1, 1) * 5.0f + 5f;
        float targetZ = Mathf.Clamp(actions.ContinuousActions[1], -1, 1) * 5.0f + 5f;

        // 计算目标位置
        Vector3 targetPosition = new Vector3(targetX, transform.position.y, targetZ);

        //  如果目标位置与当前位置几乎相同，移动距离过小，认为是无效动作
        if (Vector3.Distance(transform.position, targetPosition) < 0.2f)
        {
            //Debug.Log("本动作是无效动作");
            return true; // 目标位置与当前位置几乎重合，无效动作
        }

        return false; // 如果没有无效动作，返回 false
    }
    //奖励相关的函数、、、、、、、、、、、、、、、、、、
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        if (myEnv.useRobot is false)
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


    /// <summary>
    /// 利用模型决策结果移动机器人本体
    /// </summary>
    /// <param name="actions"></param>
    private void MoveAgent(ActionBuffers actions)
    {
        ActionSegment<float> continuousActions = actions.ContinuousActions;

        Vector3 robotPosition = robot.transform.position;
        robotPosition.y = 0.5f + 4 * (currentFloor - 1);
        Vector3 positionExit = GetCrossDoorDestination(myEnv.Exits[0].gameObject); ;

        float targetX = Mathf.Clamp(continuousActions[0], -1, 1) * 5.0f+5f;
        float targetZ = Mathf.Clamp(continuousActions[1], -1, 1) * 5.0f+5f;
        
        Vector3 targetPosition = new(targetX, 0.5f, targetZ);
        //Debug.Log("机器人的目的地为：" +targetPosition );
        if (Vector3.Distance(targetPosition, positionExit) < 20 && currentFloor == 1)
            targetPosition = positionExit;

        if (myEnv.isTraining is false)
        {
            // if (currentFloor == 1 && robotInfo.robotFollowerCounter > 4 && IsPathOnCurrentFloor(GetWayPoints(robotPosition, positionExit)) && Vector3.Distance(robotPosition, positionA) < 40)
            // {
            //     targetPosition = positionExit;
            // }
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
            // 如果目标可达则前往目标，不可达则继续上一步动作
            stuckCounter = 0;
            //
            if (robotDestinationCache == targetPosition)
            {
                //targetPosition = new Vector3(2, 0.5f, -18);//位置
                GMoveAgent();
                //Debug.Log("强制位置" + robotDestinationCache);
            }
            else
            {
                robotDestinationCache = targetPosition;
                robotNavMeshAgent.SetDestination(robotDestinationCache);
               // Debug.Log(robot.name + robotDestinationCache);
            }
            //
            //robotDestinationCache = targetPosition;
            //robotNavMeshAgent.SetDestination(robotDestinationCache);
            //Debug.Log(robotAgent.name + robotDestinationCache + pathFromHereToTarget.Count);
            //robotInfo.robotCommand = GetRobotCommand();
    }
    private void GMoveAgent()
    {
        Vector3 targetPosition = new();
        Vector3 robotPosition = robot.transform.position;
        //print("机器人的跟随者数量为：" + robotInfo.myDirectFollowers.Count);
        if (robotInfo.myDirectFollowers.Count > 0)//如果当前机器人当前跟随者大于0个，前往出口
        {
            //随机一个出口，将人送到出口
            //print("2机器人检测到的出口数量为："+myEnv.Exits.Count);
            targetPosition =GetCrossDoorDestination( myEnv.Exits[0].gameObject);  
        }
        else
        {
            //找到离机器人最近的人类，并朝其进行移动
            float minDist = int.MaxValue;
            //float minDist = 18f;
            foreach (Person human in myEnv.personList)
            {
                Vector3 humanPosition = human.transform.position - new Vector3(0, 0.5f, 0);
                if (human.isActiveAndEnabled is false || Mathf.Abs(humanPosition.y - robotPosition.y) > 1 || humanPosition.x < -20 || humanPosition.z > 20)
                    continue;
                if (Vector3.Distance(humanPosition, robotPosition) < minDist)
                {
                    minDist = Vector3.Distance(humanPosition, robotPosition);
                    targetPosition = humanPosition - human.transform.forward/2;
                }
            }
            robotDestinationCache = targetPosition;
            robotNavMeshAgent.SetDestination(robotDestinationCache);
            return;
        }

        if (targetPosition != Vector3.zero  && !isTrans)
        {
            robotDestinationCache = targetPosition;
            robotNavMeshAgent.SetDestination(robotDestinationCache);
           
            return;
        }

    }//贪心算法移动机器人


   //重置所有智能体
    public void ResetAgent()
    {
        if (myEnv.useRobot is false)
            return;
        isTrans = false;
        //Debug.Log("用于测试123 " + nextRobotPosition);
        /* Vector3 nextRobotPosition = new Vector3(8,0.25f,8);  //固定位置*/

        Vector3 nextRobotPosition = GetSpawnBlockPosition(1);//fixed
        Debug.Log("生成的机器人随机位置是："+nextRobotPosition);
        robot.transform.SetPositionAndRotation(nextRobotPosition + new Vector3(0, 1, 0), Quaternion.identity); //生成随机位置 
        robot.GetComponent<Robot>().isRunning = true;//将机器人设置为工作状态

        robotDestinationCache = nextRobotPosition + new Vector3(0, 0.5f, 0);
        robotRigidbody.velocity = Vector3.zero;
        robotRigidbody.angularVelocity = Vector3.zero;
        decisionCountDown = maxDecisionCountDown;

        stuckCounter = 0;
        mileageCounter = 0;
        mileageRecorder = new Vector3();
    }

    //获取随机出生位置
    private static Vector3 GetSpawnBlockPosition(int floor)//！！！！！！！！奶奶的，坐标的问题也要注意一下（0.734，,0.25,6）范围内的坐标是（10.734，0.25,16）
    {
        Vector3 spawnBlockPosition = new();
        for (int tryCounter = 80000; tryCounter > 0; tryCounter--)
        {
            float randomX = Random.Range(1, 9) + 0.5f;
            float randomZ = Random.Range(1, 9) + 0.5f;

            if (floor == 1 && randomX is > -5 and < 8 && randomZ is > -19 and < -15)
                continue;

            spawnBlockPosition.Set(randomX, 0.5f+(floor - 1) * 4, randomZ);

            if (Physics.CheckBox(spawnBlockPosition + Vector3.up, new Vector3(0.49f, 0.49f, 0.49f)) is false)
                return spawnBlockPosition;
        }

        return new Vector3();
    }





    private Vector3 GetCrossDoorDestination(GameObject targetDoor)//去到门前的位置，该函数是给机器人使用，放置发生拥堵
    {
        Vector3 myPosition = transform.position;

        if (targetDoor.CompareTag("Door") || targetDoor.CompareTag("Exit"))
        {
            string doorDirection = targetDoor.GetComponent<Door>().doorDirection;
            Vector3 doorPosition = targetDoor.transform.position + new Vector3(0, -1.5f, 0);
            switch (doorDirection)
            {
                case "Horizontal": //水平
                    if (myPosition.z < doorPosition.z)
                        return doorPosition + new Vector3(0, 0, -1);
                    return doorPosition - new Vector3(0, 0,-1);
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
}
