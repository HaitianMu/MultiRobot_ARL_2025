 using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using System;
using Unity.VisualScripting;
public partial class BuildingControl : MonoBehaviour
{
    //生成房间的要求：
    //1.不能过扁或者过窄
    //2.房间面积应该有一个最小值，其实这一步与第一个是重复的
    //3.房间的长宽比不能过于奇怪
    //该方法用到的所有数据：
    //区域总面积，areaSize
    /*.....................................一、接受UI输入的区域面积和房间数目，然后进行房间的摆放和房间墙壁的生成.......................................*/
    public void GenerateRoomsBinary(float areasize,int roomnum)
    {
      // Debug.Log("开始执行二分法生成房间布局......");
       totalArea=areasize;
       roomNum = roomnum;
       FindBestDimensions(totalArea);//调用该函数后，得到整个区域的长和宽 totalWidth和TotalHeight（z值）
        if (roomList.Count == 0)//roomList初始化，将整个作为数组的第一个
        {
           // Debug.Log("添加第一个房间");
            Room room = new Room(new Vector3(0, y / 2, 0), totalWidth, totalHeight, totalHeight * totalWidth);
            roomList.Add(room);
        }
        CreateRoomRectBinary(roomList[0]);      //对房间布局进行划分，生成的房间对象全部存储在roomList中
        CreateRoomBinary(roomList);

        // 生成连通图
        Room[][] CN = GenerateCN(roomList.ToArray());
        //根据连通图来生成门
        CreateDoorBetweenRooms(roomList.ToArray(), CN); //根据连通图CN生成门
        //在右上角的房间生成门
        AddExitDoors(FindEscapeRoom());
    }

    private void CreateRoomRectBinary(Room room) {
        if (roomList.Count >= roomNum)//终止函数
        {
           // Debug.Log("数组中的房间数量为：" + roomList.Count + ",需要生成的房间数量为：" + roomNum+"。生成完毕");
            return;
        }
        // 选择分割方向
        bool splitVertical; // 随机选择垂直或水平分割
                            // 如果宽度大于高度，优先垂直分割
        if (room.width > 2*room.height)
        {
            splitVertical = true;
        }
        // 如果高度大于宽度，优先水平分割
        else if (room.height > 2*room.width)
        {
            splitVertical = false;
        }
        // 如果宽度和高度相等，随机选择分割方向
        else
        {
            splitVertical = UnityEngine.Random.Range(0, 2) == 0;
        }


        //避免了房间过扁或过窄
        if (room.width<minWidth)
        {
            splitVertical = false;
        }
        else if(room.height<minheight)
        { splitVertical = true; }



        if (splitVertical)  //垂直分割，图形很长
        {
           // Debug.Log("选择的分割方向是：垂直分割");
            // 垂直分割：随机选择分割点
            float minSplit =  minWidth; // 最小分割宽度
            float maxSplit = room.width - minWidth; // 最大分割宽度
           

            if (maxSplit < minSplit)//可能出现，输入的房间数目过多，如果不进行终止的话，最终划分的房间会过小
            {
                Debug.Log("划分后的房间过小，不能继续进行划分了");
                return;
            }

            // 随机分割宽度，并控制长宽比
            float splitWidth = FindValidSplit(minSplit, maxSplit, room.width, room.height);


            // 创建左房间和右房间
            Room leftRoom = new Room(
                new Vector3(room.xzPosition.x,0,room.xzPosition.z),
                splitWidth,
                room.height,
                splitWidth*room.height);

            Room RightRoom= new Room(
                new Vector3(room.xzPosition.x+splitWidth,0,room.xzPosition.z),
                room.width-splitWidth,
                room.height,
                (room.width - splitWidth)*room.height);

            roomList.Remove(room);//移除匹配的第一个元素，其余向前移
            roomList.Add(leftRoom);//添加在序列末尾
            roomList.Add(RightRoom);


            Room newBigRoom=FindTheBiggestRoom(roomList);
            CreateRoomRectBinary(newBigRoom);
        }
        else
        {
         //   Debug.Log("选择的分割方向是：水平分割");
            // 水平分割：随机选择分割点
            float minSplit =minheight; // 最小分割高度
            float maxSplit = room.height - minheight; // 最大分割高度
            

            if (maxSplit < minSplit)
            {
                Debug.Log("划分后的房间过小，不能继续进行划分了");
                return;
            }
            // 选取分割点
            float splitHeight = FindValidSplit(minSplit, maxSplit, room.width, room.height);


            // 创建下房间和上房间
            Room bottomRoom = new Room(
                new Vector3(room.xzPosition.x, 0, room.xzPosition.z), 
                room.width, 
                splitHeight,
                room.width*splitHeight);

            Room topRoom = new Room(
                new Vector3(room.xzPosition.x, 0, room.xzPosition.z+splitHeight),
                room.width,
                room.height - splitHeight, 
                room.width * (room.height - splitHeight));

            roomList.Remove(room);
            roomList.Add (bottomRoom);
            roomList.Add(topRoom);
            Room newBigRoom = FindTheBiggestRoom(roomList);
            CreateRoomRectBinary(newBigRoom);
        }
    }
    public Room FindTheBiggestRoom(List<Room> roomlist)
    {
        //Debug.Log("寻找房间面积最大的房间！！！！！！！！！！！！！！！");
        float MaxroomSize = 0f;
        int num = 1;
        int max = 0;
        Room biggestroom = new();
        foreach (Room room in roomlist) {
            //Debug.Log("房间"+num + "的面积是：" + room.roomSize);
          if(room.roomSize > MaxroomSize) {
            MaxroomSize = room.roomSize; // 更新最大面积
            biggestroom = room;
             max = num;
            }
            num++;
        }
       // Debug.Log( "最大房间是：" + "房间" + max);

        return biggestroom;

    }
    public void CreateRoomBinary(List<Room> roomlist)
    {
      //  Debug.Log("执行了在场景中创建房间的函数");
        foreach (Room room in roomlist) {
             GameObject Realroom=  CreateRoomInScene(room.xzPosition.x, room.xzPosition.z, room.width, room.height,room.roomName);
            AddObjectToList(Realroom);
            //Debug.Log("房间"+num+"的XZ坐标为"+room.xzPosition.x+"，"+room.xzPosition.z);
            //Debug.Log("房间" + num + "的长和高为" + room.width + "，" + room.height);
        }
    }

    /// <summary>
    /// 尝试找到符合长宽比要求的分割点
    /// </summary>
    /// <param name="minSplit">最小分割值</param>
    /// <param name="maxSplit">最大分割值</param>
    /// <param name="roomWidth">房间宽度</param>
    /// <param name="roomHeight">房间高度</param>
    /// <returns>符合要求的分割点；如果未找到，返回 -1</returns>
    private float FindValidSplit(float minSplit, float maxSplit, float roomWidth, float roomHeight)
    {
        int maxAttempts = 2000; // 最大尝试次数
        int attempts = 0;

        // 初始长宽比限制
        float minAspectRatio = 0.618f; // 
        float maxAspectRatio = 1.618f;    // 4

        do
        {
            // 随机生成分割点
            float splitValue = UnityEngine.Random.Range(minSplit, maxSplit);

            // 计算两个房间的长宽比
            float aspectRatio1 = splitValue / roomHeight;
            float aspectRatio2 = (roomWidth - splitValue) / roomHeight;

            attempts++;

            // 每 100 次尝试后放宽限制
            if (attempts % 2000== 0 && attempts > 0)
            {
                minAspectRatio *= 0.8f; // 放宽下限
                maxAspectRatio *= 1.2f; // 放宽上限
              //  Debug.Log($"尝试 {attempts} 次未成功，放宽长宽比限制为 {minAspectRatio} 到 {maxAspectRatio}");
            }

            // 检查长宽比是否符合要求
            if (aspectRatio1 >= minAspectRatio && aspectRatio1 <= maxAspectRatio &&
                aspectRatio2 >= minAspectRatio && aspectRatio2 <= maxAspectRatio)
            {
                return splitValue; // 返回符合要求的分割点
            }
        }
        while (attempts < maxAttempts);

        // 如果未找到符合要求的分割点，返回 -1

        //Debug.Log("没有找到理想的分割点，返回随机值");
        return UnityEngine.Random.Range(minSplit, maxSplit);
    }

    public GameObject CreateRoomInScene(float x, float z, float width, float height,string roomName)
    {
        // 创建房间的主体

        GameObject room = new GameObject(roomName);
        room.tag = "Room";
        room.transform.parent = ParentObject.transform;
        RoomNum++;

        // 生成房间的底部（如果需要的话，可以添加底部，作为房间地面）
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.AddComponent<NavMeshModifier>();
        floor.name = "floor";
        floor.tag = "Floor";
        floor.layer = 8;
        floor.transform.parent = room.transform;
        floor.transform.position = new Vector3(x + width / 2, 0f, z + height / 2);
        floor.transform.localScale = new Vector3(width, 0.1f, height); // 底部宽度和高度

        // 给房间底部上色 Assets/Material/Floor.mat
        floor.GetComponent<Renderer>().material = Floor;

        /* AddObjectToList(floor); // 将地面加入到房间列表中*/ //墙壁是房间的子物体，房间已经被清除了
                                                    // 生成四个墙壁
        CreateWall(x, z, width, height, room);
        return room;
    }
    public Room FindEscapeRoom()
    {
        if (roomList == null || roomList.Count == 0)
        {
            Debug.LogWarning("房间列表为空，无法找到右上角的房间！");
            return null;
        }

        Room escapeRoom = null;
        float maxSum = float.MinValue; // 用于记录最大的右上角坐标之和

        foreach (Room room in roomList)
        {
            // 计算房间右上角的坐标之和
            float sum = room.xzPosition.x + room.width + room.xzPosition.z + room.height;

            // 如果当前房间的右上角坐标之和更大，则更新最大值和房间
            if (sum > maxSum)
            {
                maxSum = sum;
                escapeRoom = room;
            }
        }

        // 输出找到的房间信息
        if (escapeRoom != null)
        {
            //Debug.Log($"找到右上角的房间：位置=({escapeRoom.xzPosition.x}, {escapeRoom.xzPosition.z}), 宽度={escapeRoom.width}, 高度={escapeRoom.height}");
        }
        else
        {
            Debug.Log("未找到右上角的房间");
        }

        return escapeRoom;
    }
}
