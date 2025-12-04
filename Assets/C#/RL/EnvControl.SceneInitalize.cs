using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public partial class EnvControl : MonoBehaviour
{
    private void ResetAgentandClearList()
    {
        // 清除人类
        if (personList.Count > 0)
        {
            foreach (HumanControl person in personList)
            {
                if (person != null && person.gameObject != null)
                {
                    Destroy(person.gameObject);
                }
            }
            personList.Clear();
        }

        // 清除机器人
        if (RobotList.Count > 0)
        {
            foreach (RobotControl robot in RobotList)
            {
                if (robot != null && robot.gameObject != null)
                {
                    Destroy(robot.gameObject);
                }
            }
            RobotList.Clear();
        }

        // 清除智能体，只清空智能体列表即可，实体不用清除
        if (RobotBrainList.Count > 0)
        {
            RobotBrainList.Clear();
        }
        //清除智能体，只清空智能体列表即可，实体不用清除
        if (HumanBrainList.Count > 0)
        {
            HumanBrainList.Clear();
        }
        

        // 清除出口
        if (Exits.Count > 0)
        {
            foreach (var exit in Exits)
            {
                if (exit != null && exit.gameObject != null)
                {
                    Destroy(exit.gameObject);
                }
            }
            Exits.Clear();
        }
        cachedRoomPositions.Clear();

        if (FireList.Count > 0)  //清除三个火源
        {
            foreach (FireControl fire in FireList)
            {
                if (fire != null && fire.gameObject != null)
                {
                    // 先执行火焰的自定义清理逻辑
                    fire.StopAllCoroutines();

                    // 直接从场景销毁，不返回对象池（因为整个场景要重置）
                    Destroy(fire.gameObject);
                }
            }
            FireList.Clear();
        }
        if (FirePoolManager.Instance != null)
        {
            FirePoolManager.Instance.ClearPool(); // 需要实现这个方法
        }
    }
    public void AddRobot()  //在这里动态添加机器人，以确保机器人是在环境生成之后才添加上去的，以确保机器人导航的正常使用
    {
        // 在场景中生成num个机器人，并把他们加入到List中
        Vector3 spawnPosition = Vector3.zero;
        // 尝试找到一个没有碰撞的位置
        // 随机生成位置
        //标注掉的只适用于矩形布局
        /*float randomX = UnityEngine.Random.Range(1f, complexityControl.buildingGeneration.totalWidth);
        float randomZ = UnityEngine.Random.Range(1f, complexityControl.buildingGeneration.totalHeight);*/

        if (isTest)
        {
            print("测试环节，将机器人放在出口右侧");
            spawnPosition = Exits[0].transform.position + new Vector3(1, 0, 0);
        }
        else { spawnPosition = GetRandomPosInLayout(); }
        // 实例化机器人
        GameObject Robot = Instantiate(RobotPrefab, spawnPosition, Quaternion.identity);//实例化机器人的位置
        RobotList.Add(Robot.GetComponent<RobotControl>()); //将机器人加入列表
        Robot.transform.parent = RobotParent.transform;    //将机器人放在场景的RobotList物体下
    }

    public void AddRobotBrain()  //在这里添加机器人大脑. 并进行组装  机器人的数量较少而且确定，因此直接在场景中进行添加
    {
        GameObject RobotBrain = GameObject.Find("RobotBrain1");
        RobotBrain robotBrain = RobotBrain.GetComponent<RobotBrain>();
        RobotBrainList.Add(robotBrain);
        /*  在这里进行机器人和大脑的脚本初始化工作！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！*/
        RobotControl robot = RobotList[0];
        robot.myAgent = robotBrain;
        robotBrain.robot = robot.gameObject;
        robotBrain.robotNavMeshAgent = robot.GetComponent<NavMeshAgent>();
        robotBrain.robotInfo = robot;
        robotBrain.robotRigidbody = robot.GetComponent<Rigidbody>();
        robotBrain.robotDestinationCache = robot.gameObject.transform.position;
        robotBrain.stuckCounter = 0;
        robotBrain.SignalcostTime = 0;
        robotBrain.TotalcostTime = 0;
        robotBrain._humanHealthObservation = 100;
        robotBrain.robotPosition = robot.gameObject.transform.position;
        RobotBrainList[0].RobotIsInitialized = true;//初始化已完成，可以执行后续函数
    }
    public void AddPerson(int num)
    {
        // print("添加人类函数");
        // 在场景中生成num个人类，并把他们加入到personList中
        Vector3 [] RoomPosition=new Vector3[10];


        for (int i = 0; i < num; i++)
        {
            Vector3 spawnPosition = Vector3.zero;

            
               
            if (isTest)
            {
                print("测试环节，将人类放在前10个房间中心");
                spawnPosition = new Vector3(complexityControl.buildingGeneration.roomList[i].xzPosition.x+ complexityControl.buildingGeneration.roomList[i].width/2,
                                             1.5f,
                                             complexityControl.buildingGeneration.roomList[i].xzPosition.z + complexityControl.buildingGeneration.roomList[i].height / 2);
                                     //固定在前10个房间的中间
            }
            else
            {  // 尝试找到一个没有碰撞的位置
                spawnPosition = GetRandomPosInLayout();
            }
            // 实例化人类
            GameObject Person = Instantiate(HumanPrefab, spawnPosition, Quaternion.identity);
            personList.Add(Person.GetComponent<HumanControl>());
            Person.transform.parent = humanParent.transform;
            Person.GetComponent<HumanControl>().myEnv = this;
            Person.GetComponent<HumanControl>().Start();//初始化人类的各个变量  

        }

        // Debug.Log("执行生成人类函数");
    }
    public void AddHumanBrain(int num)//找到场景中的人类大脑预制体，并将其与人类进行挂载，控制人类的状态
    {
        //2025.5.3新增，进行人类智能体的编写
        //print("执行添加人类大脑函数");
       GameObject[] HumanBrains = GameObject.FindGameObjectsWithTag("HumanBrain");
        //但是设置为“unActive”状态之后就找不到了
       // print("场景中的人类大脑数量为"+HumanBrains.Length);

        for (int i = 0; i <num; i++)
        {
           // print("大脑名称为：" + HumanBrains[i].name);
            HumanBrainList.Add(HumanBrains[i].GetComponent<HumanBrain>());
            //将大脑添加到环境中的人类大脑列表
            HumanBrains[i].GetComponent<HumanBrain>().myHuman = personList[i];
            personList[i].myHumanBrain = HumanBrains[i].GetComponent<HumanBrain>();
            //将对应的人类物体告诉相应的大脑
            HumanBrains[i].GetComponent<HumanBrain>().HumanIsInitialized = true;
            //人类和其大脑已经相互挂载
            //HumanBrains[i].GetComponent<HumanBrain>().myEnv
        }
        


    }
    public void AddExits() //将出口添加到出口列表当中
    {
        GameObject Exit = GameObject.Find("Exit");
        if (Exit != null)
        {
            Exits.Add(Exit);
            //   Debug.Log("执行添加出口函数，赋值");
        }
        else { Debug.Log("没有找到出口的游戏物体"); }

    }

    public void AddFire(Vector3 FirePosition) //添加火焰，只需传入x，z坐标即可
    {
        //随机生成火焰位置
        /*Vector3 spawnPosition = Vector3.zero;
        spawnPosition = GetRandomPosInLayout();
        spawnPosition.x = Mathf.Round(spawnPosition.x);
        spawnPosition.z = Mathf.Round(spawnPosition.z);//四舍五入取整*/
        // 检查是否达到最大火焰数量
        // 使用对象池安全获取
        GameObject fire = FirePoolManager.Instance.GetFire(FirePosition + new Vector3(0, 0.5f, 0), Quaternion.identity, this);

        if (fire != null)
        {
            var fireControl = fire.GetComponent<FireControl>();
            if (fireControl != null)
            {
                FireList.Add(fireControl);
            }
            else
            {
                Debug.LogError("生成的火焰缺少FireControl组件");
                FirePoolManager.Instance.ReturnFire(fire);
            }
        }

    }
    /*public void AddSmoke(Vector3 FirePosition) //添加火焰，只需传入x，z坐标即可
    {
        // 参数校验
        if (SmokePrefab == null)
        {
            Debug.LogError("烟雾预制体未赋值！", this);
            return;
        }

        // 设置生成位置（保持原有XZ坐标，添加Y偏移）
        Vector3 spawnPosition = new Vector3(
            FirePosition.x,
             0,
            FirePosition.z
        );

        // 生成烟雾
        GameObject smokeInstance = Instantiate(
            SmokePrefab,
            spawnPosition,
            Quaternion.identity
        );

        GameObject.Find("SmokeList");
        // 可选：设置父物体（保持层级整洁）
        smokeInstance.transform.SetParent(transform);
    }*/
    /// <summary>
    /// 获取房间内无碰撞的随机位置
    /// 加权随机分布,较大概率生成在大房间内部，但又保证房间的随机性
    /// </summary>
    public Vector3 GetRandomPosInLayout()
    {
        //随机选取一个房间，在随机房间内选取随机点
        List<Room> rooms = complexityControl.buildingGeneration.roomList;
        if (rooms == null || rooms.Count == 0)
        {
            Debug.LogError("房间列表为空！");
            return Vector3.zero;
        }
        System.Random _random = new System.Random();
        // _random.Next(maxValue)
        //作用：返回 0 到 maxValue-1 之间的随机整数
        //NextDouble() 生成的是一个范围在 [0, 1) 的浮动值
        Room room = rooms[_random.Next(rooms.Count)];
        // 计算有效范围
        float xMin = room.xzPosition.x + room.width / 4;
        float xMax = room.xzPosition.x + room.width * 3 / 4; // 正确右边界
        float zMin = room.xzPosition.z + room.height / 4;
        float zMax = room.xzPosition.z + room.height * 3 / 4; // 正确上边界

        float x = xMin + (float)_random.NextDouble() * (xMax - xMin);
        float z = zMin + (float)_random.NextDouble() * (zMax - zMin);

        Vector3 RandomPos = new Vector3(x, 0.5f, z);

        return RandomPos;
    }



    public void AddFirePosition()//找到场景中的左上，右上，右下三个房间，将他们的中心作为三个火源的位置
    {
        /* if (complexityControl.buildingGeneration.roomList == null || complexityControl.buildingGeneration.roomList.Count == 0)
         {
             Debug.LogError("房间列表为空！");
             return;
         }

         // 初始化关键变量（单次遍历完成所有计算）
         Room topLeftRoom = null;
         Room topRightRoom = null;
         Room bottomRightRoom = null;
         float minX = float.MaxValue;
         float maxX = float.MinValue;

         // 单次遍历处理所有逻辑
         foreach (Room room in complexityControl.buildingGeneration.roomList)
         {
             float x = room.xzPosition.x;
             float z = room.xzPosition.z;

             // 更新左上房间逻辑
             if (x < minX || (x == minX && z > topLeftRoom?.xzPosition.z))
             {
                 minX = x;
                 topLeftRoom = room;
             }
             else if (x == minX && z > topLeftRoom.xzPosition.z)
             {
                 topLeftRoom = room;
             }

             // 更新右上和右下房间逻辑
             if (x > maxX)
             {
                 maxX = x;
                 topRightRoom = room;
                 bottomRightRoom = room;
             }
             else if (x == maxX)
             {
                 // 更新右上（取Z最大）
                 if (z > topRightRoom.xzPosition.z)
                 {
                     topRightRoom = room;
                 }
                 // 更新右下（取Z最小）
                 if (z < bottomRightRoom.xzPosition.z)
                 {
                     bottomRightRoom = room;
                 }
             }
         }

         // 计算中心点
         Vector3 topLeftCenter = CalculateRoomCenter(topLeftRoom);
         Vector3 topRightCenter = CalculateRoomCenter(topRightRoom);
         Vector3 bottomRightCenter = CalculateRoomCenter(bottomRightRoom);

         FirePosition.Add(topLeftCenter);
         FirePosition.Add(topRightCenter); 
         FirePosition.Add(bottomRightCenter);*/  //计算三个角房间的位置坐标


        //临时添加两个火焰的位置，根据Pyrosim中的火焰位置进行
        FirePosition.Add(new Vector3(27,0,-6));
        FirePosition.Add(new Vector3(28, 0, 33));
        FirePosition.Add(new Vector3(47, 0, 23));

    }

    // 保持原有中心计算方法
    private Vector3 CalculateRoomCenter(Room room) =>
        new Vector3(
            room.xzPosition.x + room.width / 2f,
            room.xzPosition.y,
            room.xzPosition.z + room.height / 2f
        );

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
