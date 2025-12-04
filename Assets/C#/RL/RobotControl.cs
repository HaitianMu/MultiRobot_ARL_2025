using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public class RobotControl : MonoBehaviour
{
    public string robotCommand;
    public List<HumanControl> myDirectFollowers;
    public int robotFollowerCounter;
    public RobotBrain myAgent;
    // bot的NavMeshAgent组件
    private NavMeshAgent _botNavMeshAgent;
    public bool isRunning;//机器人是否处于工作状态
    // Start is called before the first frame update
    public void Start()
    {
        this.gameObject.SetActive(true);
        isRunning = true;//机器人默认工作
        myDirectFollowers = new List<HumanControl>();
        _botNavMeshAgent = GetComponent<NavMeshAgent>();
        _botNavMeshAgent.speed = 6f;//将机器人速度设置为5m/s
    }

    public void Update()
    {
        robotFollowerCounter = myDirectFollowers.Count;
        myAgent.robotPosition=this.transform.position;

    }

    private void OnTriggerEnter(Collider trigger)
    {
        // Debug.Log("碰撞发生，碰撞体的标签是：" + trigger.transform.tag);
        GameObject triggerObject = trigger.gameObject;
      
        switch (trigger.transform.tag)
        {
            case "Fire":
                print("机器人碰到火焰");
                myAgent.stuckCounter++;
                myAgent.AddReward(-5);//碰一次给5点惩罚
                myAgent.LogReward("机器人触碰火焰惩罚",-5);
                break;
        }
    }


}
