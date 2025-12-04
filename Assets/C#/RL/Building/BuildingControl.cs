using System;
using System.Collections.Generic;
using UnityEngine;
public partial class BuildingControl : MonoBehaviour
{

    /*........................一、房间生成用到的数据结构.....................................*/
    private float totalArea; // 总区域大小
    public int roomNum=10; //需要生成的房间数量
    public float totalWidth;//用于记录整个区域的宽
    public float totalHeight;//用于记录整个区域的高


    public float[] roomAreas;// 输入的房间面积数组（已知数组）
    public int RoomNum = 0;//记录已经生成的房间数量,用于给房间编号  
    public int doorNum = 1;//记录已经生成的门数量,用于给门编号
    float y = 3.0f;//墙体的高度 ,
    float doorWidth = 1.5f;//门的宽度

    private GameObject[] AllObjects; //存储单次生成的所有物体，这个数据用于多次生成环境时，清空之前的场景
    public Material Floor;
    public Material Door;
    public Material Exit;
    public Material Wall;
    /*.............................二、房间之间生成门用到的数据结构................................*/

    public float minWidth = 2.5f;
    public float minheight = 2.5f;


    [System.Serializable]
    public class Vector3Serializable
    {
        public float x;
        public float y;
        public float z;

        public Vector3 ToVector3() => new Vector3(x, y, z);
    } 
    public List<Room> roomList = new List<Room>();  // 存储生成的所有房间对象

    /*.............................三、生成导航使用的数据结构................................*/

    public GameObject ParentObject;

    }
