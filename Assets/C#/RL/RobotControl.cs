using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class RobotControl : MonoBehaviour
{
    [Header("Status")]
    public bool isRunning = true; // 机器人是否处于工作状态
    public int robotFollowerCounter; // 仅用于 Inspector 显示，逻辑直接用 List.Count

    [Header("References")]
    // 这个引用由 RobotBrain.BindRobotBody() 自动赋值
    public RobotBrain myAgent;
    public List<HumanControl> myDirectFollowers = new List<HumanControl>();

    private NavMeshAgent _botNavMeshAgent;

    private void Awake()
    {
        _botNavMeshAgent = GetComponent<NavMeshAgent>();
        // 确保速度设置正确 (也可以在 Inspector 中设置)
        if (_botNavMeshAgent != null)
        {
            _botNavMeshAgent.speed = 6f;
            _botNavMeshAgent.avoidancePriority = 0; // 机器人优先级最高，推着人走
        }
    }

    private void Start()
    {
        // 确保物体激活
        this.gameObject.SetActive(true);
    }

    private void Update()
    {
        // ---------------------------------------------------------
        // 1. 自动维护跟随者列表 (Lazy Cleanup)
        // ---------------------------------------------------------
        // 防止 HumanControl 销毁后，这里还留着空引用
        // 倒序遍历以安全移除元素
        for (int i = myDirectFollowers.Count - 1; i >= 0; i--)
        {
            if (myDirectFollowers[i] == null || !myDirectFollowers[i].isActiveAndEnabled)
            {
                myDirectFollowers.RemoveAt(i);
            }
        }

        // 更新计数器供 Brain 观测
        robotFollowerCounter = myDirectFollowers.Count;

        // ---------------------------------------------------------
        // 注意：移除了 myAgent.robotPosition = this.transform.position
        // 原因：RobotBrain 已经持有 Robot 的引用，它可以直接访问 transform，
        // 不需要这里每帧去“推送”数据，这是一种更高效的架构。
        // ---------------------------------------------------------
    }

    private void OnTriggerEnter(Collider other)
    {
        // 【安全检查】如果大脑还没绑定，不要处理逻辑
        if (myAgent == null || !myAgent.RobotIsInitialized) return;

        GameObject triggerObject = other.gameObject;

        if (other.CompareTag("Fire"))
        {
            // Debug.Log("机器人碰到火焰");

            // 增加卡死计数，促使 Agent 尝试改变策略
            myAgent.stuckCounter += 10;

            // 给予惩罚
            myAgent.AddReward(-0.8f);

            // 如果你在 Brain 里实现了日志系统，可以调用，但要注意判空
            // myAgent.LogReward("机器人触碰火焰惩罚", -5);
        }
    }

    // ---------------------------------------------------------
    // 辅助方法：供 HumanControl 调用
    // ---------------------------------------------------------

    /// <summary>
    /// 当人类决定跟随机器人时调用
    /// </summary>
    public void AddFollower(HumanControl human)
    {
        if (!myDirectFollowers.Contains(human))
        {
            myDirectFollowers.Add(human);
        }
    }

    /// <summary>
    /// 当人类恐慌或到达出口离开时调用
    /// </summary>
    public void RemoveFollower(HumanControl human)
    {
        if (myDirectFollowers.Contains(human))
        {
            myDirectFollowers.Remove(human);
        }
    }
}