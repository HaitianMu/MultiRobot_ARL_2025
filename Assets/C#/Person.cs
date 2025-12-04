using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

public partial class Person : MonoBehaviour
{
    public Env myEnv;
    public Transform targetPosition; // 目标位置
    public int visionLimit = 1; // 射线检测距离
    private NavMeshAgent _myNavMeshAgent; // 导航代理组件
    private Queue<GameObject> _doorMemoryQueue;//用于记录人看到的门
    public GameObject myTargetDoor = null;  // 当前计划前往的门                              
    public GameObject lastDoorWentThrough;// 上一扇经过的门
    public Vector3 myDestination;     // 移动的目的地 
    public bool isReturnFromLastDoor;  // 是否从死胡同返回
    public String myBehaviourMode; //该人类的行为模式
    public int myFollowerCounter;  //跟随者的数量
    public List<GameObject> RbtList;  //发现的机器人列表
    public GameObject myLeader;//领导者目前只能是机器人 1.19

    public void Start()
    {
        myBehaviourMode = "Leader";
        _myNavMeshAgent = GetComponent<NavMeshAgent>();
        myDestination = new Vector3();
        _doorMemoryQueue = new Queue<GameObject>();
        myTargetDoor = null;
        lastDoorWentThrough = null;
    }
    private void FixedUpdate()
    {
        //每个人刚开始都是独立的领导者，但是随着程序的进行，
        //当看到机器人时，人类会进行跟随
        switch (myBehaviourMode)
        {
            case "Follower":
                FollowerUpdate();
                break;
            case "Leader":
                LeaderUpdate();
                break;
        }

        foreach (Vector3 vision in GetVision(360, 5))//检查视野范围内是否有机器人
        {
            // 从物体位置发射射线
            if (Physics.Raycast(transform.position, vision, out RaycastHit hit, visionLimit))//自身0.5f范围内是否有物体
            {
                GameObject hitObject = hit.collider.gameObject;
                // 检查射线击中的物体是否具有特定标签
                if (hitObject.tag == "Robot" && !RbtList.Contains(hitObject))
                {

                }
            }
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
            Debug.DrawRay(transform.position, vision * 0.4f, Color.green);
            // 调试用的代码，用于在场景中绘制视线方向（可注释掉）
        }

        return myVisions;
    }

    //领导者模式。人物自己移动
    private void LeaderUpdate()
    {
        if (myTargetDoor is null)
        {
            // print("当前没有计划前往的门，开始扫描，然后筛选");
            (List<GameObject> doorCandidates, List<Vector3> unknownDirections) = GetCandidate(new List<string> { "Door", "Exit" }, 360, visionLimit);
            GameObject exit = FilterTargetDoorCandidates(ref doorCandidates, unknownDirections.Count > 0 ? "Explore" : "Normal");
            if (exit is not null)
            {

                //print("发现出口，直接选择出口作为目标");
                myTargetDoor = exit;
                myDestination = GetCrossDoorDestination(exit);
                _myNavMeshAgent.SetDestination(myDestination);
                return;
            }
            else if (doorCandidates.Count <= 0)
            {
                if (unknownDirections.Count <= 0)
                {
                    if (lastDoorWentThrough is not null)
                    {
                        myTargetDoor = lastDoorWentThrough;
                        myDestination = GetCrossDoorDestination(lastDoorWentThrough);
                        _myNavMeshAgent.SetDestination(myDestination);
                    }

                    return;
                }
                else if (unknownDirections.Count > 0)
                {
                    Vector3 exploreDirection = unknownDirections[Random.Range(0, unknownDirections.Count)];
                    _myNavMeshAgent.SetDestination(transform.position + exploreDirection * visionLimit);
                    return;
                }
            }
            else if (doorCandidates.Count > 0)
            {
                //print("候选的门中不存在出口，随机选择一扇门作为移动目标");

                myTargetDoor = doorCandidates[Random.Range(0, doorCandidates.Count)];//Random.Range(0, doorCandidates.Count)
                //print("选择的门是：" + myTargetDoor.transform.name);
                myDestination = GetCrossDoorDestination(myTargetDoor);

                if (_myNavMeshAgent.SetDestination(myDestination))
                {
                    // print("目的地是：" + myDestination);
                }
                else { print("设置目的地失败"); };

                return;
            }
        }
        else
        {
            if (myTargetDoor.tag.Contains("Exit"))//如果目标门是出口的话，直接滚蛋
            {
                return;
            }
            else
            {
                // print("当前存在计划前往的门，正在向门对面移动");
                Vector3 myPosition = transform.position;
                myPosition.y -= 0.5f;
                float distanceRemain = Vector3.Distance(myPosition, myDestination);
                if (distanceRemain > 0.5f)
                {//print($"距离门对面还有{distanceRemain}米，扫描沿路是否有合适的领导者");
                    List<GameObject> leaderCandidates = GetCandidate(new List<string> { "Human", "Robot" }, 360, visionLimit).Item1;
                    if (leaderCandidates.Count > 0)
                    {
                        //print("发现了符合追随条件的人类或者机器人，进入追随者模式");

                        myLeader = leaderCandidates[0];

                        if (myLeader.GetComponent<Robot>().isRunning)
                        {//如果机器人在工作，就进行跟随
                         //print("找到了在工作的机器人，我的领导者是：" + leaderCandidates[0].name);
                            if (!myLeader.GetComponent<Robot>().myDirectFollowers.Contains(gameObject.GetComponent<Person>()))
                            {
                                //print(this.name + "将自己加入机器人的跟随者列表");
                                myLeader.GetComponent<Robot>().myDirectFollowers.Add(gameObject.GetComponent<Person>());//将自己加入机器人的跟随者列表
                            }
                            //print(myLeader.GetComponent<Robot>().myDirectFollowers);
                            SwitchBehaviourMode();
                        }
                        return;
                    }
                }
                else
                {  //print($"距离门对面还有{distanceRemain}米，初步认为已经认为已经到达目的地，开始重新扫描");
                    myTargetDoor = null;
                    return;
                }
            }
            }
        }
    //跟随者模式
    private void FollowerUpdate()
    {
        //print("切换模式后，我的追随者是：" + myLeader.name);
        Vector3 leaderPosition = myLeader.transform.position;

        List<GameObject> exitList = GetCandidate(new List<string> { "Exit" }, 360, visionLimit).Item1;
        //在跟随的过程中，持续进行检测是否有出口，有的话就直接离开,没有的话就继续跟随机器人
        if (exitList.Count > 0)
        {
            myLeader.GetComponent<Robot>().myDirectFollowers.Remove(gameObject.GetComponent<Person>());
            print(this.name+"将自己移除机器人的跟随列表");
            //set
            GameObject exit = exitList[0];
            SwitchBehaviourMode();
            myTargetDoor = exit;
            myDestination = GetCrossDoorDestination(exit);
            _myNavMeshAgent.SetDestination(myDestination);
            return;
        }
        else //一直跟随，直到看到出口
        {
            _myNavMeshAgent.SetDestination(leaderPosition);
         }
    }

    private Tuple<List<GameObject>, List<Vector3>> GetCandidate(List<string> targetTags, int visionWidth, int visionDiff)
    {
        // 初始化候选对象列表和未知方向列表
        List<GameObject> candidateList = new();
        List<Vector3> unknownDirections = new();
        Vector3 myPosition = transform.position;
        // 获取当前对象的位置
        String layer="Door";//根据标签来获取射线检测的层次,默认扫描门
        if (targetTags.Contains("Door")||targetTags.Contains("Exit"))
        {
            layer = "Default";
        }
        else if (targetTags.Contains("Robot"))
        {
            layer = "Robot";
        }

        foreach (Vector3 vision in GetVision(visionWidth, visionDiff))
        {
            // 从当前位置向视线方向发射射线
            if (Physics.Raycast(myPosition, vision, out RaycastHit hit, visionLimit,LayerMask.GetMask(layer)))
            {
                // 如果射线击中的对象的标签在目标标签列表中，并且该对象不在候选列表中，则添加到候选列表
                if (targetTags.Contains(hit.transform.tag) && !candidateList.Contains(hit.transform.gameObject))
                    //print("扫描到的门有："+hit.transform.gameObject.name);
                    candidateList.Add(hit.transform.gameObject);
            }
            else
            {
                // 如果射线没有击中任何对象，则将该方向添加到未知方向列表
                unknownDirections.Add(vision);
               // print("扫描到的未知方向有：" + vision);
            }
        }

        //RbtList = candidateList;
        // 返回候选对象列表和未知方向列表
        return Tuple.Create(candidateList, unknownDirections);
    }
    private GameObject FilterTargetDoorCandidates(ref List<GameObject> targetDoorCandidates, string filterMode)
    {
       // Debug.Log("我在执行出口筛选函数");
        GameObject exit = null;

        // 先筛一遍可以绝对排除的
        if (targetDoorCandidates.Count > 0)
        {
            for (int doorCandidateIndex = targetDoorCandidates.Count - 1; doorCandidateIndex >= 0; doorCandidateIndex--)
            {
                // 如果一个候选都没有就直接退出
                if (targetDoorCandidates.Count <= 0)
                    break;

                // 如果存在出口就返回出口
                if (targetDoorCandidates[doorCandidateIndex].CompareTag("Exit"))
                {
                    exit = targetDoorCandidates[doorCandidateIndex];
                    break;
                }
                // 现在还有没探索过的方向，且这个门在记忆队列里
                if (filterMode is "Explore" && _doorMemoryQueue.Contains(targetDoorCandidates[doorCandidateIndex]))
                {

                    targetDoorCandidates.Remove(targetDoorCandidates[doorCandidateIndex]);
                    continue;
                }
            }

        }
        // 再筛一遍需要比较后排除的
        if (targetDoorCandidates.Count > 1)
        {
            for (int doorCandidateIndex = targetDoorCandidates.Count - 1; doorCandidateIndex >= 0; doorCandidateIndex--)
            {
                // 在同时存在"上楼楼梯"和"走过的门"时， 优先筛掉"上楼楼梯"
                // 但是当人类是从经过的门返回时，优先筛掉"走过的门"
                if (filterMode is "Normal" && targetDoorCandidates[doorCandidateIndex] == lastDoorWentThrough)
                {
                    targetDoorCandidates.Remove(targetDoorCandidates[doorCandidateIndex]);
                    continue;
                }
            }
        }
        return exit;
    }
    private void FilterLeaderCandidates(ref List<GameObject> leaderCandidates)
    {
        myLeader = leaderCandidates[0].gameObject;
    }
    private bool IsOnNavMesh(Vector3 targetPosition)
    {
        return NavMesh.SamplePosition(targetPosition, out NavMeshHit _, 0.1f, 1);
    }

    private Vector3 GetCrossDoorDestination(GameObject targetDoor)//去到穿过门的位置
    {
        Vector3 myPosition = transform.position;

        if (targetDoor.CompareTag("Door") || targetDoor.CompareTag("Exit"))
        {
            string doorDirection = targetDoor.GetComponent<Door>().doorDirection;
            Vector3 doorPosition = targetDoor.transform.position + new Vector3(0, -1.5f, 0);
            switch (doorDirection)
            {
                case "Horizontal":
                    if (myPosition.z < doorPosition.z)
                        return doorPosition + new Vector3(0, 0, 2);
                    return doorPosition - new Vector3(0, 0, 1);
                case "Vertical":
                    if (myPosition.x < doorPosition.x)
                        return doorPosition + new Vector3(2, 0, 0);
                    return doorPosition - new Vector3(1, 0, 0);
                default:
                    return myPosition;
            }
        }
        else
        {
            return myPosition;
        }
    }

    /// <summary>
    /// 切换行为模式
    /// </summary>
    private void SwitchBehaviourMode()
    {
        if (myBehaviourMode == "Follower")
        {
            myBehaviourMode = "Leader";
            
            myTargetDoor = null;
            // _myRigidBody.mass = 4;
        }
        else
        {
            myBehaviourMode = "Follower";
            
            //追随机器人
            myTargetDoor = null;
            // _myRigidBody.mass = 2;
        }
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
                if (!_doorMemoryQueue.Contains(triggerObject))
                    _doorMemoryQueue.Enqueue(triggerObject);
                //print(triggerObject.name + "已经被加入记忆");
                if (_doorMemoryQueue.Count > 4)
                    _doorMemoryQueue.Dequeue();
                break;
            case "Exit":
                // print("我成功逃离了");
              /*  myEnv.personList.Remove(this);*/
                this.gameObject.SetActive(false);
                
                break;
        }
    }
}


