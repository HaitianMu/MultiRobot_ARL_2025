using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using System.Text.RegularExpressions;
using static JsonLoad;
using System.Linq;

public partial class BuildingControl : MonoBehaviour
{

    [System.Serializable]
    public class EmergencyExit
    {
        public string ExitRoom;
        public string position;
    }
    [System.Serializable]
    public class Layout
    {
        public string name;
        // 修改这里：匹配 JSON 中的数组结构
        public List<EmergencyExit> EmergencyExits;
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
       
        // 1. 加载并解析一次
        TextAsset jsonFile = Resources.Load<TextAsset>(filename);
        if (jsonFile == null) return;

        LayoutList data = JsonConvert.DeserializeObject<LayoutList>(jsonFile.text);
        Layout targetLayout = data.Layouts.Find(l => l.name == layoutname);

        if (targetLayout == null) return;


        // 2. 清理并填充房间列表
        roomList.Clear();

        foreach (Room room in targetLayout.rooms)
        {
            roomList.Add(room);
        }
    
        // 3. 生成实体
        CreateRoomBinary(roomList);  
        GetMaxXZofLayout(roomList);

        // 4. 处理门（传递已解析的数据）
        Room[][] CN = BuildConnectionMatrix(targetLayout);
        CreateDoorBetweenRooms(CN);

        // 5. 处理出口（使用修正后的数据结构）
        AddExitDoorsFromLayout(targetLayout);
    }

    // 提取出的解析逻辑
    private Room[][] BuildConnectionMatrix(Layout layout)
    {
        Dictionary<string, Room> nameToRoom = new Dictionary<string, Room>();
        foreach (Room room in roomList) nameToRoom[room.roomName] = room;

        Room[][] connection = new Room[roomList.Count][];
        for (int i = 0; i < roomList.Count; i++)
        {
            Room currentRoom = roomList[i];
            List<Room> connectedRooms = new List<Room>();

            if (currentRoom.ConnectedRoom != null)
            {
                foreach (string connectedName in currentRoom.ConnectedRoom)
                {
                    if (nameToRoom.TryGetValue(connectedName, out Room r))
                        connectedRooms.Add(r);
                }
            }
            connection[i] = connectedRooms.ToArray();
        }
        return connection;
    }

    private void AddExitDoorsFromLayout(Layout layout)
    {
        if (layout.EmergencyExits == null) return;

        // 缓存一个由所有房间名组成的“全名列表”用于报错时打印，避免重复循环
        // 同时也方便我们肉眼检查
        string allRoomNamesForDebug = "";
        if (roomList.Count > 0)
        {
            // 简单拼接一下，方便Debug看
            var names = new System.Collections.Generic.List<string>();
            foreach (var r in roomList) names.Add(r.roomName);
            allRoomNamesForDebug = string.Join(", ", names);
        }

        Debug.Log($"============== 开始匹配出口 (房间总数: {roomList.Count}) ==============");
        //Debug.LogError($"[关键检查] 当前 roomList 的数量是: {roomList.Count}");

        if (roomList.Count > 0)
        {
            Debug.Log($"[关键检查] 列表第4个房间是: '{roomList[3].roomName}'");
        }

        foreach (var exitData in layout.EmergencyExits)
        {
            // 步骤 1: 暴力清洗目标字符串 (只保留字母和数字)

            Room escapeRoom=new Room();
            foreach (Room a in roomList)
            {
                if (a.roomName.Equals(exitData.ExitRoom))
                {
                    print("找到了出口房间：" + a.roomName);
                    escapeRoom = a;

                    Vector3 pos = GetDoorPosition(escapeRoom, exitData.position);
                    bool isVerticalWall = (exitData.position == "left" || exitData.position == "right");
                    CreateDoor(pos, 0.1f, isVerticalWall, "Exit");
                    break;
                }
                }
            }
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

