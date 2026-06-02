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

        // 如果已经有确定的门目标，或者正在跟随机器人，通常不需要在这里做漫游逻辑
        // 但这里我们主要处理“没有目标”时的状态
        if (myTargetDoor is null && myLeader == null)
        {
            // 1. 优先扫描机器人 (保持你原有的逻辑优先级)
            // 优化：使用复用的 List，避免 new List
            var result = GetCandidate_Clam(new List<string> { "Robot" }, 360, 20);
            List<GameObject> leaderCandidates = result.Item1;

            if (leaderCandidates.Count > 0)
            {
                GameObject potentialLeader = leaderCandidates[0];
                // 优化：尝试获取组件并检查 null
                if (potentialLeader.TryGetComponent(out RobotControl robotControl))
                {
                    if (robotControl.isRunning)
                    {
                        // 找到了在工作的机器人，设置为 Leader 并结束漫游逻辑
                        myLeader = potentialLeader;
                        SwitchBehaviourMode();
                        return; // 退出函数，交给 Update 的跟随逻辑去处理
                    }
                }
            }

            // 2. 如果没有找到机器人，执行【全图探索逻辑】
            // 检查当前是否已经到达目的地，或者还没有路径
            if (!_myNavMeshAgent.hasPath || _myNavMeshAgent.remainingDistance < _myNavMeshAgent.stoppingDistance + 0.5f)
            {
                List<GameObject> exitList = GetCandidate_Clam(new List<string> { "Exit" }, 360, 20).Item1;
                if (exitList.Count > 0)//看到出口了就走
                {
                    GameObject exit = exitList[0];
                    SwitchBehaviourMode();
                    myTargetDoor = exit;
                    myDestination = GetCrossDoorDestination(exit);
                    _myNavMeshAgent.SetDestination(myDestination);
                    return;
                }
                else//没有出口就随机探索
                {
                    WanderGlobally();
                }
            }
        }
    }

    /// <summary>
    /// 全图探索逻辑：寻找一个足够远的随机点
    /// </summary>
    private void WanderGlobally()
    {
        // 尝试次数，防止死循环
        int attempts = 10;
        float minExploreDistance = 15f; // 最小探索距离：必须走这么远
        float maxExploreRadius = 60f;   // 最大搜索半径：在这个范围内找

        for (int i = 0; i < attempts; i++)
        {
            // 在球面上随机取一个方向，并乘以随机距离
            Vector3 randomDirection = Random.onUnitSphere * Random.Range(minExploreDistance, maxExploreRadius);
            // 加上当前坐标（相对于玩家位置的偏移）
            // 或者：如果你的地图不大，可以直接用 Vector3(Random.Range(-X, X), 0, Random.Range(-Z, Z)) 来取绝对坐标
            Vector3 randomDest = transform.position + randomDirection;

            NavMeshHit hit;
            // 在随机点附近找合法的 NavMesh 位置
            if (NavMesh.SamplePosition(randomDest, out hit, 10f, NavMesh.AllAreas))
            {
                // 再次检查距离：确保找到的点真的离我很远（防止 SamplePosition 把点吸附回墙这边）
                if (Vector3.Distance(transform.position, hit.position) >= minExploreDistance)
                {
                    myDestination = hit.position;
                    _myNavMeshAgent.SetDestination(myDestination);
                    // 稍微降低一点探索时的速度，显得悠闲一点，或者保持原速
                    // _myNavMeshAgent.speed = 3.5f; 
                    return; // 找到目标，结束
                }
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