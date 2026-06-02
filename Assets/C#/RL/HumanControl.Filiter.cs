using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

public partial class HumanControl : MonoBehaviour
{
    // [建议] 可以在这里限制一下最大跟随数量，防止贪吃蛇太长导致掉帧，但为了模拟恐慌拥挤，不限制也可以。
    public List<HumanControl> myDirectFollowers = new List<HumanControl>();

    // --- 领导者模式更新逻辑 ---
    private void LeaderUpdate_HF()   // Herd Following
    {
        if (myTargetDoor is null)
        {
            // 1. 扫描周围的门和潜在的领导者
            var scanResult = GetCandidate(new List<string> { "Door", "Exit", "Human", "Robot" }, 360, 8);
            List<GameObject> allCandidates = scanResult.Item1;
            List<Vector3> unknownDirections = scanResult.Item2;

            // [优化] 在盲目找门之前，先看看有没有机器人或者能带路的人！
            // 如果我是没头苍蝇（没有目标门），看到机器人应该直接贴上去，而不是继续找门。
            List<GameObject> leaderCandidates = allCandidates.Where(c => c.CompareTag("Robot") || c.CompareTag("Human")).ToList();

            // 过滤一下，只要机器人，或者有组织的人类
            leaderCandidates = FilterValidLeaders(leaderCandidates);

            if (leaderCandidates.Count > 0)
            {
                // 发现有人带路，果断放弃找门，切换为跟随者
                SwitchBehaviourMode();
                return;
            }

            // --- 下面是原本的找门逻辑 ---
            List<GameObject> doorCandidates = allCandidates.Where(c => c.CompareTag("Door") || c.CompareTag("Exit")).ToList();
            GameObject exit = FilterTargetDoorCandidates(ref doorCandidates, unknownDirections.Count > 0 ? "Explore" : "Normal");

            if (exit is not null)
            {
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
                else
                {
                    Vector3 exploreDirection = unknownDirections[Random.Range(0, unknownDirections.Count)];
                    _myNavMeshAgent.SetDestination(transform.position + exploreDirection * visionLimit);
                    return;
                }
            }
            else if (doorCandidates.Count > 0)
            {
                if (lastDoorWentThrough == null)
                {
                    myTargetDoor = doorCandidates[Random.Range(0, doorCandidates.Count)];
                }
                else
                {
                    var excludedDoors = new HashSet<GameObject>(_doorMemoryQueue);
                    var validDoors = doorCandidates.Where(door => !excludedDoors.Contains(door)).ToList();

                    if (validDoors.Count > 0)
                    {
                        myTargetDoor = validDoors[Random.Range(0, validDoors.Count)];
                    }
                    else
                    {
                        var fallbackDoors = doorCandidates.Where(door => door != lastDoorWentThrough).ToList();
                        myTargetDoor = fallbackDoors.Count > 0 ? fallbackDoors[Random.Range(0, fallbackDoors.Count)] : doorCandidates[0];
                    }
                }

                myDestination = GetCrossDoorDestination(myTargetDoor);
                if (_myNavMeshAgent.SetDestination(myDestination)) { }
                else { print("设置目的地失败"); };
                return;
            }
        }
        else
        {
            // 已经有目标门了
            if (myTargetDoor.tag.Contains("Exit"))
            {
                return;
            }
            else
            {
                // [优化] 即使正在去门的路上，如果看到了机器人，也应该立刻变节去跟机器人
                // 原逻辑只检查了 null，这里建议加强检测频率，或者在上面的 GetCandidate 里做文章
                if (myLeader == null)
                {
                    List<GameObject> leaderCandidates = GetCandidate(new List<string> { "Human", "Robot" }, 360, 8).Item1;

                    // 只要看到机器人，无视当前去普通门的计划，直接切模式
                    if (leaderCandidates.Any(c => c.CompareTag("Robot")))
                    {
                        SwitchBehaviourMode();
                        return;
                    }
                }

                if (myTargetDoor.GetComponent<DoorControl>().isBurnt == false)
                {
                    Vector3 myPosition = transform.position;
                    myPosition.y -= 0.5f;
                    float distanceRemain = Vector3.Distance(myPosition, myDestination);

                    if (distanceRemain > 0.5f)
                    {
                        // 路上顺便看看有没有领导
                        List<GameObject> leaderCandidates = GetCandidate(new List<string> { "Human", "Robot" }, 360, 8).Item1;
                        // 过滤无效领导
                        leaderCandidates = FilterValidLeaders(leaderCandidates);

                        if (leaderCandidates.Count > 0)
                        {
                            SwitchBehaviourMode();
                            return;
                        }
                    }
                    else
                    {
                        myTargetDoor = null;
                        return;
                    }
                }
                else { myTargetDoor = null; }
            }
        }
    }

    // --- 跟随者模式更新逻辑 ---
    private void FollowerUpdate_HF()  // Herd Following(盲目跟随)
    {
        if (myLeader == null)
        {
            // 寻找领导者
            List<GameObject> leaderCandidates = GetCandidate(new List<string> { "Human", "Robot" }, 360, 5).Item1;

            // 使用重构后的过滤函数
            leaderCandidates = FilterValidLeaders(leaderCandidates);

            GameObject targetLeader = null;
            if (leaderCandidates.Count > 0)
            {
                // [修改重点] 优先级逻辑：机器人 > 人类
                // 现实中，人们会优先跟随穿制服的救援人员(Robot)，而不是盲从路人
                var robotLeaders = leaderCandidates.Where(c => c.CompareTag("Robot")).ToList();
                var humanLeaders = leaderCandidates.Where(c => c.CompareTag("Human")).ToList();

                if (robotLeaders.Count > 0)
                {
                    targetLeader = robotLeaders[Random.Range(0, robotLeaders.Count)];
                }
                else if (humanLeaders.Count > 0)
                {
                    targetLeader = humanLeaders[Random.Range(0, humanLeaders.Count)];
                }

                // 绑定领导者逻辑
                if (targetLeader != null)
                {
                    if (targetLeader.CompareTag("Human"))
                    {
                        if (targetLeader.GetComponent<HumanControl>().dazingCountDown < 2)
                        {
                            dazingCountDown = Random.Range(2, 8);
                            return;
                        }
                        else
                        {
                            myLeader = targetLeader;
                            myLeader.GetComponent<HumanControl>().myDirectFollowers.Add(this);
                        }
                    }
                    else if (targetLeader.CompareTag("Robot"))
                    {
                        myLeader = targetLeader;
                        myLeader.GetComponent<RobotControl>().myDirectFollowers.Add(this);
                    }
                }
            }
            else
            {
                // 没有合适的领导者，切回自己带队
                targetLeader = null;
                SwitchBehaviourMode();
            }
        }
        else
        {
            // 已经有领导者了
            // [保护] 防止 myLeader 被销毁导致的空引用
            if (myLeader == null || !myLeader.activeInHierarchy)
            {
                myLeader = null;
                SwitchBehaviourMode();
                return;
            }

            Vector3 leaderPosition = myLeader.transform.position;
            List<GameObject> exitList = GetCandidate(new List<string> { "Exit" }, 360, 30).Item1;

            // 看到出口就单飞
            if (exitList.Count > 0)
            {
                RemoveSelfFromLeader();

                GameObject exit = exitList[0];
                SwitchBehaviourMode();
                myTargetDoor = exit;
                myDestination = GetCrossDoorDestination(exit);
                _myNavMeshAgent.SetDestination(myDestination);
                return;
            }
            else
            {
                // [修改重点] 避免拥挤的跟随逻辑
                // 原代码: targetPosition = leaderPosition - leaderForward * 1f; 
                // 问题: 所有人都会挤在领导者正后方 1米 处的一个点上。

                // 新逻辑: 添加随机扰动，模拟人群围绕
                // 每个人在目标点附近 Random.insideUnitCircle 范围内随机选一个点
                // 这样几十个人跟随也不会看起来像叠罗汉

                Vector3 leaderForward = myLeader.transform.forward;

                // 基础目标在身后 1.2 米
                Vector3 baseTarget = leaderPosition - leaderForward * 1.2f;

                // 加上随机偏移 (X, Z 平面)
                // 使用 Hash Code 保证每一帧对同一个人的偏移是相对稳定的，或者直接 Random 也可以(会导致抖动)
                // 这里为了简单直接用 Random，NavMeshAgent 会平滑掉一部分抖动
                Vector3 randomOffset = new Vector3(Random.Range(-0.8f, 0.8f), 0, Random.Range(-0.8f, 0.8f));

                _myNavMeshAgent.SetDestination(baseTarget + randomOffset);
            }
        }
    }

    // --- 辅助函数：从领导者列表中移除自己 ---
    private void RemoveSelfFromLeader()
    {
        if (myLeader == null) return;

        if (myLeader.CompareTag("Robot"))
        {
            var rc = myLeader.GetComponent<RobotControl>();
            if (rc != null) rc.myDirectFollowers.Remove(this);
        }
        else if (myLeader.CompareTag("Human"))
        {
            var hc = myLeader.GetComponent<HumanControl>();
            if (hc != null) hc.myDirectFollowers.Remove(this);
        }
    }

    // --- 辅助函数：统一的领导者筛选逻辑 ---
    // 提取出来，避免 FollowerUpdate 和 LeaderUpdate 写两遍重复的 Lambda
    private List<GameObject> FilterValidLeaders(List<GameObject> rawCandidates)
    {
        return rawCandidates.Where(candidate =>
            candidate != gameObject &&  // 排除自己
            (
                candidate.CompareTag("Robot") ||  // 机器人总是可以跟
                (candidate.CompareTag("Human") && candidate.GetComponent<HumanControl>().myLeader == null) // 只能跟"没有领导"的人(避免 A跟B，B跟A 的死循环)
            )
        ).ToList();
    }

    // ... (GetCrossDoorDestination, SwitchBehaviourMode, GetCandidate, FilterTargetDoorCandidates 保持不变) ...

    private Vector3 GetCrossDoorDestination(GameObject targetDoor)
    {
        Vector3 myPosition = transform.position;
        if (targetDoor.CompareTag("Door"))
        {
            string doorDirection = targetDoor.GetComponent<DoorControl>().doorDirection;
            Vector3 doorPosition = targetDoor.transform.position + new Vector3(0, -1.5f, 0);
            switch (doorDirection)
            {
                case "Vertical":
                    return (myPosition.z < doorPosition.z) ? doorPosition + new Vector3(0, 0, 2) : doorPosition - new Vector3(0, 0, 2);
                case "Horizontal":
                    return (myPosition.x < doorPosition.x) ? doorPosition + new Vector3(2, 0, 0) : doorPosition - new Vector3(2, 0, 0);
                default:
                    return myPosition;
            }
        }
        else if (targetDoor.CompareTag("Exit"))
        {
            return targetDoor.transform.position + new Vector3(0, -1.5f, 0);
        }
        return myPosition;
    }

    private void SwitchBehaviourMode()
    {
        if (myBehaviourMode == "Follower")
        {
            myBehaviourMode = "Leader";
            myTargetDoor = null;
        }
        else
        {
            myBehaviourMode = "Follower";
            myTargetDoor = null;
        }
    }

    private Tuple<List<GameObject>, List<Vector3>> GetCandidate(List<string> targetTags, int visionWidth, int visionDiff)
    {
        List<GameObject> candidateList = new();
        List<Vector3> unknownDirections = new();
        Vector3 myPosition = transform.position;

        // =========================================================
        // [修改重点 1] 动态构建 LayerMask，支持多层混合检测
        // =========================================================
        List<string> layersToCheck = new List<string>();

        // 1. 如果目标包含 门 或 出口 -> 需要检测 "Default" 层
        if (targetTags.Contains("Door") || targetTags.Contains("Exit"))
        {
            layersToCheck.Add("Default");
        }

        // 2. 如果目标包含 机器人 或 人类 -> 需要检测 "Follower" 层
        if (targetTags.Contains("Robot") || targetTags.Contains("Human"))
        {
            layersToCheck.Add("Follower");
        }

        // [修改重点 2] 防止透视眼 (X-Ray Vision)
        // 即使我们只找机器人(Follower层)，也必须检测墙壁(Default层)。
        // 否则射线会穿透墙壁直接检测到墙后的机器人，这是不合理的。
        if (!layersToCheck.Contains("Default"))
        {
            layersToCheck.Add("Default");
        }

        // 将字符串列表转换为 Unity 的层级掩码 (int)
        // LayerMask.GetMask 可以接受多个字符串参数
        int finalMask = LayerMask.GetMask(layersToCheck.ToArray());

        // =========================================================
        // [修改重点 3] 射线检测循环
        // =========================================================
        foreach (Vector3 vision in GetVision(visionWidth, visionDiff))
        {
            // 使用混合后的 finalMask 发射射线
            if (Physics.Raycast(myPosition, vision, out RaycastHit hit, visionLimit, finalMask))
            {
                // 击中物体后，再次确认 Tag 是否是我们真正想要的
                // (因为射线也会击中 Default 层的墙壁，但墙壁的 Tag 不是我们现在的目标，会被这里过滤掉)
                if (targetTags.Contains(hit.transform.tag) && !candidateList.Contains(hit.transform.gameObject))
                {
                    candidateList.Add(hit.transform.gameObject);
                }
            }
            else
            {
                // 射线没打中任何东西（既没打中墙，也没打中人），说明这个方向是空旷未知的
                unknownDirections.Add(vision);
            }
        }

        return Tuple.Create(candidateList, unknownDirections);
    }

    private GameObject FilterTargetDoorCandidates(ref List<GameObject> targetDoorCandidates, string filterMode)
    {
        GameObject exit = null;
        if (targetDoorCandidates.Count > 0)
        {
            for (int i = targetDoorCandidates.Count - 1; i >= 0; i--)
            {
                if (targetDoorCandidates[i].CompareTag("Exit"))
                {
                    exit = targetDoorCandidates[i];
                    break;
                }
                if (filterMode is "Explore" && _doorMemoryQueue.Contains(targetDoorCandidates[i]))
                {
                    targetDoorCandidates.RemoveAt(i);
                }
            }
        }
        if (exit == null && targetDoorCandidates.Count > 1 && filterMode is "Normal")
        {
            for (int i = targetDoorCandidates.Count - 1; i >= 0; i--)
            {
                if (targetDoorCandidates[i] == lastDoorWentThrough)
                {
                    targetDoorCandidates.RemoveAt(i);
                }
            }
        }
        return exit;
    }
}