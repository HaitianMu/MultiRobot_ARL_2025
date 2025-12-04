using System.Collections.Generic;
using Unity.MLAgents;
using UnityEngine;
using UnityEngine.AI;

public partial class Env : MonoBehaviour
{
    // 机器人控制智能体列表
    public List<MultiAgent> agentList = new();

    // 人类列表
    public List<Person> personList = new();

    // 临时火焰列表
    public List<GameObject> tmpFireList = new();

    // 被火焰摧毁的门的列表
    public List<GameObject> disabledDoorList = new();

    public List<GameObject> Exits;//环境中的出口

    // MA-POCA智能体组
    public SimpleMultiAgentGroup agentGroup;

    // 人类计数器
    [HideInInspector] public int humanCounter;

    // 场景重置计数器
    [HideInInspector] public int resetCountDown;

    // 每局游戏的帧数计数器
    [HideInInspector] public int stepCounter;

    // 累计奖励计数器
    [HideInInspector] public float[] episodeTotalRewardCounter = new float[3];

    // 是否在训练
    public bool isTraining;

    // 是否使用火源生成器
    public bool useFireAgent;

    // 是否使用强化学习机器人
    public bool useFloorAgent;

    // 是否使用机器人
    public bool useRobot;

    private StatsRecorder m_StatsRecorder;///ADD
    public int hardness;///ADD
    public int fire_human_count;///ADD
    public double Eva;//ADD
    //public double frontE = 0;//ADD
   


    private void Start()
    {
        //print("Env环境初始化脚本开始了");
        agentGroup = new SimpleMultiAgentGroup();
        m_StatsRecorder = Academy.Instance.StatsRecorder;///ADD
        hardness = 0;///ADD
        fire_human_count = 0;//ADD
        Eva = 0;//ADD
        AddRobot();
        AddPerson();
        AddExits();
        foreach (MultiAgent agent in agentList)//这里可能也要改
        {
            agentGroup.RegisterAgent(agent);
            agent.myEnv = this;
        }

        resetCountDown = 50;
        stepCounter = 0;
        episodeTotalRewardCounter = new float[] { 0, 0, 0 };
        CleanTheScene();
    }

    /// <summary>
    /// 获取随机出生位置（）
    /// </summary>
    /// <param name="floor"></param>
    /// <returns></returns>
    private static Vector3 GetSpawnBlockPosition(int floor)//！！！！！！！！奶奶的，坐标的问题也要注意一下,找没有父物体的查看世界坐标
    {
        Vector3 spawnBlockPosition = new();
        for (int tryCounter = 80000; tryCounter > 0; tryCounter--)
        {
            // Generate random X and Z coordinates within the (0, 10) range.
            float randomX = Random.Range(1, 9) + 0.5f;
            float randomZ = Random.Range(1, 9) + 0.5f;

            // 跳过该区域
            if (floor == 1 && randomX is > 5 and < 8 && randomZ is > 2 and < 5)
                continue;

            // Set the spawn position at the appropriate floor level
            spawnBlockPosition.Set(randomX, (floor - 1) * 4, randomZ);

            // Check if the spawn position is valid (no collision)
            if (Physics.CheckBox(spawnBlockPosition + Vector3.up, new Vector3(0.49f, 0.49f, 0.49f)) is false)

                return spawnBlockPosition;
        }
        print("生成的随机位置是" +spawnBlockPosition);
        // Return a default value if no valid position is found
        return new Vector3();
    }

    private void CleanTheScene()
    {
        ///add
        ///
        fire_human_count = 0; //reset
        Eva = 0;//ADD
        ///end
        //print("执行CleanScene函数");
        ResetAllAgents();

        /*foreach (Person person in personList)
            person.gameObject.SetActive(false);*/
    }
    /// <summary>
    /// 重置所有机器人控制智能体
    /// </summary>
    private void ResetAllAgents()
    {
        if (useRobot is false)
            return;
        foreach (MultiAgent agent in agentList)
        {
            //print("执行所有机器人重置函数");
            //agent.gameObject.SetActive(true);
            //agent.robotInfo.gameObject.SetActive(false);//ADD
            //print("1");
            agent.ResetAgent();
            //print("2");
            agent.gameObject.SetActive(true);
            agent.robotInfo.gameObject.SetActive(true);
            agent.robotInfo.Start();
        }
    }
    /// <summary>
    /// 重置所有人类
    /// </summary>
    public void ResetAllHumans()
    {
        Debug.Log("进入了重置人类的函数");
        foreach (Person human in personList)
        {
            Debug.Log("重置时找到了列表中的人类"+human);
            int randomFloor = 1;
            Vector3 spawnPosition = GetSpawnBlockPosition(randomFloor) + new Vector3(0, 1, 0);

            if (spawnPosition == Vector3.zero)
                continue;
            if (!human.gameObject.activeSelf)
            {
                human.gameObject.SetActive(true);
                human.transform.SetPositionAndRotation(spawnPosition, Quaternion.identity);
            }  // 激活人物}
            human.Start();
        }
    }

    private void AddRobot() {//把所有机器人加入agentlist，然后实现机器人和机器人大脑之间的变量挂载
        //Find函数无法递归向下寻找
        //print("执行加入机器人函数");
        //找到挂载多智能体脚本的物体
        Transform Multiagent = transform.Find("MultiAgent");
       // print(Multiagent.name);
        agentList.Add(Multiagent.GetComponent<MultiAgent>());
        //找到所有的机器人，并存入robots数组
        Transform robotList = transform.Find("RobotList");
        //print(robotList.name);
       // print("机器人的数量为："+robotList.childCount);
        Robot[] robots=new Robot[robotList.childCount];


        //一大堆赋值工作，机器人代理和机器人之间的相互赋值
        for (int i = 0; i < robotList.childCount; i++)
        {
            robots[i] = robotList.GetChild(i).GetComponent<Robot>();
            Multiagent.GetComponent<MultiAgent>().robot = robots[i].gameObject;
            Multiagent.GetComponent<MultiAgent>().robotRigidbody = robots[i].GetComponent<Rigidbody>();
            Multiagent.GetComponent<MultiAgent>().robotInfo = robots[i];
            Multiagent.GetComponent<MultiAgent>().robotNavMeshAgent = robots[i].GetComponent<NavMeshAgent>();
            Multiagent.GetComponent<MultiAgent>().myEnv = this;
            robots[i].myAgent= Multiagent.GetComponent<MultiAgent>();
        }
    
        //print("机器人代理挂载的机器人名字是" + Multiagent.GetComponent<MultiAgent>().robot.gameObject.name);
        //将机器人挂载到多智能体上，后续根据需要看是否将，多智能体属性中的robotAgent扩展到robotAgents
    }
    private void AddPerson()
    {//把多智能体加入agentlist，然后将每一个机器人加到这个多智能体的robotAgeent属性当中
        //print("执行加入人类函数");
        //找到挂载多智能体脚本的物体
        Transform humanList = transform.Find("HumanList");
        for (int i = 0; i < humanList.childCount; i++)
        {
           personList.Add(humanList.GetChild(i).GetComponent<Person>());
            humanList.GetChild(i).GetComponent<Person>().myEnv = this;
        }
        
    }
    private void AddExits() {
        Transform ExitList = transform.Find("Building").Find("Exit");
        //GameObject[] targets = GameObject.FindGameObjectsWithTag("tag");//返回tag相同的所有物体,,,find函数消耗性能较大
        for (int i = 0; i < ExitList.childCount; i++)
        {
           Exits.Add(ExitList.GetChild(i).gameObject);
        }
    }
}
