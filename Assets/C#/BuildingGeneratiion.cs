using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;
public class BuildingGeneratiion : MonoBehaviour
{

    /*........................一、房间生成用到的数据结构.....................................*/
    public float[] roomAreas;// 输入的房间面积数组（已知数组）
    public int num = 1;//记录已经生成的房间数量,用于给房间编号  
    private float totalArea; // 总区域大小
    public int totalWidth;//用于记录整个区域的宽
    public int totalHeight;//用于记录整个区域的高
    float y = 3.0f;//墙体的高度 ,
    float doorWidth = 1.5f;//门的宽度
    private GameObject[] generatedRooms; //存储生成的房间对象，这个数据用于多次生成环境时，删除之前生成的object

    public Material Floor ;
    public Material Door;
    public Material Exit;
    public Material Wall;
    /*.............................二、房间之间生成门用到的数据结构................................*/
    public class Room
    {
        //为房间增加一些属性，比如房间的坐标、大小，以及它的邻居列表（用于记录相邻房间）。
        public GameObject roomObject;  // 房间的游戏对象
        public Vector3 ZXposition;       // 房间的左下角坐标
        public float width;            // 房间的宽度
        public float height;           // 房间的高度

        public Room(GameObject roomObject, Vector3 position, float width, float height)
        {
            this.roomObject = roomObject;
            this.ZXposition = position;
            this.width = width;
            this.height = height;
        }
        // 方法：检查与另一个房间是否相邻（基于房间位置和大小）
        public bool IsAdjacentTo(Room other)  //判断该房间是否与另外一个房间相邻,当两个房间相邻的长度大于Distance时，我们才认为这两个房间相邻
        {
            // 假设房间是矩形的，我们检查是否有一个相邻的面
            float Distance = 2.0f; 
            bool isAdjacent = false;

            // 检查左右相邻（假设x为水平方向，y为垂直方向，z为深度方向）
            if (Mathf.Abs(this.ZXposition.x + this.width - other.ZXposition.x) < 0.1f || Mathf.Abs(other.ZXposition.x + other.width - this.ZXposition.x) < 0.1f)
            {
                // 如果两个房间在 x 轴方向上相邻,检查两个房间相邻部分在z轴方向的差值.如果<2，那么我们不认为这两个房间是相邻的,因为有 1 的距离要用来放门
                //总体可分为三种情况
                if (other.ZXposition.z <= this.ZXposition.z && other.ZXposition.z+other.height>=this.ZXposition.z && other.ZXposition.z+other.height-this.ZXposition.z >= Distance)
                {
                    isAdjacent = true;
                    return isAdjacent;
                }
                else if(other.ZXposition.z <= this.ZXposition.z+height && other.ZXposition.z + other.height >= this.ZXposition.z+height && this.ZXposition.z + this.height - other.ZXposition.z >= Distance)
                {
                    isAdjacent = true;
                    return isAdjacent;
                }
                else if(other.ZXposition.z >= this.ZXposition.z && other.ZXposition.z + other.height <= this.ZXposition.z + this.height && other.height >= Distance)
                {
                    isAdjacent = true;
                    return isAdjacent;
                }
                if (other.ZXposition.z <= ZXposition.z || other.ZXposition.z + other.height >= ZXposition.z + height || height >= 0.2)
                {

                }
                else { return isAdjacent; }

            }
            // 检查上下相邻
            else if (Mathf.Abs(this.ZXposition.z + this.height - other.ZXposition.z) < 0.1f || Mathf.Abs(other.ZXposition.z + other.height - this.ZXposition.z) < 0.1f)
            {
                // 如果两个房间在 z 轴方向上相邻，检查两个房间相邻部分在x轴方向的差值.如果<2，那么我们不认为这两个房间是相邻的,因为有 1 的距离要用来放门
                if (other.ZXposition.x<= this.ZXposition.x && other.ZXposition.x+other.width>=this.ZXposition.x && other.ZXposition.x + other.width-this.ZXposition.x>=Distance)
                {
                    isAdjacent = true;
                    return isAdjacent;
                }
                else if (other.ZXposition.x <= this.ZXposition.x+this.width && other.ZXposition.x + other.width >= this.ZXposition.x+this.width && ZXposition.x + width - other.ZXposition.x >= Distance)
                {
                    isAdjacent = true;
                    return isAdjacent;
                }
                else if (other.ZXposition.x >= this.ZXposition.x && other.ZXposition.x + other.width <= this.ZXposition.x+width && other.width  >= Distance)
                {
                    isAdjacent = true;
                    return isAdjacent;
                }
                else if (other.ZXposition.x <= ZXposition.x && other.ZXposition.x + other.width >= ZXposition.x + width && width >= 0.2)
                {
                    isAdjacent = true;
                    return isAdjacent;
                }

                else { return isAdjacent; }
            }
            return isAdjacent;
        }

    }
    private List<Room> roomList = new List<Room>();  // 存储生成的所有房间对象

    /*.....................................一、接受UI输入的区域面积和房间数目，然后进行房间的摆放和房间墙壁的生成.......................................*/
    public void GenerateRooms()
    {
        /*foreach (float part in roomAreas)
        {
            Debug.Log("（生成器接受到的数据）" + part);
        }*/
        num = 1;  //重置房间计数器
        ClearPreviousRooms(); // 清除上次生成的房间
        // 计算总面积
        totalArea = 0f;
        foreach (var area in roomAreas)
        {
            totalArea += area;
        }

        // 找出最接近正方形的长宽组合
        FindBestDimensions(totalArea);
        // 输出计算的总区域宽高
        /* Debug.Log("Total Area: " + totalArea);
         Debug.Log("Total Width: " + totalWidth);
         Debug.Log("Total Height: " + totalHeight);*/

        // 使用方形树图生成房间
        CreateRoomRects(roomAreas, 0, roomAreas.Length, 0, 0, totalWidth, totalHeight, totalArea, (totalHeight / (float)totalWidth) > 1);
        // 创建房间之间的门，确保房间连通
        Room [][]CN= GenerateCN(roomList.ToArray());
        //CreateDoorBetweenRooms();
        CreateDoorBetweenRooms(roomList.ToArray(), CN); //根据连通图CN生成门
        //在最后一个房间的左后两个墙中心生成两扇门作为逃生出口
        AddExitDoors(roomList[roomList.Count - 1]);
    }

    void FindBestDimensions(float totalArea)// 找出长宽比最接近1的宽高组合，如果BestRatio>3或<1/3，则将该区域设置为正方形
    {
        int bestWidth = 0;
        int bestHeight = 0;
        float bestRatio = float.MaxValue;  // 最接近1的比值

        // 遍历所有可能的宽度值
        for (int width = 1; width <= Mathf.FloorToInt(Mathf.Sqrt(totalArea)); width++)
        {
            if (totalArea % width == 0)  // 如果可以整除，得到对应的高度
            {
                int height = Mathf.FloorToInt(totalArea / width);

                // 计算当前的长宽比
                float ratio;
                if ((float)width / height > 1)
                {
                    // 长宽比大于1，直接计算差值
                    ratio = Mathf.Abs((float)width / height - 1);
                }
                else
                {
                    // 长宽比小于1，取倒数后计算差值
                    ratio = Mathf.Abs((float)height / width - 1);
                }

                // 如果当前的长宽比更接近1，则更新最佳结果
                if (ratio < bestRatio)
                {
                    bestRatio = ratio;
                    bestWidth = width;
                    bestHeight = height;
                }
            }
        }

        // 计算最终的长宽比
        float finalRatio = (float)bestWidth / bestHeight;

        //如果最佳长宽比大于3或小于1/3。 设置为正方形
        if (finalRatio > 3 || finalRatio < 1 / 3f)
        {
            // 设置为正方形（宽高相等）
            bestWidth = Mathf.FloorToInt(Mathf.Sqrt(totalArea));
            bestHeight = bestWidth;
        }


        // 设置最佳的宽度和高度
        totalWidth = bestWidth;
        totalHeight = bestHeight;
    }
    void CreateRoomRects(float[] areas, int start, int end, float x, float z, float width, float height, float totalArea, bool isHorizontal)
    {
        // 方形树图法划分房间
        //areas 指需要划分的房间，
        //start是当前区域的数组的起始点，end是数组的结束点，
        //x，z分别表示当前划分区域的左下角，width是当前区域的长（x方向），height是当前区域的高（z方向），totalArea是当前区域的总面积大小
        // isHorizontal 如果是1，则表示该区域使用的是竖直划分，即z/x>1;反之则z/x<1，在下一步划分当中采用相反的划分方法


        // 输出当前递归的参数信息到 Unity 控制台
        /* Debug.Log("........................................");
         Debug.Log("Calling CreateRoomRects:");
         Debug.Log($"  start: {start}, end: {end}");
         Debug.Log($"  x: {x}, z: {z}");
         Debug.Log($"  width: {width}, height: {height}");
         Debug.Log($"  totalArea: {totalArea}, isHorizontal: {isHorizontal}");
         Debug.Log("........................................");*/


        if (start >= end) return;
        // 如果只有一个房间，直接生成矩形，不再递归
        if (end - start == 1)
        {
            GameObject room = CreateRoom(x, z, width, height);
            AddRoomToList(room);
            return;
        }

        // 找到最佳划分点，使两部分面积最接近
        // 我们想要找到一个划分点，使得从 start 到 splitIndex 的面积总和尽可能接近 currentArea / 2。
        // 这里最初是采用的方形树图方法，但发现单纯的方形树图会使小房间全部生成在一个角落，这并不符合我们的预期，因此进行了一定的改进
        // 我们依据
        int splitIndex = start;
        float splitArea = 0; //划分后的面积


        float targetArea = totalArea / 5; //目标面积,
                                          //这里暂时将划分点改为总面积的1/10，以便于后面连通图的产生。连通图大致会最多划分为10个区域，所以产生的房间数最少应为10个，2025.1.1 目前还在考虑

        float minDifference = float.MaxValue; // 用来记录与目标面积的最小差距
        float currentArea = 0;//当前划分的面积


        for (int i = start; i < end; i++)//求出划分点
        {
            currentArea += areas[i];  // 累加当前部分的面积
            float currentDifference = Math.Abs(currentArea - targetArea); // 计算与目标面积的差距

            // 如果当前差距小于最小差距，则更新最佳划分点
            if (currentDifference < minDifference)
            {
                minDifference = currentDifference;
                splitIndex = i;
            }
            // 如果当前划分的面积已经超过了目标面积，可以提前终止
            if (currentArea >= targetArea)
            {
                break;
            }
        }

        //求出划分点之前的所有区域面积
        for (int i = start; i <= splitIndex; i++)
        {
            splitArea += areas[i];
        }



        //splitArea划分出来区域的面积
        // 当前方向的划分
        if (isHorizontal)
        {
            // 水平划分：划分高度
            float splitHeight = (splitArea / totalArea) * height;
            // 下半部分区域（从start到splitIndex）
            float currentX = x;
            // 生成每个小区域
            for (int i = start; i <= splitIndex; i++)
            {
                float roomWidth = (areas[i] / splitArea) * width; // 当前小区域的宽度
                GameObject room = CreateRoom(currentX, z, roomWidth, splitHeight);
                AddRoomToList(room);
                currentX += roomWidth; // 更新下一个区域的y坐标
            }
            //递归生成上半部分
            CreateRoomRects(areas, splitIndex + 1, end, x, z + splitHeight, width, height - splitHeight, totalArea - splitArea, !isHorizontal);
        }
        else

        {
            // 垂直划分：划分宽度
            float splitWidth = (splitArea / totalArea) * width;
            // 左半部分区域（从start到splitIndex）
            float currentZ = z;
            // 生成每个小区域，
            for (int i = start; i <= splitIndex; i++)
            {
                float roomHeight = (areas[i] / splitArea) * height; // 当前小区域的高度
                GameObject room = CreateRoom(x, currentZ, splitWidth, roomHeight);
                AddRoomToList(room);
                currentZ += roomHeight; // 更新下一个区域的x坐标
            }
            //递归生成右半部分
            CreateRoomRects(areas, splitIndex + 1, end, x + splitWidth, z, width - splitWidth, height, totalArea - splitArea, !isHorizontal);
        }
    }
    public GameObject CreateRoom(float x, float z, float width, float height)
    {
        // 创建房间的主体
        GameObject room = new GameObject("Room" + num);
        num++;

        // 生成房间的底部（如果需要的话，可以添加底部，作为房间地面）
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "floor";
        floor.transform.parent = room.transform;
        floor.transform.position = new Vector3(x + width / 2, 0f, z + height / 2);
        floor.transform.localScale = new Vector3(width, 0.1f, height); // 底部宽度和高度

        // 给房间底部上色 Assets/Material/Floor.mat
        floor.GetComponent<Renderer>().material = Floor;

        AddRoomToList(floor); // 将地面加入到房间列表中
                              // 生成四个墙壁
        CreateWall(x, z, width, height, room);

        // 将新房间加入房间列表,并记录每个房间的位置和大小
        Room newRoom = new Room(room, new Vector3(x, 0f, z), width, height);
        roomList.Add(newRoom);  // 将新房间加入房间列表


        return room; // 返回生成的房间对象
    }
    void CreateWall(float x, float z, float width, float height, GameObject room)
    {
        // 墙壁的厚度（可以调整，越大墙壁越厚）
        float wallThickness = 0.1f;
        // 创建四面墙体：四个方向
        // 1. 左墙
        GameObject leftWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftWall.name = "leftWall";
        leftWall.transform.parent = room.transform;
        leftWall.transform.position = new Vector3(x + wallThickness / 2, y / 2, z + height / 2);
        leftWall.transform.localScale = new Vector3(wallThickness, y, height);
        leftWall.GetComponent<Renderer>().material = Wall; // 墙壁颜色


        // 2. 右墙
        GameObject rightWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightWall.name = "rightWall";
        rightWall.transform.parent = room.transform;
        rightWall.transform.position = new Vector3(x + width - wallThickness / 2, y / 2, z + height / 2);
        rightWall.transform.localScale = new Vector3(wallThickness, y, height);
        rightWall.GetComponent<Renderer>().material = Wall; // 墙壁颜色

        // 3. 后墙
        GameObject frontWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        frontWall.name = "backWall";
        frontWall.transform.parent = room.transform;
        frontWall.transform.position = new Vector3(x + width / 2, y / 2, z + wallThickness / 2);
        frontWall.transform.localScale = new Vector3(width, y, wallThickness);
        frontWall.GetComponent<Renderer>().material = Wall; // 墙壁颜色

        // 4. 前墙
        GameObject backWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        backWall.name = "frontWall";
        backWall.transform.parent = room.transform;
        backWall.transform.position = new Vector3(x + width / 2, y / 2, z + height - wallThickness / 2);
        backWall.transform.localScale = new Vector3(width, y, wallThickness);
        backWall.GetComponent<Renderer>().material = Wall; // 墙壁颜色

    }
    public void AddRoomToList(GameObject room)// 将生成的房间添加到房间列表
    {
        // 将新生成的房间加入到房间列表中
        Array.Resize(ref generatedRooms, generatedRooms.Length + 1);
        generatedRooms[generatedRooms.Length - 1] = room;
    }
    void ClearPreviousRooms()// 清除上次生成的房间
        {
            // 检查 AllObjects 是否为 null，如果是，初始化为一个空数组
            if (generatedRooms == null)
            {
                generatedRooms = new GameObject[0];
            }

            // 查找所有已经生成的房间对象并销毁
            foreach (var room in generatedRooms)
            {
                if (room != null)
                {
                    Destroy(room);
                }
            }

            // 清空房间列表
            generatedRooms = new GameObject[0];
        }
  

    /*.......................................二、在生成的房间之间生成门.....................................*/
    Room[][] GenerateCN(Room [] rooms) { //生成连通图
        Room[][] connection = new Room[rooms.Length][];
        for (int i = 0; i < rooms.Length; i++)
        {
            // 对每个房间，初始化一个新的邻居列表
            connection[i] = new Room[rooms.Length];
        }
        // 填充连通图
        for (int i = 0; i < rooms.Length; i++)
        {
            for (int j = i + 1; j < rooms.Length; j++)
            {
                if (rooms[i].IsAdjacentTo(rooms[j])) // 如果两个房间相邻
                {
                    connection[i][j] = rooms[j]; // 添加连接
                    connection[j][i] = rooms[i]; // 双向连接

                    Debug.Log(rooms[i].roomObject.name + "和" + rooms[j].roomObject.name+"相邻");//调试用
                }
            }
        }
        return connection;
    }

    void CreateDoorBetweenRooms(Room[] rooms, Room[][] CN) //根据连通图CN生成门
        {

        for (int i = 0; i < rooms.Length; i++)
        {
            for (int j = i; j < rooms.Length; j++)
            {
                if (CN[i][j] != null)//说明两个房间是邻接的，接着判断两个房间是上、下、左、右哪种相邻方式，根据不同的相邻方式进行不同的处理
                {
                    Debug.Log(rooms[i].roomObject.name + "和" + rooms[j].roomObject.name + "相邻");//调试用
                    Vector3 DoorPosition;
                    // 右方相邻
                    if (Mathf.Abs(rooms[i].ZXposition.x + rooms[i].width - rooms[j].ZXposition.x) < 0.1f)
                    {
                       // Debug.Log("在该房间右方相邻");//调试用
                        // 如果两个房间在 x 轴方向上相邻,检查两个房间相邻部分在z轴方向的差值.如果<2，那么我们不认为这两个房间是相邻的,因为有 1 的距离要用来放门
                        //总体可分为三种情况
                        if (rooms[j].ZXposition.z < rooms[i].ZXposition.z && rooms[j].ZXposition.z + rooms[j].height > rooms[i].ZXposition.z && rooms[j].ZXposition.z + rooms[j].height - rooms[i].ZXposition.z >= 0.2)
                        {
                           // Debug.Log("在该房间右方相邻：情况1");//调试用
                            DoorPosition = new Vector3(rooms[i].ZXposition.x + rooms[i].width, y / 2, (rooms[i].ZXposition.z + rooms[j].ZXposition.z + rooms[j].height) / 2);
                           /* DivideWall(rooms[i].roomObject.transform.Find("RightWall"), DoorPosition, "RightWall");
                            DivideWall(rooms[j].roomObject.transform.Find("leftWall"), DoorPosition, "leftWall");*/
                            CreateDoor(DoorPosition, doorWidth, true, "Door");
                        }
                        if (rooms[j].ZXposition.z < rooms[i].ZXposition.z + rooms[i].height && rooms[j].ZXposition.z + rooms[j].height > rooms[i].ZXposition.z + rooms[i].height && rooms[i].ZXposition.z + rooms[i].height - rooms[j].ZXposition.z >= 0.2)
                        {
                           // Debug.Log("在该房间右方相邻：情况2");//调试用
                            DoorPosition = new Vector3(rooms[i].ZXposition.x + rooms[i].width, y / 2, (rooms[j].ZXposition.z + rooms[i].ZXposition.z + rooms[i].height) / 2);
                           /* DivideWall(rooms[i].roomObject.transform.Find("RightWall"), DoorPosition, "RightWall");
                            DivideWall(rooms[j].roomObject.transform.Find("leftWall"), DoorPosition, "leftWall");*/
                            CreateDoor(DoorPosition, doorWidth, true, "Door");
                        }
                        if (rooms[j].ZXposition.z >= rooms[i].ZXposition.z && rooms[j].ZXposition.z + rooms[j].height <= rooms[i].ZXposition.z + rooms[i].height && rooms[j].height >= 0.2)
                        {
                          //  Debug.Log("在该房间右方相邻：情况3");//调试用
                            DoorPosition = new Vector3(rooms[i].ZXposition.x + rooms[i].width, y / 2, rooms[j].ZXposition.z + rooms[j].height / 2);
                           /* DivideWall(rooms[i].roomObject.transform.Find("RightWall"), DoorPosition, "RightWall");
                            DivideWall(rooms[j].roomObject.transform.Find("leftWall"), DoorPosition, "leftWall");*/
                            CreateDoor(DoorPosition, doorWidth, true, "Door");
                        }
                        if (rooms[j].ZXposition.z <rooms[i].ZXposition.z && rooms[j].ZXposition.z + rooms[j].height > rooms[i].ZXposition.z + rooms[i].height && rooms[i].height >= 0.2)
                        {
                         //   Debug.Log("在该房间右方相邻：情况4");//调试用
                            DoorPosition = new Vector3(rooms[i].ZXposition.x + rooms[i].width, y / 2, rooms[i].ZXposition.z + rooms[i].height);
                          /*  DivideWall(rooms[i].roomObject.transform.Find("RightWall"), DoorPosition, "RightWall");
                            DivideWall(rooms[j].roomObject.transform.Find("leftWall"), DoorPosition, "leftWall");*/
                            CreateDoor(DoorPosition, doorWidth, true, "Door");
                        }

                    }
                    //左方相邻
                    if (Mathf.Abs(rooms[i].ZXposition.x - rooms[j].width - rooms[j].ZXposition.x) < 0.1f)
                    {
                        // 如果两个房间在 x 轴方向上相邻,检查两个房间相邻部分在z轴方向的差值.如果<2，那么我们不认为这两个房间是相邻的,因为有 1 的距离要用来放门
                       // Debug.Log("在该房间左方相邻");//调试用
                        //总体可分为三种情况
                        if (rooms[j].ZXposition.z <= rooms[i].ZXposition.z && rooms[j].ZXposition.z + rooms[j].height >= rooms[i].ZXposition.z && rooms[j].ZXposition.z + rooms[j].height - rooms[i].ZXposition.z >= 0.2)
                        {
                          //  Debug.Log("左方相邻第一种情况");//调试用
                            DoorPosition = new Vector3(rooms[i].ZXposition.x, y / 2, (rooms[i].ZXposition.z + rooms[j].ZXposition.z + rooms[j].height) / 2);
                         /*   DivideWall(rooms[i].roomObject.transform.Find("RightWall"), DoorPosition, "RightWall");
                            DivideWall(rooms[j].roomObject.transform.Find("leftWall"), DoorPosition, "leftWall");*/
                            CreateDoor(DoorPosition, doorWidth, true, "Door");
                        }
                        if (rooms[j].ZXposition.z <= rooms[i].ZXposition.z + rooms[i].height && rooms[j].ZXposition.z + rooms[j].height >= rooms[i].ZXposition.z + rooms[i].height && rooms[i].ZXposition.z + rooms[i].height - rooms[j].ZXposition.z >= 0.2)
                        {
                           // Debug.Log("左方相邻第二种情况");//调试用
                            DoorPosition = new Vector3(rooms[i].ZXposition.x, y / 2, (rooms[j].ZXposition.z + rooms[i].ZXposition.z + rooms[i].height) / 2);
                            /*DivideWall(rooms[i].roomObject.transform.Find("RightWall"), DoorPosition, "RightWall");
                            DivideWall(rooms[j].roomObject.transform.Find("leftWall"), DoorPosition, "leftWall");*/
                            CreateDoor(DoorPosition, doorWidth, true, "Door");
                        }
                        if (rooms[j].ZXposition.z >= rooms[i].ZXposition.z && rooms[j].ZXposition.z + rooms[j].height <= rooms[i].ZXposition.z + rooms[i].height && rooms[j].height >= 0.2)
                        {
                          //  Debug.Log("左方相邻第三种情况");//调试用
                            DoorPosition = new Vector3(rooms[i].ZXposition.x, y / 2, rooms[j].ZXposition.z + rooms[j].height / 2);
                          /*  DivideWall(rooms[i].roomObject.transform.Find("RightWall"), DoorPosition, "RightWall");
                            DivideWall(rooms[j].roomObject.transform.Find("leftWall"), DoorPosition, "leftWall");*/
                            CreateDoor(DoorPosition, doorWidth, true, "Door");
                        }
                        if (rooms[j].ZXposition.z < rooms[i].ZXposition.z && rooms[j].ZXposition.z + rooms[j].height > rooms[i].ZXposition.z + rooms[i].height && rooms[i].height >= 0.2)
                        {
                          //  Debug.Log("左方相邻第四种情况");//调试用
                            DoorPosition = new Vector3(rooms[i].ZXposition.x, y / 2, rooms[i].ZXposition.z + rooms[i].height);
                           /* DivideWall(rooms[i].roomObject.transform.Find("RightWall"), DoorPosition, "RightWall");
                            DivideWall(rooms[j].roomObject.transform.Find("leftWall"), DoorPosition, "leftWall");*/
                            CreateDoor(DoorPosition, doorWidth, true, "Door");

                        }
                    }
                    //上方相邻
                    if (Mathf.Abs(rooms[i].ZXposition.z + rooms[i].height - rooms[j].ZXposition.z) < 0.1f)
                    {
                      //  Debug.Log("在该房间上方相邻");//调试用
                        // 如果两个房间在 z 轴方向上相邻，检查两个房间相邻部分在x轴方向的差值.如果<2，那么我们不认为这两个房间是相邻的,因为有 1 的距离要用来放门
                        if (rooms[j].ZXposition.x < rooms[i].ZXposition.x && rooms[j].ZXposition.x + rooms[j].width > rooms[i].ZXposition.x && rooms[j].ZXposition.x + rooms[j].width - rooms[i].ZXposition.x >= 0.2)
                        {
                           // Debug.Log("上方相邻第一种情况");//调试用
                            DoorPosition = new Vector3((rooms[j].ZXposition.x + rooms[j].width + rooms[i].ZXposition.x) / 2, y / 2, rooms[i].ZXposition.z + rooms[i].height);
                         /*   DivideWall(rooms[i].roomObject.transform.Find("frontWall"), DoorPosition, "frontWall");
                            DivideWall(rooms[j].roomObject.transform.Find("backWall"), DoorPosition, "backWall");*/
                            CreateDoor(DoorPosition, doorWidth, false, "Door");
                        }
                        else if (rooms[j].ZXposition.x < rooms[i].ZXposition.x + rooms[i].width && rooms[j].ZXposition.x + rooms[j].width > rooms[i].ZXposition.x + rooms[i].width && rooms[i].ZXposition.x + rooms[i].width - rooms[j].ZXposition.x >= 0.2)
                        {
                          // Debug.Log("上方相邻第二种情况");//调试用
                            DoorPosition = new Vector3((rooms[i].ZXposition.x + rooms[i].width + rooms[j].ZXposition.x) / 2, y / 2, rooms[i].ZXposition.z + rooms[i].height);
                            /*DivideWall(rooms[i].roomObject.transform.Find("frontWall"), DoorPosition, "frontWall");
                            DivideWall(rooms[j].roomObject.transform.Find("backWall"), DoorPosition, "backWall");*/
                            CreateDoor(DoorPosition, doorWidth, false, "Door");
                        }
                        else if (rooms[j].ZXposition.x >= rooms[i].ZXposition.x && rooms[j].ZXposition.x + rooms[j].width <= rooms[i].ZXposition.x + rooms[i].width && rooms[j].width >= 0.2)
                        {
                           // Debug.Log("上方相邻第三种情况");//调试用
                            DoorPosition = new Vector3(rooms[j].ZXposition.x + rooms[j].width / 2, y / 2, rooms[i].ZXposition.z + rooms[i].height);
                          /*  DivideWall(rooms[i].roomObject.transform.Find("frontWall"), DoorPosition, "frontWall");
                            DivideWall(rooms[j].roomObject.transform.Find("backWall"), DoorPosition, "backWall");*/
                            CreateDoor(DoorPosition, doorWidth, false, "Door");
                        }
                        else if (rooms[j].ZXposition.x < rooms[i].ZXposition.x && rooms[j].ZXposition.x + rooms[j].width > rooms[i].ZXposition.x + rooms[i].width && rooms[i].width >= 0.2)
                        {
                           // Debug.Log("上方相邻第四种情况");//调试用
                            DoorPosition = new Vector3(rooms[i].ZXposition.x + rooms[i].width / 2, y / 2, rooms[i].ZXposition.z + rooms[i].height);
                          /*  DivideWall(rooms[i].roomObject.transform.Find("frontWall"), DoorPosition, "frontWall");
                            DivideWall(rooms[j].roomObject.transform.Find("backWall"), DoorPosition, "backWall");*/
                            CreateDoor(DoorPosition, doorWidth, false, "Door");
                        }
                    }
                    //下方相邻
                    if (Mathf.Abs(rooms[i].ZXposition.z - rooms[j].height - rooms[j].ZXposition.z) < 0.1f)
                    {
                        // 如果两个房间在 z 轴方向上相邻，检查两个房间相邻部分在x轴方向的差值.如果<2，那么我们不认为这两个房间是相邻的,因为有 1 的距离要用来放门
                       // Debug.Log("在该房间下方相邻");//调试用
                        if (rooms[j].ZXposition.x < rooms[i].ZXposition.x && rooms[j].ZXposition.x + rooms[j].width > rooms[i].ZXposition.x && rooms[j].ZXposition.x + rooms[j].width - rooms[i].ZXposition.x >= 0.2)
                        {
                          //  Debug.Log("在该房间下方相邻，情况1");//调试用
                            DoorPosition = new Vector3((rooms[j].ZXposition.x + rooms[j].width + rooms[i].ZXposition.x) / 2, y / 2, rooms[i].ZXposition.z);
                           /* DivideWall(rooms[i].roomObject.transform.Find("frontWall"), DoorPosition, "frontWall");
                            DivideWall(rooms[j].roomObject.transform.Find("backWall"), DoorPosition, "backWall");*/
                            CreateDoor(DoorPosition, doorWidth, false, "Door");
                        }
                        else if (rooms[j].ZXposition.x < rooms[i].ZXposition.x + rooms[i].width && rooms[j].ZXposition.x + rooms[j].width > rooms[i].ZXposition.x + rooms[i].width && rooms[i].ZXposition.x + rooms[i].width - rooms[j].ZXposition.x >= 0.2)
                        {
                           // Debug.Log("在该房间下方相邻，情况2");//调试用 
                            DoorPosition = new Vector3((rooms[j].ZXposition.x + rooms[j].width + rooms[i].ZXposition.x) / 2, y / 2, rooms[i].ZXposition.z);
                           /* DivideWall(rooms[i].roomObject.transform.Find("frontWall"), DoorPosition, "frontWall");
                            DivideWall(rooms[j].roomObject.transform.Find("backWall"), DoorPosition, "backWall");*/
                            CreateDoor(DoorPosition, doorWidth, false, "Door");

                        }
                        else if (rooms[j].ZXposition.x >= rooms[i].ZXposition.x && rooms[j].ZXposition.x + rooms[j].width <= rooms[i].ZXposition.x + rooms[i].width && rooms[j].width >= 0.2)
                        {
                           // Debug.Log("在该房间下方相邻，情况3");//调试用
                            DoorPosition = new Vector3((rooms[j].ZXposition.x + rooms[j].width + rooms[i].ZXposition.x) / 2, y / 2, rooms[i].ZXposition.z);
                           /* DivideWall(rooms[i].roomObject.transform.Find("frontWall"), DoorPosition, "frontWall");
                            DivideWall(rooms[j].roomObject.transform.Find("backWall"), DoorPosition, "backWall");*/
                            CreateDoor(DoorPosition, doorWidth, false, "Door");
                        }
                        else if (rooms[j].ZXposition.x < rooms[i].ZXposition.x && rooms[j].ZXposition.x + rooms[j].width > rooms[i].ZXposition.x + rooms[i].width && rooms[i].width >= 0.2)
                        {
                          //  Debug.Log("在该房间下方相邻，情况4");//调试用
                            DoorPosition = new Vector3((rooms[j].ZXposition.x + rooms[j].width + rooms[i].ZXposition.x) / 2, y / 2, rooms[i].ZXposition.z);
                            /*DivideWall(rooms[i].roomObject.transform.Find("frontWall"), DoorPosition, "frontWall");
                            DivideWall(rooms[j].roomObject.transform.Find("backWall"), DoorPosition, "backWall");*/
                            CreateDoor(DoorPosition, doorWidth, false, "Door");
                        }

                    }
                }
            }
        }
    }
    void AddExitDoors(Room EscapeRoom) //在最后一个房间的右侧和上方生成门
      {         
            float wallHeight = y;//门的高度
            // 右方门的位置
            Vector3 RightDoorPosition = new Vector3(EscapeRoom.ZXposition.x + EscapeRoom.width, wallHeight / 2, EscapeRoom.ZXposition.z+ EscapeRoom.height / 2);  
            // 上方门的位置
            Vector3 FrontDoorPosition = new Vector3(EscapeRoom.ZXposition.x + EscapeRoom.width/2, wallHeight / 2, EscapeRoom.ZXposition.z + EscapeRoom.height);

        // 创建右方的逃生门
        DivideWall(EscapeRoom.roomObject.transform.Find("rightWall"), RightDoorPosition, "rightWall");
        CreateDoor(RightDoorPosition, 0.1f, true, "Exit");
        //创建上方的逃生门
        DivideWall(EscapeRoom.roomObject.transform.Find("frontWall"), FrontDoorPosition, "frontWall");
        CreateDoor(FrontDoorPosition, 0.1f, false, "Exit");
       
        //Transform child = transform.Find("ChildName");
    }
    void CreateDoor(Vector3 position, float width,  bool isHorizontal, String Tag)
        {//参数依次是 门的位置，门的宽度，门如何摆放，门的标签，门的高度默认是全局变量中的 y 值
            // 创建一个新的立方体（门），并将其设置为触发器
            GameObject door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            door.GetComponent<BoxCollider>().isTrigger = true;
           // 将门添加到房间列表中
          AddRoomToList(door);
        // 设置门的名称和标签
        door.name = Tag;
            door.tag = Tag;

            // 设置门的位置
            door.transform.position =new Vector3(position.x,position.y,position.z);

            // 判断是否是横向门
            if (isHorizontal)
            {
                // 如果是横向门，设置门的大小
                // 0.3f 是门的厚度，y-1 是门的高度，1f 是门的宽度
                door.transform.localScale = new Vector3(0.4f,3 , doorWidth);

               // 设置门的颜色
               if (Tag == "Exit"){
                door.GetComponent<Renderer>().material= Exit;  // 绿色
               }
               else{
                door.GetComponent<Renderer>().material = Door;  // 白色
               }
            }
            else
            {
                // 如果是竖向门，设置门的大小
                // 1f 是门的宽度，y-1 是门的高度，0.3f 是门的厚度
                door.transform.localScale = new Vector3(doorWidth, 3, 0.4f);

            // 设置门的颜色
            if (Tag == "Exit")
            {
                door.GetComponent<Renderer>().material = Exit;  // 绿色
            }
            else
            {
                door.GetComponent<Renderer>().material = Door;  // 白色
            }
        }

           
        }
    void DivideWall(Transform wall,Vector3 DoorPosition, String WallName)
    { //划分墙壁，将墙壁分为两部分，将门的部位留出来，根据墙壁的名字来决定如何划分
        // 根据墙壁名称划分不同的墙
        if (WallName == "leftWall" || WallName == "rightWall")
        {
            // 创建上半部分墙壁
            GameObject frontWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frontWall.transform.parent = wall.transform.parent;
            frontWall.name = WallName;
            frontWall.transform.position = new Vector3(
                wall.position.x,
                wall.position.y,
               (wall.position.z + wall.localScale.z / 2 + DoorPosition.z + doorWidth / 2) / 2);
            frontWall.transform.localScale = new Vector3(wall.localScale.x, wall.localScale.y, (wall.localScale.z - doorWidth) / 2);

            // 创建下半部分墙壁
            GameObject backWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backWall.transform.parent = wall.transform.parent;
            backWall.name = WallName;
            backWall.transform.position = new Vector3(
                wall.position.x,
                wall.position.y,
               (DoorPosition.z - doorWidth / 2 + wall.position.z - wall.localScale.z / 2) / 2);
            backWall.transform.localScale = new Vector3(wall.localScale.x, wall.localScale.y, (wall.localScale.z - doorWidth) / 2);

            // 销毁原墙
             wall.gameObject.SetActive(false);
        }
        else if (WallName == "frontWall" || WallName == "backWall")
        {
            // 创建左半部分墙壁
            GameObject leftWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftWall.transform.parent = wall.transform.parent;
            leftWall.name = WallName;
            //中点坐标公式
            leftWall.transform.position = new Vector3(
                (DoorPosition.x - doorWidth / 2 + wall.position.x - wall.localScale.x / 2) / 2,
                wall.position.y,
                wall.position.z);
            leftWall.transform.localScale = new Vector3((wall.localScale.x - doorWidth) / 2, wall.localScale.y, wall.localScale.z);

            // 创建右半部分墙壁
            GameObject rightWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightWall.transform.parent = wall.transform.parent;
            rightWall.name = WallName;
            rightWall.transform.position = new Vector3(
                (wall.position.x + wall.localScale.x / 2 + DoorPosition.x + doorWidth / 2) / 2,
                wall.position.y,
                wall.position.z);
            rightWall.transform.localScale = new Vector3((wall.localScale.x - doorWidth) / 2, wall.localScale.y, wall.localScale.z);
            // 销毁原墙
             wall.gameObject.SetActive(false);
        }
    }

        /*  //程序的主入口,在UImanage里面创建了一个BuildingGeneration类，直接调用了该类的GenerateRoom方法
          void Start()
          {
              // 检查是否已经通过 UImanager 设置了房间面积
              if (roomAreas != null && roomAreas.Length > 0)
              {
                  GenerateRooms();
              }
              else
              {
                  Debug.LogError("房间面积数组为空，无法生成房间！");
              }
          }*/
    }
