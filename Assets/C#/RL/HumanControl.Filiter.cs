using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;
public partial class HumanControl: MonoBehaviour
{
    // 碰撞检测计数器
    public int dazingCountDown;
    public GameObject myTopLevelLeader;
    public List<HumanControl> myDirectFollowers;
    // Start is called before the first frame update



    //领导者模式。人物自己移动
    private void LeaderUpdate_HF()   //Herd Following
    {
        if (myTargetDoor is null)
        {
            //print(this.gameObject.name+"我目前没有目标门");
         
            //目前不知道去哪，而且视线里没有找到机器人，开始自己乱逛
            //print("当前没有计划前往的门，开始扫描，然后筛选");
            (List<GameObject> doorCandidates, List<Vector3> unknownDirections) = GetCandidate(new List<string> { "Door", "Exit" }, 360,8);

            GameObject exit = FilterTargetDoorCandidates(ref doorCandidates, unknownDirections.Count > 0 ? "Explore" : "Normal");
            if (exit is not null)
            {
                // print("发现出口，直接选择出口作为目标");
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
                // print("候选的门中不存在出口，随机选择一扇可通过的门作为移动目标");
                if (lastDoorWentThrough == null)//"自己从没有经历过门"
                {
                    myTargetDoor = doorCandidates[Random.Range(0, doorCandidates.Count)];
                }
                // Case 2: 有记录门且存在其他候选门 -> 排除记录门后随机选择
                else if (doorCandidates.Count >= 1)
                {
                    // 创建排除列表： 记忆队列中的所有门
                    var excludedDoors = new HashSet<GameObject>(_doorMemoryQueue);
                    var validDoors = doorCandidates.Where(door => !excludedDoors.Contains(door)).ToList();
                    //door => !excludedDoors.Contains(door)，Lambda表达式 

                    if (validDoors.Count > 0)//探索到了记忆中不存在的门，
                    {//Debug.Log($"{this.name} 我搜索到的门有：{string.Join(", ", validDoors.Select(door => door.name))}");
                        myTargetDoor = validDoors[Random.Range(0, validDoors.Count)];
                    }
                    else
                    {
                        // 如果所有门都被排除，则强制选择非历史门的随机门（fallback机制）
                        var fallbackDoors = doorCandidates.Where(door => door != lastDoorWentThrough).ToList();
                        myTargetDoor = fallbackDoors.Count > 0 ? fallbackDoors[Random.Range(0, fallbackDoors.Count)] : doorCandidates[0]; // 最终回退

                    }
                }


                // print("选择的门是：" + myTargetDoor.transform.name);
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
                //检查门是否被烧毁
                if (myTargetDoor.GetComponent<DoorControl>().isBurnt == false)
                {
                    // print("当前存在计划前往的门，正在向门对面移动");
                    Vector3 myPosition = transform.position;
                    myPosition.y -= 0.5f;
                    float distanceRemain = Vector3.Distance(myPosition, myDestination);
                    if (distanceRemain > 0.5f)
                    {//print($"距离门对面还有{distanceRemain}米，扫描沿路是否有合适的领导者");

                        List<GameObject> leaderCandidates = GetCandidate(new List<string> { "Human", "Robot" }, 360, 8).Item1;
                      

                        if (leaderCandidates.Count > 0)
                        {
                           // Debug.Log("附近有可以跟随的领导者，我已切换为跟随者模式");
                                SwitchBehaviourMode();
   
                            return;
                        }
                    }
                    else
                    {  //print($"距离门对面还有{distanceRemain}米，初步认为已经认为已经到达目的地，开始重新扫描");
                        myTargetDoor = null;
                        return;
                    }
                }
                else { myTargetDoor = null; }

            }
        }
    }
    //跟随者模式
    private void FollowerUpdate_HF()  //Herd Following(盲目跟随)
    {
        //print("切换模式:追随者");

        if (myLeader == null)//如果当前没有领导者，则先寻找领导者
        {
            List<GameObject> leaderCandidates = GetCandidate(new List<string> { "Human", "Robot" }, 360, 5).Item1;
            //FilterLeaderCandidates(ref leaderCandidates);//筛选掉不能作为自己领导者的人；
            //Debug.Log("我检测到的人类数量为："+leaderCandidates.Count);
            // GameObject targetLeader= DecideMyLeader(leaderCandidates);//在剩余的人中随机选择一个作为自己的领导者

            // 新增过滤：排除已经有领导者的人类
            leaderCandidates = leaderCandidates
                .Where(candidate =>
                    candidate != gameObject &&  // 排除自己
                    (candidate.CompareTag("Robot") ||  // 机器人无条件允许
                     (candidate.CompareTag("Human") &&
                      candidate.GetComponent<HumanControl>().myLeader == null)) // 人类需无领导者
                ).ToList();


            GameObject targetLeader;
            if (leaderCandidates.Count > 0)
            {
                 targetLeader = leaderCandidates[Random.Range(0, leaderCandidates.Count)];
                // 使用 targetLeader...
                //Debug.Log("！！！！我的领导者是："+targetLeader);
                if (targetLeader.CompareTag("Human"))
                {
                   // print("领导者是人类");
                    //add

                    if (targetLeader.GetComponent<HumanControl>().dazingCountDown < 2)
                    {
                        dazingCountDown = Random.Range(2, 8);
                        return;
                    }
                    else
                    {
                        myLeader = targetLeader;
                        myLeader.GetComponent<HumanControl>().myDirectFollowers.Add(gameObject.GetComponent<HumanControl>());
                    }
                }
                else if (targetLeader.CompareTag("Robot"))
                {
                    // 直接跟随机器人
                    myLeader = targetLeader;
                    myLeader.GetComponent<RobotControl>().myDirectFollowers.Add(gameObject.GetComponent<HumanControl>());
                }
            }
            else
            {
                
                Debug.LogWarning("没有可选的领导者候选对象！切换回领导者模式");
                // 处理无候选的情况（如设为 null 或默认值）
                 targetLeader = null;
                SwitchBehaviourMode();
            }
        }
        else//已经有领导者了，则跟随领导者，直到出口。
        {
            Vector3 leaderPosition = myLeader.transform.position;
            List<GameObject> exitList = GetCandidate(new List<string> { "Exit" }, 360, 30).Item1;

            //在跟随的过程中，持续进行检测是否有出口，有的话就直接离开,没有的话就继续跟随机器人
            if (exitList.Count > 0)
            {
                if (myLeader.tag == "Robot")
                {
                    myLeader.GetComponent<RobotControl>().myDirectFollowers.Remove(gameObject.GetComponent<HumanControl>());
                }
                else if (myLeader.tag == "Human")
                {
                    myLeader.GetComponent<HumanControl>().myDirectFollowers.Remove(gameObject.GetComponent<HumanControl>());
                }
                print(this.name + "将自己移除机器人的跟随列表");
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
                // 假设 leader 有 Transform 组件
                Vector3 leaderForward = myLeader.transform.forward;
                Vector3 targetPosition = leaderPosition - leaderForward * 1f;
                _myNavMeshAgent.SetDestination(targetPosition);
            }
        }
    }


    private Vector3 GetCrossDoorDestination(GameObject targetDoor)//去到穿过门的位置
    {
        //Debug.Log("执行了GetCrossDoorDestionation函数");
        Vector3 myPosition = transform.position;

        if (targetDoor.CompareTag("Door"))
        {
            string doorDirection = targetDoor.GetComponent<DoorControl>().doorDirection;
            Vector3 doorPosition = targetDoor.transform.position + new Vector3(0, -1.5f, 0);
            switch (doorDirection)
            {
                case "Vertical":
                    if (myPosition.z < doorPosition.z)
                        return doorPosition + new Vector3(0, 0, 2);
                    return doorPosition - new Vector3(0, 0, 2);
                case "Horizontal":  //水平
                    if (myPosition.x < doorPosition.x)
                        return doorPosition + new Vector3(2, 0, 0);
                    return doorPosition - new Vector3(2, 0, 0);
                default:
                    return myPosition;
            }
        }
        else if (targetDoor.CompareTag("Exit"))
        {
            Vector3 doorPosition = targetDoor.transform.position + new Vector3(0, -1.5f, 0);
            return doorPosition;
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
    private Tuple<List<GameObject>, List<Vector3>> GetCandidate(List<string> targetTags, int visionWidth, int visionDiff)
    {
        // 初始化候选对象列表和未知方向列表
        List<GameObject> candidateList = new();
        List<Vector3> unknownDirections = new();
        Vector3 myPosition = transform.position;
        // 获取当前对象的位置
        String layer = "Door";//根据标签来获取射线检测的层次,默认扫描门
        if (targetTags.Contains("Door") || targetTags.Contains("Exit"))
        {
            layer = "Default";
        }
        else if (targetTags.Contains("Robot")|| targetTags.Contains("Human"))//射线探索跟随者
        {
            layer = "Follower";
        }
        foreach (Vector3 vision in GetVision(visionWidth, visionDiff))
        {
            // 从当前位置向视线方向发射射线
            if (Physics.Raycast(myPosition, vision, out RaycastHit hit, visionLimit, LayerMask.GetMask(layer)))
            {
                // 如果射线击中的对象的标签在目标标签列表中，并且该对象不在候选列表中，则添加到候选列表
                if (targetTags.Contains(hit.transform.tag) && !candidateList.Contains(hit.transform.gameObject))
                    // print("扫描到的门有："+hit.transform.gameObject.name);
                    candidateList.Add(hit.transform.gameObject);
            }
            else
            {
                // 如果射线没有击中任何对象，则将该方向添加到未知方向列表
                //print("没有扫描到物体");
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
        if (leaderCandidates.Count > 0)
        {
            for (int candidateIndex = leaderCandidates.Count - 1; candidateIndex >= 0; candidateIndex--)
            {
                if (leaderCandidates.Count <= 0)
                    break;

                if (leaderCandidates[candidateIndex] == this.gameObject) //是本物体
                {
                    //Debug.Log(leaderCandidates[candidateIndex]);
                    leaderCandidates.Remove(leaderCandidates[candidateIndex]);
                }
                
              /*  else if (leaderCandidates[candidateIndex].CompareTag("Human"))
                {
                    HumanControl candidateInfo = leaderCandidates[candidateIndex].GetComponent<HumanControl>();
                    //add
                    if (candidateInfo.myTopLevelLeader.CompareTag("Human"))
                        leaderCandidates.Remove(leaderCandidates[candidateIndex]);

                    //minus
                    //if (candidateInfo.myFollowerCounter < myFollowerCounter &&
                    //    candidateInfo.myTopLevelLeader is not null &&
                    //    candidateInfo.myTopLevelLeader.CompareTag("Human") &&
                    //    candidateInfo.myTopLevelLeader.GetComponent<Human>().myFollowerCounter <= myFollowerCounter)
                    //    leaderCandidates.Remove(leaderCandidates[candidateIndex]);
                }*/
            }
        }
    }

    private GameObject DecideMyLeader(List<GameObject> leaderCandidates)
    {
        List<GameObject> humanCandidates = new();
        List<GameObject> robotCandidates = new();

        foreach (GameObject candidate in leaderCandidates)
        {
            if (candidate.CompareTag("Human"))
            {
                humanCandidates.Add(candidate);
                continue;
            }

            if (candidate.CompareTag("Robot"))
            {
                robotCandidates.Add(candidate);
                continue;
            }
        }
        //robotCandidates = RbtList;///
        if (robotCandidates.Count > 0)
            return robotCandidates[Random.Range(0, robotCandidates.Count)];
        else if (humanCandidates.Count > 0)
        {
            Debug.Log("返回人类列表");
            return humanCandidates[Random.Range(0, humanCandidates.Count)];
        }
        else
            return null;
    }
}
