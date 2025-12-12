using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;
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
        RobotBrainList.Clear();
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
                spawnPosition = Exits[0].transform.position + new Vector3(1 + i, 0, 0);
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
        // 优化：不再通过名字 Find("RobotBrain1")，而是通过 Tag 查找所有大脑
        // 请确保预制体 Tag 设置为 "RobotBrain"
        GameObject[] brainObjs = GameObject.FindGameObjectsWithTag("RobotBrain");

        // 排序以保证确定性（避免每次运行匹配顺序不同）
        brainObjs = brainObjs.OrderBy(g => g.name).ToArray();

        int count = Mathf.Min(RobotList.Count, brainObjs.Length);

        for (int i = 0; i < count; i++)
        {
            RobotBrain brain = brainObjs[i].GetComponent<RobotBrain>();
            RobotControl robot = RobotList[i];

            RobotBrainList.Add(brain);

            // 双向绑定
            robot.myAgent = brain;
            brain.robot = robot.gameObject;
            brain.robotNavMeshAgent = robot.GetComponent<NavMeshAgent>();
            brain.robotInfo = robot;
            brain.robotRigidbody = robot.GetComponent<Rigidbody>();
            brain.robotDestinationCache = robot.transform.position;

            // 重置状态
            brain.stuckCounter = 0;
            brain.SignalcostTime = 0;
            brain.TotalcostTime = 0;
            brain._humanHealthObservation = 100;
            brain.robotPosition = robot.transform.position;
            brain.RobotIsInitialized = true;
        }
    }

    // ---------------------------------------------------------
    // 4. 人类生成优化：移除手动 Start 和 垃圾内存分配
    // ---------------------------------------------------------
    public void AddPerson(int num)
    {
        // 移除：Vector3[] RoomPosition = new Vector3[10]; // 从未使用的内存分配

        for (int i = 0; i < num; i++)
        {
            Vector3 spawnPosition;

            // 测试模式：安全检查防止索引越界
            if (isTest && complexityControl.buildingGeneration.roomList.Count > i)
            {
                var room = complexityControl.buildingGeneration.roomList[i];
                spawnPosition = new Vector3(
                    room.xzPosition.x + room.width / 2,
                    1.5f,
                    room.xzPosition.z + room.height / 2);
            }
            else
            {
                spawnPosition = GetRandomPosInLayout();
            }

            GameObject personObj = Instantiate(HumanPrefab, spawnPosition, Quaternion.identity);

            // 优化：TryGetComponent 稍微快一点，且结构更清晰
            if (personObj.TryGetComponent<HumanControl>(out var humanCtrl))
            {
                personList.Add(humanCtrl);
                humanCtrl.myEnv = this;
                // 移除：humanCtrl.Start(); -> Unity 会自动调用，手动调用会导致初始化两次！
            }

            personObj.transform.SetParent(humanParent.transform);
        }
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
            brain.HumanIsInitialized = true;
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
