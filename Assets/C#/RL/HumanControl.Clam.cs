//领导者模式。人物自己移动
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
    private void LeaderUpdate_Clam()
    {
        if (myTargetDoor is null)
        {
            //print(this.gameObject.name+"我目前没有目标门");
            //先扫描视线里有没有机器人，有的话就直接进行跟随
            Vector3 myPosition = transform.position;

            myPosition.y -= 0.5f;
            // print(myPosition.y);
            float distanceRemain = Vector3.Distance(myPosition, myDestination);
            if (distanceRemain > 0.1f)
            {//print($"距离门对面还有{distanceRemain}米，扫描沿路是否有合适的领导者");
                List<GameObject> leaderCandidates = GetCandidate_Clam(new List<string> { "Robot" }, 360, 20).Item1;
                if (leaderCandidates.Count > 0)
                {
                   // print("发现了符合追随条件的机器人，进入追随者模式");

                    myLeader = leaderCandidates[0];

                    if (myLeader.GetComponent<RobotControl>().isRunning)
                    {//如果机器人在工作，就进行跟随
                     //print("找到了在工作的机器人，我的领导者是：" + leaderCandidates[0].name);
                        if (!myLeader.GetComponent<RobotControl>().myDirectFollowers.Contains(gameObject.GetComponent<HumanControl>()))
                        {
                            //print(this.name + "将自己加入机器人的跟随者列表");
                            myLeader.GetComponent<RobotControl>().myDirectFollowers.Add(gameObject.GetComponent<HumanControl>());//将自己加入机器人的跟随者列表

                            if (myEnv.useRobotBrain&&isFounded==false)//没有被机器人发现过
                            {
                                  //机器人领导奖励
                                //myLeader.GetComponent<RobotControl>().myAgent.AddReward(health*10);//靠近人类奖励
                               //myLeader.GetComponent<RobotControl>().myAgent.LogReward("靠近人类奖励", health*10);
                                isFounded = true;
                            }
                        }
                        //print(myLeader.GetComponent<Robot>().myDirectFollowers);
                        SwitchBehaviourMode();
                    }
                    return;
                }
            }


            //目前不知道去哪，而且视线里没有找到机器人，开始自己乱逛
            //print("当前没有计划前往的门，开始扫描，然后筛选");
            (List<GameObject> doorCandidates, List<Vector3> unknownDirections) = GetCandidate_Clam(new List<string> { "Door", "Exit" }, 360, 20);

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
                        List<GameObject> leaderCandidates = GetCandidate_Clam(new List<string> { "Robot" }, 360, 20).Item1;
                        if (leaderCandidates.Count > 0)
                        {
                            //print("发现了符合追随条件的人类或者机器人，进入追随者模式");

                            myLeader = leaderCandidates[0];//默认将

                            if (myLeader.GetComponent<RobotControl>().isRunning)
                            {//如果机器人在工作，就进行跟随
                             //print("找到了在工作的机器人，我的领导者是：" + leaderCandidates[0].name);
                                if (!myLeader.GetComponent<RobotControl>().myDirectFollowers.Contains(gameObject.GetComponent<HumanControl>()))
                                {
                                    //print(this.name + "将自己加入机器人的跟随者列表");
                                    myLeader.GetComponent<RobotControl>().myDirectFollowers.Add(gameObject.GetComponent<HumanControl>());//将自己加入机器人的跟随者列表
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
                else { myTargetDoor = null; }

            }
        }
    }
    //跟随者模式
    private void FollowerUpdate_Clam()
    {
        if (myLeader != null)
        {
            //print("切换模式后，我的追随者是：" + myLeader.name);
            Vector3 leaderPosition = myLeader.transform.position;
            List<GameObject> exitList = GetCandidate_Clam(new List<string> { "Exit" }, 360, 20).Item1;

            //鼓励机器人主动吸引更多跟随者
            if (myLeader != null && myLeader.CompareTag("Robot"))
            {
                // 每帧给予微小正向奖励（需乘以Time.fixedDeltaTime）
                /* myEnv.RobotBrainList[0].AddReward(0.3f * (1-panicLevel));
                 myEnv.RobotBrainList[0].LogReward("人类跟随奖励", 0.3f * (1 - panicLevel));*/
            }
            //在跟随的过程中，持续进行检测是否有出口，有的话就直接离开,没有的话就继续跟随机器人

            if (exitList.Count > 0)
            {

                if (myLeader.tag == "Robot")//领导者是机器人
                {
                    myLeader.GetComponent<RobotControl>().myDirectFollowers.Remove(gameObject.GetComponent<HumanControl>());
                }
                else//领导者是人类
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
        else { SwitchBehaviourMode(); }
    }


    private Tuple<List<GameObject>, List<Vector3>> GetCandidate_Clam(List<string> targetTags, int visionWidth, int visionDiff)
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
        else if (targetTags.Contains("Robot"))
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

}