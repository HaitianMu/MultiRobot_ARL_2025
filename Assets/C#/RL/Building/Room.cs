using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Room : MonoBehaviour//为房间增加一些属性，比如房间的坐标、大小，以及它的邻居列表（用于记录相邻房间）。
{
    public Vector3 xzPosition;          // Lower-left corner coordinates of the room
    public float width;                // Width of the room
    public float height;               // Height of the room
    /*!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!! Added on 3.12 */
    public float roomSize;            // Size of the room (area)
    /* !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!! Added on 4.4 */
    public String roomName;          // Name of the room
    public string[] ConnectedRoom;    // Names of adjacent rooms

    public Room() { }
    public Room(GameObject roomObject, Vector3 position, float width, float height)
    {
        this.xzPosition = position;
        this.width = width;
        this.height = height;
    }

    public Room(Vector3 position, float width, float height, float roomsize)
    {
        this.xzPosition = position;
        this.width = width;
        this.height = height;
        roomSize = roomsize;
    }
    // 计算房间的对角线长度（权重）

    // 方法：检查与另一个房间是否相邻（基于房间位置和大小）
    public bool IsAdjacentTo(Room other)  //判断该房间是否与另外一个房间相邻,当两个房间相邻的长度大于Distance时，我们才认为这两个房间相邻
    {
        // 假设房间是矩形的，我们检查是否有一个相邻的面
        float Distance = 5.0f;
        bool isAdjacent = false;

        // 检查左右相邻（假设x为水平方向，y为垂直方向，z为深度方向）
        if (Mathf.Abs(this.xzPosition.x + this.width - other.xzPosition.x) < 0.1f || Mathf.Abs(other.xzPosition.x + other.width - this.xzPosition.x) < 0.1f)
        {
            // 如果两个房间在 x 轴方向上相邻,检查两个房间相邻部分在z轴方向的差值.如果<2，那么我们不认为这两个房间是相邻的,因为有 1 的距离要用来放门
            //总体可分为三种情况
            if (other.xzPosition.z <= this.xzPosition.z && other.xzPosition.z + other.height >= this.xzPosition.z && other.xzPosition.z + other.height - this.xzPosition.z >= Distance)
            {
                isAdjacent = true;
                return isAdjacent;
            }
            else if (other.xzPosition.z <= this.xzPosition.z + height && other.xzPosition.z + other.height >= this.xzPosition.z + height && this.xzPosition.z + this.height - other.xzPosition.z >= Distance)
            {
                isAdjacent = true;
                return isAdjacent;
            }
            else if (other.xzPosition.z >= this.xzPosition.z && other.xzPosition.z + other.height <= this.xzPosition.z + this.height && other.height >= Distance)
            {
                isAdjacent = true;
                return isAdjacent;
            }
            if (other.xzPosition.z <= xzPosition.z || other.xzPosition.z + other.height >= xzPosition.z + height || height >= 0.2)
            {

            }
            else { return isAdjacent; }

        }
        // 检查上下相邻
        else if (Mathf.Abs(this.xzPosition.z + this.height - other.xzPosition.z) < 0.1f || Mathf.Abs(other.xzPosition.z + other.height - this.xzPosition.z) < 0.1f)
        {
            // 如果两个房间在 z 轴方向上相邻，检查两个房间相邻部分在x轴方向的差值.如果<2，那么我们不认为这两个房间是相邻的,因为有 1 的距离要用来放门
            if (other.xzPosition.x <= this.xzPosition.x && other.xzPosition.x + other.width >= this.xzPosition.x && other.xzPosition.x + other.width - this.xzPosition.x >= Distance)
            {
                isAdjacent = true;
                return isAdjacent;
            }
            else if (other.xzPosition.x <= this.xzPosition.x + this.width && other.xzPosition.x + other.width >= this.xzPosition.x + this.width && xzPosition.x + width - other.xzPosition.x >= Distance)
            {
                isAdjacent = true;
                return isAdjacent;
            }
            else if (other.xzPosition.x >= this.xzPosition.x && other.xzPosition.x + other.width <= this.xzPosition.x + width && other.width >= Distance)
            {
                isAdjacent = true;
                return isAdjacent;
            }
            else if (other.xzPosition.x <= xzPosition.x && other.xzPosition.x + other.width >= xzPosition.x + width && width >= 0.2)
            {
                isAdjacent = true;
                return isAdjacent;
            }

            else { return isAdjacent; }
        }
        return isAdjacent;
    }
}
