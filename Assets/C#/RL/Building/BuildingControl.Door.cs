using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class BuildingControl : MonoBehaviour
{
    /*.......................................二、在生成的房间之间生成门.....................................*/
    Room[][] GenerateCN(Room[] rooms)//确保所有的节点都能到达最后一个节点即可
    {

        //生成连通图,所有连通的房间之间都添加一个门
        int n = rooms.Length;
        Room[][] connection = new Room[n][];


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
                     //Debug.Log(rooms[i].roomObject.name + "和" + rooms[j].roomObject.name + "相邻");//调试用
                }
            }
        }
        return connection;
        // //生成连通图,然后在所有连通的房间之间都添加一个门

    }

    // 判断边是否是确保连通性所必需的
    void CreateDoorBetweenRooms(Room[] rooms, Room[][] CN) //根据连通图CN生成门
    {
        String DoorName = "Door" + doorNum;
        float distance = 5;
        for (int i = 0; i < rooms.Length; i++)
        {
            for (int j = i+1; j < rooms.Length; j++)
            {
                if (CN[i][j] != null)//说明两个房间是邻接的，接着判断两个房间是上、下、左、右哪种相邻方式，根据不同的相邻方式进行不同的处理
                {
                    /*Debug.Log(rooms[i].roomObject.name + "和" + rooms[j].roomObject.name + "相邻");//调试用*/
                    Vector3 DoorPosition;
                    // 右方相邻
                    if (Mathf.Abs(rooms[i].xzPosition.x + rooms[i].width - rooms[j].xzPosition.x) < 0.1f)
                    {
                        // Debug.Log("在该房间右方相邻");//调试用
                        // 如果两个房间在 x 轴方向上相邻,检查两个房间相邻部分在z轴方向的差值.如果<2，那么我们不认为这两个房间是相邻的,因为有 1 的距离要用来放门
                        //总体可分为三种情况
                        if (rooms[j].xzPosition.z < rooms[i].xzPosition.z && rooms[j].xzPosition.z + rooms[j].height > rooms[i].xzPosition.z && rooms[j].xzPosition.z + rooms[j].height - rooms[i].xzPosition.z >= distance)
                        {
                            // Debug.Log("在该房间右方相邻：情况1");//调试用
                            DoorPosition = new Vector3(rooms[i].xzPosition.x + rooms[i].width, y / 2, (rooms[i].xzPosition.z + rooms[j].xzPosition.z + rooms[j].height) / 2);
                            /* DivideWall(rooms[i].roomObject.transform.Find("RightWall"), DoorPosition, "RightWall");
                             DivideWall(rooms[j].roomObject.transform.Find("leftWall"), DoorPosition, "leftWall");*/
                            CreateDoor(DoorPosition, doorWidth, true, "Door");
                            continue;
                        }
                        if (rooms[j].xzPosition.z < rooms[i].xzPosition.z + rooms[i].height && rooms[j].xzPosition.z + rooms[j].height > rooms[i].xzPosition.z + rooms[i].height && rooms[i].xzPosition.z + rooms[i].height - rooms[j].xzPosition.z >= distance)
                        {
                            // Debug.Log("在该房间右方相邻：情况2");//调试用
                            DoorPosition = new Vector3(rooms[i].xzPosition.x + rooms[i].width, y / 2, (rooms[j].xzPosition.z + rooms[i].xzPosition.z + rooms[i].height) / 2);
                            /* DivideWall(rooms[i].roomObject.transform.Find("RightWall"), DoorPosition, "RightWall");
                             DivideWall(rooms[j].roomObject.transform.Find("leftWall"), DoorPosition, "leftWall");*/
                            CreateDoor(DoorPosition, doorWidth, true, "Door");
                            continue;
                        }
                        if (rooms[j].xzPosition.z >= rooms[i].xzPosition.z && rooms[j].xzPosition.z + rooms[j].height <= rooms[i].xzPosition.z + rooms[i].height && rooms[j].height >= distance)
                        {
                            //  Debug.Log("在该房间右方相邻：情况3");//调试用
                            DoorPosition = new Vector3(rooms[i].xzPosition.x + rooms[i].width, y / 2, rooms[j].xzPosition.z + rooms[j].height / 2);
                            /* DivideWall(rooms[i].roomObject.transform.Find("RightWall"), DoorPosition, "RightWall");
                             DivideWall(rooms[j].roomObject.transform.Find("leftWall"), DoorPosition, "leftWall");*/
                            CreateDoor(DoorPosition, doorWidth, true, "Door");
                            continue;
                        }
                        if (rooms[j].xzPosition.z < rooms[i].xzPosition.z && rooms[j].xzPosition.z + rooms[j].height > rooms[i].xzPosition.z + rooms[i].height && rooms[i].height >= distance)
                        {
                            //   Debug.Log("在该房间右方相邻：情况4");//调试用
                            DoorPosition = new Vector3(rooms[i].xzPosition.x + rooms[i].width, y / 2, rooms[i].xzPosition.z + rooms[i].height);
                            /*  DivideWall(rooms[i].roomObject.transform.Find("RightWall"), DoorPosition, "RightWall");
                              DivideWall(rooms[j].roomObject.transform.Find("leftWall"), DoorPosition, "leftWall");*/
                            CreateDoor(DoorPosition, doorWidth, true, "Door");
                            continue;
                        }
                    }

                    //左方相邻
                   else if (Mathf.Abs(rooms[i].xzPosition.x - rooms[j].width - rooms[j].xzPosition.x) < 0.1f)
                    {
                        // 如果两个房间在 x 轴方向上相邻,检查两个房间相邻部分在z轴方向的差值.如果<2，那么我们不认为这两个房间是相邻的,因为有 1 的距离要用来放门
                        // Debug.Log("在该房间左方相邻");//调试用
                        //总体可分为三种情况
                        if (rooms[j].xzPosition.z <= rooms[i].xzPosition.z && rooms[j].xzPosition.z + rooms[j].height >= rooms[i].xzPosition.z && rooms[j].xzPosition.z + rooms[j].height - rooms[i].xzPosition.z >= distance)
                        {
                            //  Debug.Log("左方相邻第一种情况");//调试用
                            DoorPosition = new Vector3(rooms[i].xzPosition.x, y / 2, (rooms[i].xzPosition.z + rooms[j].xzPosition.z + rooms[j].height) / 2);
                            /*   DivideWall(rooms[i].roomObject.transform.Find("RightWall"), DoorPosition, "RightWall");
                               DivideWall(rooms[j].roomObject.transform.Find("leftWall"), DoorPosition, "leftWall");*/
                            CreateDoor(DoorPosition, doorWidth, true, "Door");
                            continue;
                        }
                        if (rooms[j].xzPosition.z <= rooms[i].xzPosition.z + rooms[i].height && rooms[j].xzPosition.z + rooms[j].height >= rooms[i].xzPosition.z + rooms[i].height && rooms[i].xzPosition.z + rooms[i].height - rooms[j].xzPosition.z >= distance)
                        {
                            // Debug.Log("左方相邻第二种情况");//调试用
                            DoorPosition = new Vector3(rooms[i].xzPosition.x, y / 2, (rooms[j].xzPosition.z + rooms[i].xzPosition.z + rooms[i].height) / 2);
                            /*DivideWall(rooms[i].roomObject.transform.Find("RightWall"), DoorPosition, "RightWall");
                            DivideWall(rooms[j].roomObject.transform.Find("leftWall"), DoorPosition, "leftWall");*/
                            CreateDoor(DoorPosition, doorWidth, true, "Door");
                            continue;
                        }
                        if (rooms[j].xzPosition.z >= rooms[i].xzPosition.z && rooms[j].xzPosition.z + rooms[j].height <= rooms[i].xzPosition.z + rooms[i].height && rooms[j].height >= distance)
                        {
                            //  Debug.Log("左方相邻第三种情况");//调试用
                            DoorPosition = new Vector3(rooms[i].xzPosition.x, y / 2, rooms[j].xzPosition.z + rooms[j].height / 2);
                            /*  DivideWall(rooms[i].roomObject.transform.Find("RightWall"), DoorPosition, "RightWall");
                              DivideWall(rooms[j].roomObject.transform.Find("leftWall"), DoorPosition, "leftWall");*/
                            CreateDoor(DoorPosition, doorWidth, true, "Door");
                            continue;
                        }
                        if (rooms[j].xzPosition.z < rooms[i].xzPosition.z && rooms[j].xzPosition.z + rooms[j].height > rooms[i].xzPosition.z + rooms[i].height && rooms[i].height >= distance)
                        {
                            //  Debug.Log("左方相邻第四种情况");//调试用
                            DoorPosition = new Vector3(rooms[i].xzPosition.x, y / 2, rooms[i].xzPosition.z + rooms[i].height);
                            CreateDoor(DoorPosition, doorWidth, true, "Door");
                            continue;
                        }
                    }
                    //上方相邻
                    else if (Mathf.Abs(rooms[i].xzPosition.z + rooms[i].height - rooms[j].xzPosition.z) < 0.3f)
                    {
                        //  Debug.Log("在该房间上方相邻");//调试用
                        // 如果两个房间在 z 轴方向上相邻，检查两个房间相邻部分在x轴方向的差值.如果<2，那么我们不认为这两个房间是相邻的,因为有 1 的距离要用来放门
                        if (rooms[j].xzPosition.x < rooms[i].xzPosition.x && rooms[j].xzPosition.x + rooms[j].width > rooms[i].xzPosition.x && rooms[j].xzPosition.x + rooms[j].width - rooms[i].xzPosition.x >= distance)
                        {
                            DoorPosition = new Vector3((rooms[j].xzPosition.x + rooms[j].width + rooms[i].xzPosition.x) / 2, y / 2, rooms[i].xzPosition.z + rooms[i].height);
                            CreateDoor(DoorPosition, doorWidth, false, "Door");
                            continue;

                        }
                        else if (rooms[j].xzPosition.x < rooms[i].xzPosition.x + rooms[i].width && rooms[j].xzPosition.x + rooms[j].width > rooms[i].xzPosition.x + rooms[i].width && rooms[i].xzPosition.x + rooms[i].width - rooms[j].xzPosition.x >= distance)
                        {
                            DoorPosition = new Vector3((rooms[i].xzPosition.x + rooms[i].width + rooms[j].xzPosition.x) / 2, y / 2, rooms[i].xzPosition.z + rooms[i].height);
                            CreateDoor(DoorPosition, doorWidth, false, "Door");
                            continue;
                        }
                        else if (rooms[j].xzPosition.x >= rooms[i].xzPosition.x && rooms[j].xzPosition.x + rooms[j].width <= rooms[i].xzPosition.x + rooms[i].width && rooms[j].width >= distance)
                        {
                            DoorPosition = new Vector3(rooms[j].xzPosition.x + rooms[j].width / 2, y / 2, rooms[i].xzPosition.z + rooms[i].height);
                            CreateDoor(DoorPosition, doorWidth, false, "Door");
                            continue;
                        }
                        else if (rooms[j].xzPosition.x < rooms[i].xzPosition.x && rooms[j].xzPosition.x + rooms[j].width > rooms[i].xzPosition.x + rooms[i].width && rooms[i].width >= distance)
                        {
                            DoorPosition = new Vector3(rooms[i].xzPosition.x + rooms[i].width / 2, y / 2, rooms[i].xzPosition.z + rooms[i].height);
                            CreateDoor(DoorPosition, doorWidth, false, "Door");
                            continue;
                        }
                    }
                    //下方相邻
                   else if (Mathf.Abs(rooms[i].xzPosition.z - rooms[j].height - rooms[j].xzPosition.z) < 0.1f)
                    {
                        // 如果两个房间在 z 轴方向上相邻，检查两个房间相邻部分在x轴方向的差值.如果<2，那么我们不认为这两个房间是相邻的,因为有 1 的距离要用来放门
                        // Debug.Log("在该房间下方相邻");//调试用
                        if (rooms[j].xzPosition.x < rooms[i].xzPosition.x && rooms[j].xzPosition.x + rooms[j].width > rooms[i].xzPosition.x && rooms[j].xzPosition.x + rooms[j].width - rooms[i].xzPosition.x >= distance)
                        {
                            DoorPosition = new Vector3((rooms[j].xzPosition.x + rooms[j].width + rooms[i].xzPosition.x) / 2, y / 2, rooms[i].xzPosition.z);
                            CreateDoor(DoorPosition, doorWidth, false, "Door");
                            continue;
                        }
                        else if (rooms[j].xzPosition.x < rooms[i].xzPosition.x + rooms[i].width && rooms[j].xzPosition.x + rooms[j].width > rooms[i].xzPosition.x + rooms[i].width && rooms[i].xzPosition.x + rooms[i].width - rooms[j].xzPosition.x >= distance)
                        {
                            DoorPosition = new Vector3((rooms[j].xzPosition.x + rooms[j].width + rooms[i].xzPosition.x) / 2, y / 2, rooms[i].xzPosition.z);
                            CreateDoor(DoorPosition, doorWidth, false, "Door");
                            continue;
                        }
                        else if (rooms[j].xzPosition.x >= rooms[i].xzPosition.x && rooms[j].xzPosition.x + rooms[j].width <= rooms[i].xzPosition.x + rooms[i].width && rooms[j].width >= distance)
                        {
                            DoorPosition = new Vector3((rooms[j].xzPosition.x + rooms[j].width + rooms[i].xzPosition.x) / 2, y / 2, rooms[i].xzPosition.z);
                            CreateDoor(DoorPosition, doorWidth, false, "Door");
                            continue;
                        }
                        else if (rooms[j].xzPosition.x < rooms[i].xzPosition.x && rooms[j].xzPosition.x + rooms[j].width > rooms[i].xzPosition.x + rooms[i].width && rooms[i].width >= distance )
                        {
                            DoorPosition = new Vector3((rooms[j].xzPosition.x + rooms[j].width + rooms[i].xzPosition.x) / 2, y / 2, rooms[i].xzPosition.z);
                            CreateDoor(DoorPosition, doorWidth, false, "Door");
                            continue;
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
        Vector3 RightDoorPosition = new Vector3(EscapeRoom.xzPosition.x + EscapeRoom.width, wallHeight / 2, EscapeRoom.xzPosition.z + EscapeRoom.height / 2);
        // 上方门的位置
        Vector3 FrontDoorPosition = new Vector3(EscapeRoom.xzPosition.x + EscapeRoom.width / 2, wallHeight / 2, EscapeRoom.xzPosition.z + EscapeRoom.height);
        // 创建右方的逃生门
        CreateDoor(RightDoorPosition, 0.1f, true, "Exit");
        /* CreateDoor(FrontDoorPosition, 0.1f, false, "Exit");*/
        //Transform child = transform.Find("ChildName");
    }
    void CreateDoor(Vector3 position, float width, bool isHorizontal, String Tag)
    {//参数依次是 门的位置，门的宽度，门如何摆放，门的标签，门的高度默认是全局变量中的 y 值
     // 创建一个新的立方体（门），并将其设置为触发器
     //添加门的脚本,并执行相应的初始化函数
        GameObject door = GameObject.CreatePrimitive(PrimitiveType.Cube);
        door.transform.parent = ParentObject.transform;
        door.GetComponent<BoxCollider>().isTrigger = true;
        DoorControl thisDoor = door.AddComponent<DoorControl>();

        // 设置门的名称和标签
        if (Tag == "Door")
        {
            door.name = Tag + doorNum;
            doorNum++;
        }
        else { door.name = Tag; }
        door.tag = Tag;
        // 将门添加到待删除列表中
        AddObjectToList(door);
        // 设置门的位置
        door.transform.position = new Vector3(position.x, position.y, position.z);

        // 判断是否是横向门
        if (isHorizontal)
        {
            // 如果是横向门，设置门的大小
            // 0.3f 是门的厚度，y-1 是门的高度，1f 是门的宽度
            door.transform.localScale = new Vector3(0.4f, 3.2f, doorWidth);
            thisDoor.doorDirection = "Horizontal";
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
        else
        {
            // 如果是竖向门，设置门的大小
            // 1f 是门的宽度，y-1 是门的高度，0.3f 是门的厚度
            door.transform.localScale = new Vector3(doorWidth, 3.2f, 0.4f);
            thisDoor.doorDirection = "Vertical";
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
        thisDoor.AddNavMeshLink();


    }
}
