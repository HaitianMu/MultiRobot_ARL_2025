using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEditor;
using UnityEngine;

public class HumanBrain : Agent
{
    // 在EnvController中定义常量
    public const int MAX_HUMANS = 10; //最大人类数量， 与课程学习上限一致
    public const int MAX_ROOMS = 15; // 与建筑设计上限一致
    public const float INVALID_MARKER = -2f; // 超出[-1,1]范围的无效标记

    public EnvControl myEnv;
    public HumanControl myHuman;//大脑对应的人类
    public bool HumanIsInitialized=false;
    public int HumanState;//根据决策确定的人类移动状态；

    public void FixedUpdate()
    {
        if (HumanIsInitialized)
        {
            //print("初始化已完成，我的小人是"+myHuman.name);
            return;
        }
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

        if (!myEnv.useHumanAgent) return;

        //Debug.Log("CollectObservations called."); 
        if (myEnv == null || myEnv.useHumanAgent is false)
        {
            Debug.Log("myEnv is null or useHumanAgen is false.");
            return;
        }
        //  print("场景对角线为长度为："+sceneDiagonal);
        sensor.AddObservation(myHuman.health);//人类自己的生命值  1
        sensor.AddObservation(myHuman.CurrentState);//人类当前的状态   1
        // 归一化 Agent 位置 ，           2个
        foreach (RobotBrain agent in myEnv.RobotBrainList)
        {
            sensor.AddObservation(NormalizedPos(agent.robot.transform.position).x);
            sensor.AddObservation(NormalizedPos(agent.robot.transform.position).z);
            //Debug.Log("机器人的位置为" + normalizedPos);
        }

        // 归一化 Human 位置，人类最多10个            20个

        // 固定观测维度为 MAX_HUMANS * 2
        for (int i = 0; i < MAX_HUMANS; i++)
        {
            if (i < myEnv.personList.Count)
            {
                // 填充实际人类位置
                HumanControl human = myEnv.personList[i];
                sensor.AddObservation(NormalizedPos(human.transform.position).x);
                sensor.AddObservation(NormalizedPos(human.transform.position).z);
            }
            else
            {
                // 填充占位值（推荐使用无效坐标）
                sensor.AddObservation(-1f); // x
                sensor.AddObservation(-1f); // z
            }
        }


        //移除人类对于房间、出口、火源的感知
        /*// 添加房间位置（相对Agent） 最大20个房间，  40//
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
*/
        //删除人类对火源和出口位置的感知
       /* //添加出口位置   只有1个出口         39+[24,45]=[63,84]     2个
        sensor.AddObservation(NormalizedPos(myEnv.Exits[0].transform.position).x);
        sensor.AddObservation(NormalizedPos(myEnv.Exits[0].transform.position).z);
        //Debug.Log("出口的位置为" + (myEnv.Exits[0].transform.position) / Mathf.Max(myEnv.complexityControl.buildingGeneration.totalWidth, myEnv.complexityControl.buildingGeneration.totalHeight));

        //添加火源位置，目前火源只设置了三个      6个
        for (int i = 0; i < 3; i++)
        {
            Vector3 firePos = myEnv.FirePosition[i];
            {
                // 位置归一化（相对于环境中心）
                sensor.AddObservation(NormalizedPos(firePos).x);
                sensor.AddObservation(NormalizedPos(firePos).z);
                // Debug.Log("火源的位置为" + normalizedPos);
            }
        }*/
    }

    public Vector3 NormalizedPos(Vector3 pos)
    {
        float maxX = myEnv.complexityControl.buildingGeneration.totalWidth;
        float maxZ = myEnv.complexityControl.buildingGeneration.totalHeight;
        // 归一化到 [-1, 1] 范围
        float normalizedX = (pos.x / maxX) * 2 - 1;
        float normalizedZ = (pos.z / maxZ) * 2 - 1;

        return new Vector3(normalizedX, 0.5f, normalizedZ);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!myEnv.useHumanAgent) return;
        ChangeHumanState(actions);
    }

    public void ChangeHumanState(ActionBuffers actions)
    {

        ActionSegment<int> DiscreteActions = actions.DiscreteActions;
        //Debug.Log(this.name+"Actions:"+ DiscreteActions[0]);
        //HumanState = DiscreteActions[0];
            HumanState = DiscreteActions[0];
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
        string directoryPath = Path.Combine(Application.persistentDataPath, "HumanReward");
        string filePath = Path.Combine(directoryPath, $"Human_Reward_log_{DateTime.Now:yyyyMMdd_HHmmss}.json");

        try
        {
            // 确保目录存在
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            string result = "";
            foreach (var kv in rewardLog)
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
}
