using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;
using Unity.MLAgents;
using UnityEngine.AI;



public partial class EnvControl : MonoBehaviour
{
    // ---------------------------------------------------------
    // 1. 通用清理方法：大幅减少重复代码
    // ---------------------------------------------------------
    private void ClearAndDestroyList<T>(List<T> list) where T : Component
    {
        if (list == null || list.Count == 0) return;

        foreach (var item in list)
        {
            // Unity 重载了 == 运算符，检查 item 是否存在
            if (item != null && item.gameObject != null)
            {
                Destroy(item.gameObject);
            }
        }
        list.Clear();
    }

    private void ResetAgentandClearList()
    {
        // 使用通用方法清理列表
        ClearAndDestroyList(personList);
        ClearAndDestroyList(RobotList);

        // 出口通常带有 Collider 或其他组件，如果 List<GameObject> 则需要单独处理
        // 建议将 Exits 改为 List<Transform> 或 List<GameObject> 的通用处理
        if (Exits.Count > 0)
        {
            foreach (var exit in Exits)
            {
                if (exit != null) Destroy(exit);
            }
            Exits.Clear();
        }

        // 智能体大脑通常不销毁(如果是场景固定物体)，只清空列表引用
        // 如果大脑也是生成的，请使用 ClearAndDestroyList
        HumanBrainList.Clear();

        cachedRoomPositions.Clear();

        // 清理火焰：需要先停止协程
        if (FireList.Count > 0)
        {
            foreach (FireControl fire in FireList)
            {
                if (fire != null)
                {
                    fire.StopAllCoroutines();
                    Destroy(fire.gameObject);
                }
            }
            FireList.Clear();
        }

        // 重置对象池
        if (FirePoolManager.Instance != null)
        {
            FirePoolManager.Instance.ClearPool();
        }
    }

    // ---------------------------------------------------------
    // 2. 机器人生成优化：支持多机器人
    // ---------------------------------------------------------
    public void AddRobot()
    {
        // 建议改为变量控制，方便后续拓展

        for (int i = 0; i < robotCount; i++)
        {
            Vector3 spawnPosition;
            if (isTest && Exits.Count > 0)
            {
                // 测试模式：在出口旁排开，防止重叠
                spawnPosition = Exits[i].transform.position + new Vector3(1 + i, 0, 0);
            }
            else
            {
                spawnPosition = GetRandomPosInLayout();
            }

            GameObject robotObj = Instantiate(RobotPrefab, spawnPosition, Quaternion.identity);

            // 优化：只获取一次 Component
            RobotControl robotCtrl = robotObj.GetComponent<RobotControl>();
            RobotList.Add(robotCtrl);

            robotObj.transform.SetParent(RobotParent.transform);
        }
    }

    // ---------------------------------------------------------
    // 3. 大脑组装优化：自动匹配多个大脑
    // ---------------------------------------------------------
    public void AddRobotBrain()
    {
        // 安全检查：确保身体和脑子数量一致
        int count = Mathf.Min(RobotList.Count, RobotBrainList.Count);

        if (count == 0)
        {
            Debug.LogError("没有找到机器人身体或大脑，无法绑定！");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            RobotBrain brain = RobotBrainList[i];
            RobotControl robotBody = RobotList[i];

            // 【核心修改 4】 只绑定，不注册！
            // 因为 Awake 里已经注册过了，Agent 会一直活在这个 Group 里
            brain.BindRobotBody(robotBody.gameObject);

            // 如果需要重置 Agent 的内部状态（比如奖励归零），可以手动调用
            // brain.EndEpisode(); // 通常 HandleEpisodeReset 里已经调了，这里不用动
        }

        Debug.Log($"[EnvControl] 新回合：已将 {count} 个新身体绑定给大脑。");
    }

    // ---------------------------------------------------------
    // 4. 人类生成优化：移除手动 Start 和 垃圾内存分配
    // ---------------------------------------------------------
    // ---------------------------------------------------------
    // 4. 人类生成优化：支持课程学习 (Curriculum Learning)
    // ---------------------------------------------------------
    public void AddPerson(int num)
    {
        // 1. 获取课程学习参数 (默认 1000f 代表全图)
        float spawnRadius = Academy.Instance.EnvironmentParameters.GetWithDefault("spawn_distance_limit", 1000f);

        for (int i = 0; i < num; i++)
        {
            Vector3 spawnPosition = Vector3.zero;
            bool positionFound = false;

            // --- 策略 A: 测试模式 (指定房间，最高优先级) ---
            if (isTest && complexityControl.buildingGeneration.roomList.Count > i)
            {
                var room = complexityControl.buildingGeneration.roomList[i];
                spawnPosition = new Vector3(
                    room.xzPosition.x + room.width / 2,
                    1.5f,
                    room.xzPosition.z + room.height / 2);
                positionFound = true;
            }
            // --- 策略 B: 课程学习模式 (限制距离) ---
            // 只有当参数小于 999 (即处于课程早期) 且有出口时才启用
            else if (spawnRadius < 999f && Exits.Count > 0)
            {
                spawnPosition = GetRandomPosNearExit(spawnRadius);
                // 如果返回不是零向量，说明找到了有效位置
                if (spawnPosition != Vector3.zero)
                {
                    positionFound = true;
                }
            }

            // --- 策略 C: 全图随机 (保底逻辑) ---
            // 如果不是测试模式，且课程模式找位置失败(比如卡墙里了)，就退化为全图随机
            if (!positionFound)
            {
                spawnPosition = GetRandomPosInLayout();
            }

            // 实例化人类
            GameObject personObj = Instantiate(HumanPrefab, spawnPosition, Quaternion.identity);

            if (personObj.TryGetComponent<HumanControl>(out var humanCtrl))
            {
                personList.Add(humanCtrl);
                humanCtrl.myEnv = this;
                // Unity ML-Agents 的 Agent 通常在 OnEnable 或 Initialize 中初始化
            }

            personObj.transform.SetParent(humanParent.transform);
        }
    }

    /// <summary>
    /// 辅助函数：在任意一个出口的指定半径内寻找 NavMesh 上的点
    /// </summary>
    private Vector3 GetRandomPosNearExit(float radius)
    {
        if (Exits == null || Exits.Count == 0) return Vector3.zero;

        // 尝试 10 次寻找有效位置，避免死循环
        for (int i = 0; i < 10; i++)
        {
            // 1. 随机选一个出口
            GameObject targetExit = Exits[UnityEngine.Random.Range(0, Exits.Count)];

            // 2. 在圆内随机取点
            Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * radius;
            Vector3 candidatePos = targetExit.transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);

            // 3. 采样 NavMesh (防止生成在墙里或门外虚空)
            NavMeshHit hit;
            // 2.0f 是允许的垂直误差，NavMesh.AllAreas 允许所有可行走区域
            if (NavMesh.SamplePosition(candidatePos, out hit, 2.0f, NavMesh.AllAreas))
            {
                return hit.position;
            }
        }

        // 如果 10 次都没找到（太拥挤或半径太小全是墙），返回零向量，外层会降级处理
        return Vector3.zero;
    }

    public void AddHumanBrain(int num)
    {
        // 优化：使用 Tag 查找代替 Find 名字
        GameObject[] humanBrainObjs = GameObject.FindGameObjectsWithTag("HumanBrain");

        // 安全检查
        int count = Mathf.Min(num, humanBrainObjs.Length, personList.Count);

        for (int i = 0; i < count; i++)
        {
            var brain = humanBrainObjs[i].GetComponent<HumanBrain>();
            var human = personList[i];

            HumanBrainList.Add(brain);
            brain.myHuman = human;
            human.myHumanBrain = brain;
          
        }
    }

    // ---------------------------------------------------------
    // 5. 出口查找优化
    // ---------------------------------------------------------
    public void AddExits()
    {
        // 优化：查找所有 Tag 为 Exit 的物体，而不仅仅是名为 "Exit" 的那一个
        GameObject[] exitsFound = GameObject.FindGameObjectsWithTag("Exit");

        if (exitsFound != null && exitsFound.Length > 0)
        {
            Exits.AddRange(exitsFound);
        }
        else
        {
            // 兼容旧逻辑
            GameObject singleExit = GameObject.Find("Exit");
            if (singleExit != null) Exits.Add(singleExit);
        }
    }

    public void AddFire(Vector3 FirePosition)
    {
        // 优化：使用 TryGetComponent 防止空引用异常
        // Y轴微调防止 Z-Fighting
        GameObject fire = FirePoolManager.Instance.GetFire(FirePosition + new Vector3(0, 0.5f, 0), Quaternion.identity, this);

        if (fire != null)
        {
            if (fire.TryGetComponent<FireControl>(out var fireControl))
            {
                FireList.Add(fireControl);
            }
            else
            {
                Debug.LogError("火焰缺少 FireControl 组件，回收对象");
                FirePoolManager.Instance.ReturnFire(fire);
            }
        }
    }

    // ---------------------------------------------------------
    // 6. 随机算法核心修复
    // ---------------------------------------------------------
    public Vector3 GetRandomPosInLayout()
    {
        var rooms = complexityControl.buildingGeneration.roomList;
        if (rooms == null || rooms.Count == 0) return Vector3.zero;

        // 修复：使用 UnityEngine.Random，避免 System.Random 在循环中生成相同种子
        int index = UnityEngine.Random.Range(0, rooms.Count);
        Room room = rooms[index];

        float xMin = room.xzPosition.x + room.width / 4;
        float xMax = room.xzPosition.x + room.width * 3 / 4;
        float zMin = room.xzPosition.z + room.height / 4;
        float zMax = room.xzPosition.z + room.height * 3 / 4;

        // UnityEngine.Random.Range 对 float 是包含 min 和 max 的
        float x = UnityEngine.Random.Range(xMin, xMax);
        float z = UnityEngine.Random.Range(zMin, zMax);

        return new Vector3(x, 0.5f, z);
    }

    public void AddFirePosition()
    {
        // 使用 List 初始化器更整洁
        FirePosition = new List<Vector3>
        {
            new Vector3(27, 0, -6),
            new Vector3(28, 0, 33),
            new Vector3(47, 0, 23)
        };
    }

    private void RecordRoomPosition(List<Room> roomList)
    {
        foreach (Room room in roomList)
        {
            cachedRoomPositions.Add(new Vector3(
                room.xzPosition.x + room.width / 2,
                0,
                room.xzPosition.z + room.height / 2));
        }
    }
}
