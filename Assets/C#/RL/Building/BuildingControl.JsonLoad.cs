using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using static JsonLoad;

public partial class BuildingControl : MonoBehaviour
{

    [System.Serializable]
    public class Layout                 //布局数据类，用来读取json数据
    {
        public string name;
        public String ExitRoom;
        public String ExitDoorPosition;//right left forward backward
        public Room[] rooms;
    }

    [System.Serializable]
    public class LayoutList
    {
        public List<Layout> Layouts;
    }
    // Start is called before the first frame update
    public void GenerateRoomsJsonLoad(string filename, string layoutname)
    {
        LoadRoomDataFromJson(filename, layoutname);//从json文件中加载房间数组的数据
        CreateRoomBinary(roomList);//在场景中生成房间

        GetMaxXZofLayout(roomList);//得到该场景所在矩形的右上角坐标，用于后续训练时的观测量归一化

        Room[][] CN = LoadDoorDataFromJson(filename, layoutname);//生成的不再是严格的对称矩阵，可能是锯齿形的，只存储了需要的信息
                                                                 // print("roomList中的房间为：" + roomList);
        CreateDoorBetweenRooms(CN); //根据连通图CN生成门
        AddExitDoors(filename, layoutname);
    }

    public void LoadRoomDataFromJson(string filename, string layoutname)
    {
        //Debug.Log("从json文件中加载房间数组的数据");
        TextAsset jsonFile = Resources.Load<TextAsset>(filename); //加载json数据到jsonFile当中
        if (jsonFile == null)
        {
            Debug.LogError("JSON 文件未找到！请检查：\n" +
                         "- 文件是否在 Assets/Resources 内\n" +
                         "- 文件名是否完全匹配（包括大小写）\n" +
                         "- 文件扩展名是否为 .json");
            return;
        }
        // Debug.Log("原始JSON内容:\n" + jsonFile.text);
        // 2. 使用 Newtonsoft.Json 解析，读取json文件这一步没问题。那应该就是解析的时候出错了
        try
        {
            LayoutList data = JsonConvert.DeserializeObject<LayoutList>(jsonFile.text);
            if (data == null || data.Layouts == null)
            {
                Debug.LogError("JSON解析失败，请检查格式是否正确");
                return;
            }

            // 查找指定的 Layout
            Layout targetLayout = data.Layouts.Find(layouts => layouts.name == layoutname);
            if (targetLayout == null)
            {
                Debug.LogError("没有找到目标布局，请检查提供的布局名称是否正确");
                return;
            }
            foreach (Room room in targetLayout.rooms)
            {
                roomList.Add(room);
            }

            //!!!!!!!!!!!调试用，看各个房间是否加入roomlist
            /*foreach (Room room in roomList)
            {
                print("!!!!!!!!!!!!!!!!!!!!!");
                Debug.Log("房间名称为："+room.roomName);
                Debug.Log("房间左下角坐标为："+room.xzPosition);
                Debug.Log("房间长宽分别为："+room.width+","+room.height);
            }*/
            //!!!!!!!!!!!调试用，看各个房间是否加入roomlist
        }

        catch (System.Exception e)
        {
            Debug.LogError($"解析失败: {e.Message}\nJSON 内容:\n{jsonFile.text}");
        }


    }
    public Room[][] LoadDoorDataFromJson(string filename, string layoutname)
    {
        //这一步是没问题的，已经检查过了，那应该就是下面的算法出现问题了
        /* Debug.Log("加载门的数据 ");*/
        // 1. 加载并解析JSON文件
        TextAsset jsonFile = Resources.Load<TextAsset>(filename); //加载json数据到jsonFile当中
        if (jsonFile == null)
        {
            Debug.LogError("JSON 文件未找到！请检查：\n" +
                         "- 文件是否在 Assets/Resources 内\n" +
                         "- 文件名是否完全匹配（包括大小写）\n" +
                         "- 文件扩展名是否为 .json");
            return null;
        }
        LayoutList data = JsonConvert.DeserializeObject<LayoutList>(jsonFile.text);
        if (data == null || data.Layouts == null)
        {
            Debug.LogError("JSON解析失败，请检查格式是否正确");
            return null;
        }

        // 2. 查找目标布局
        Layout targetLayout = data.Layouts.Find(layouts => layouts.name == layoutname);
        if (targetLayout == null)
        {
            Debug.LogError("没有找到目标布局，请检查提供的布局名称是否正确");
            return null;
        }

        // 3. 建立房间名到Room对象的字典。
        // 字典的使用，新知识！！！！！，好像也不是新知识？？？这个是java里面的map映射
        Dictionary<string, Room> nameToRoom = new Dictionary<string, Room>();
        foreach (Room room in roomList)
        {
            nameToRoom[room.roomName] = room;
            //print("正在建立名字到类的字典："+room.roomName+"对应的实体是"+room);
        }

        // 4. 初始化二维连接数组
        Room[][] connection = new Room[roomList.Count][];
        for (int i = 0; i < roomList.Count; i++)//挨个添加邻接房间
        {
            Room currentRoom = roomList[i];

            if (currentRoom.ConnectedRoom == null || currentRoom.ConnectedRoom.Length == 0)// 无邻接房间，一般不存在这个情况，否则这个房间就是个孤立点
            {
                connection[i] = new Room[0]; // 无邻接房间
                continue;
            }

            // 5. 转换邻接房间名列表为Room对象数组
            List<Room> connectedRooms = new List<Room>();
            foreach (string connectedName in currentRoom.ConnectedRoom)
            {
                //当前的房间是
                if (nameToRoom.TryGetValue(connectedName, out Room connectedRoom))
                {
                    // Debug.Log("当前房间是："+currentRoom.roomName+",其邻接房间为"+connectedRoom.roomName);
                    connectedRooms.Add(connectedRoom);
                }
                else
                {
                    Debug.LogWarning($"房间 '{currentRoom.roomName}' 的邻接房间 '{connectedName}' 不存在");
                }
            }

            connection[i] = connectedRooms.ToArray();
        }

        return connection;
    }
    private void CreateDoorBetweenRooms(Room[][] cN)
    {
        float distanceThreshold = 0.1f;

        for (int i = 0; i < cN.Length; i++)
        {
            Room currentRoom = roomList[i];

            foreach (Room connectedRoom in cN[i])
            {
                // 检查右方相邻
                if (Mathf.Abs(currentRoom.xzPosition.x + currentRoom.width - connectedRoom.xzPosition.x) < distanceThreshold)
                {
                    TryCreateHorizontalDoor(currentRoom, connectedRoom, true);
                }
                // 检查左方相邻
                else if (Mathf.Abs(currentRoom.xzPosition.x - (connectedRoom.xzPosition.x + connectedRoom.width)) < distanceThreshold)
                {
                    TryCreateHorizontalDoor(currentRoom, connectedRoom, false);
                }
                // 检查上方相邻
                else if (Mathf.Abs(currentRoom.xzPosition.z + currentRoom.height - connectedRoom.xzPosition.z) < distanceThreshold)
                {
                    TryCreateVerticalDoor(currentRoom, connectedRoom, true);
                }
                // 检查下方相邻
                else if (Mathf.Abs(currentRoom.xzPosition.z - (connectedRoom.xzPosition.z + connectedRoom.height)) < distanceThreshold)
                {
                    TryCreateVerticalDoor(currentRoom, connectedRoom, false);
                }
            }
        }
    }

    private void TryCreateHorizontalDoor(Room current, Room connected, bool isRight)
    {
        float overlapStart = Mathf.Max(current.xzPosition.z, connected.xzPosition.z);
        float overlapEnd = Mathf.Min(current.xzPosition.z + current.height, connected.xzPosition.z + connected.height);
        float overlap = overlapEnd - overlapStart;

        if (overlap >= 1f)
        { // 确保有足够的重叠空间
            float doorX = isRight ? current.xzPosition.x + current.width : current.xzPosition.x;
            float doorZ = overlapStart + overlap / 2;

            Vector3 doorPosition = new Vector3(doorX, y / 2, doorZ);
            CreateDoor(doorPosition, doorWidth, true, "Door");
        }
    }

    private void TryCreateVerticalDoor(Room current, Room connected, bool isTop)
    {
        float overlapStart = Mathf.Max(current.xzPosition.x, connected.xzPosition.x);
        float overlapEnd = Mathf.Min(current.xzPosition.x + current.width, connected.xzPosition.x + connected.width);
        float overlap = overlapEnd - overlapStart;

        if (overlap >= 1f)
        { // 确保有足够的重叠空间
            float doorZ = isTop ? current.xzPosition.z + current.height : current.xzPosition.z;
            float doorX = overlapStart + overlap / 2;

            Vector3 doorPosition = new Vector3(doorX, y / 2, doorZ);
            CreateDoor(doorPosition, doorWidth, false, "Door");
        }
    }


    private void AddExitDoors(string filename, string layoutname)
    {
        Room EscapeRoom = null;

        TextAsset jsonFile = Resources.Load<TextAsset>(filename); //加载json数据到jsonFile当中
        if (jsonFile == null)
        {
            Debug.LogError("JSON 文件未找到！请检查：\n" +
                         "- 文件是否在 Assets/Resources 内\n" +
                         "- 文件名是否完全匹配（包括大小写）\n" +
                         "- 文件扩展名是否为 .json");
            return;
        }
        // Debug.Log("原始JSON内容:\n" + jsonFile.text);
        // 2. 使用 Newtonsoft.Json 解析，读取json文件这一步没问题。那应该就是解析的时候出错了
        try
        {
            LayoutList data = JsonConvert.DeserializeObject<LayoutList>(jsonFile.text);
            if (data == null || data.Layouts == null)
            {
                Debug.LogError("JSON解析失败，请检查格式是否正确");
                return;
            }

            // 查找指定的 Layout
            Layout targetLayout = data.Layouts.Find(layouts => layouts.name == layoutname);
            if (targetLayout == null)
            {
                Debug.LogError("没有找到目标布局，请检查提供的布局名称是否正确");
                return;
            }
            //Debug.Log(targetLayout.ExitRoom);
            //Debug.Log(targetLayout.ExitDoorPosition);

            // 查找目标逃生房间
            foreach (Room room in roomList)
            {
                if (room.roomName == targetLayout.ExitRoom)
                {
                    EscapeRoom = room;
                    break;
                }
            }
            //查找门在该房间的位置，right left forward backward

            Vector3 DoorPosition = GetDoorPosition(EscapeRoom, targetLayout.ExitDoorPosition);
            string DoorPOS = targetLayout.ExitDoorPosition;

            if (DoorPOS == "right")
            {
                CreateDoor(DoorPosition, 0.1f, true, "Exit");
            }

            else if (DoorPOS == "left")
            {
                CreateDoor(DoorPosition, 0.1f, true, "Exit");
            }
            else if (DoorPOS == "forward")
            {
                CreateDoor(DoorPosition, 0.1f, false, "Exit");
            }
            else if (DoorPOS == "backward")
            {
                CreateDoor(DoorPosition, 0.1f, false, "Exit");
            }
            return;
        }
        catch (Exception e)
        {
            Debug.LogError($"解析失败: {e.Message}\nJSON 内容:\n{jsonFile.text}");
            return;
        }
    }

    private Vector3 GetDoorPosition(Room escapeRoom, string doorPosition)
    {
        Vector3 doorPos = new Vector3();

        if (doorPosition == "right")
        {
            doorPos = new Vector3(
                escapeRoom.xzPosition.x + escapeRoom.width,  // 右侧墙的X位置
                y / 2,                                      // 门高度（Y位置）
                escapeRoom.xzPosition.z + escapeRoom.height / 2  // 在墙的中间位置
            );
        }
        else if (doorPosition == "left")
        {
            doorPos = new Vector3(
                escapeRoom.xzPosition.x,                    // 左侧墙的X位置
                y / 2,
                escapeRoom.xzPosition.z + escapeRoom.height / 2
            );
        }
        else if (doorPosition == "forward")
        {
            doorPos = new Vector3(
                escapeRoom.xzPosition.x + escapeRoom.width / 2,  // 在墙的中间位置
                y / 2,
                escapeRoom.xzPosition.z + escapeRoom.height      // 前侧墙的Z位置
            );
        }
        else if (doorPosition == "backward")
        {
            doorPos = new Vector3(
                escapeRoom.xzPosition.x + escapeRoom.width / 2,
                y / 2,
                escapeRoom.xzPosition.z                        // 后侧墙的Z位置
            );
        }
        else
        {
            Debug.LogError("Unknown door position: " + doorPosition);
        }

        return doorPos;
    }

    private void GetMaxXZofLayout(List<Room> roomList)
    {
        float maxX = 0f;
        float maxZ = 0f;
        foreach (Room room in roomList)
        {
            // 计算房间的右上角坐标（左下角xzPosition + width/height）
            float roomMaxX = room.xzPosition.x + room.width;
            float roomMaxZ = room.xzPosition.z + room.height;

            // 更新全局最大值
            maxX = Mathf.Max(maxX, roomMaxX);
            maxZ = Mathf.Max(maxZ, roomMaxZ);
        }
        totalWidth = maxX;
        totalHeight = maxZ;
    }

}

