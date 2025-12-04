using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.IO;
using System.Linq;
using System.Collections.ObjectModel;
using TMPro;

public class JsonLoad : MonoBehaviour
{
    public Material lineMaterial; // 线条材质（在Inspector中指定）
    private Dictionary<string, RoomData> roomsDict = new Dictionary<string, RoomData>();

    [System.Serializable]
    public class Vector3Serializable
    {
        public float x;
        public float y;
        public float z;

        public Vector3 ToVector3() => new Vector3(x, y, z);
    }

    [System.Serializable]
    public class RoomData
    {
        public string id;
        public string name;
        public Vector3Serializable xzPosition;
        public float width;
        public float height;
        public float roomSize;
        public string[] connectedRooms;
    }

    [System.Serializable]
    public class Layout
    {
        public string name;
        public RoomData[] rooms;
    }

    [System.Serializable]
    public class LayoutCollection
    {
        public List<Layout> layouts;
    }

    void Start()
    {
        string[] names = { "layout1" , "layout2", "office_layout2", "supermarket_layout", "university_library", "apartment_complex", "hospital_floor", "SEU_LiWenzheng_Library_Floor1" }; // 声明一个长度为10的字符串数组

        LoadAndDrawRooms("layout_complex_50_sector_alpha_fixed");
    }

    private void LoadAndDrawRooms(string targetLayoutName)
    {
        // 1. 加载JSON文件（路径可以是 StreamingAssets 或 Resources）
        TextAsset jsonFile = Resources.Load<TextAsset>("layout");
        if (jsonFile == null)
        {
            Debug.LogError("JSON 文件未找到！请检查：\n" +
                         "- 文件是否在 Assets/Resources 内\n" +
                         "- 文件名是否完全匹配（包括大小写）\n" +
                         "- 文件扩展名是否为 .json");
            return;
        }

        // 2. 使用 Newtonsoft.Json 解析，读取json文件这一步没问题。那应该就是解析的时候出错了
        try
        {
            LayoutCollection data = JsonConvert.DeserializeObject<LayoutCollection>(jsonFile.text);
           // Debug.Log("原始JSON内容:\n" + jsonFile.text);
            if (data == null || data.layouts == null)
            {
                Debug.LogError("JSON解析失败，请检查格式是否正确");
                return;
            }


            // 查找指定的 Layout
            Layout targetLayout = data.layouts.Find(layout => layout.name == targetLayoutName);
            if (targetLayout == null )
            {
                Debug.LogError("没有找到目标布局，请检查提供的布局名称是否正确");
                return;
            }


            // 画出布局的边界线
                foreach (RoomData room in targetLayout.rooms)
                {
                    if (!roomsDict.ContainsKey(room.id))
                    {
                        roomsDict.Add(room.id, room);
                        DrawRoom(room);
                        DrawRoomLabel(room); // 新增标签绘制函数
                }

                    /*// 调试信息
                    Debug.Log($"房间ID: {room.id}, 名称: {room.name}");
                    Debug.Log($"位置: X={room.xzPosition.x}, Z={room.xzPosition.z}");
                    Debug.Log($"尺寸: 宽={room.width}, 高={room.height}");
                    Debug.Log($"连接房间: {string.Join(", ", room.connectedRooms)}");*/
                }
            }

        catch (System.Exception e)
        {
            Debug.LogError($"解析失败: {e.Message}\nJSON 内容:\n{jsonFile.text}");
        }
    }


    private void DrawRoom(RoomData data)
    {
        Vector3[] corners = new Vector3[5] {
            new Vector3(data.xzPosition.x, 0, data.xzPosition.z),
            new Vector3(data.xzPosition.x + data.width, 0, data.xzPosition.z),
            new Vector3(data.xzPosition.x + data.width, 0, data.xzPosition.z + data.height),
            new Vector3(data.xzPosition.x, 0, data.xzPosition.z + data.height),
            new Vector3(data.xzPosition.x, 0, data.xzPosition.z) // 闭合
        };

        GameObject lineObj = new GameObject($"Room_{data.id}_Border");
        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.material = lineMaterial;

        lr.startWidth = lr.endWidth = 0.3f;
        lr.positionCount = corners.Length;
        lr.SetPositions(corners);
    }

    private void DrawRoomLabel(RoomData room)
    {
        // 计算标签位置（房间中心点）
        Vector3 labelPosition = new Vector3(
            room.xzPosition.x + room.width / 2f,
            0,
            room.xzPosition.z + room.height / 2f
        );

        // 创建文本标签
        GameObject labelObj = new GameObject($"Label_{room.id}");
       
        labelObj.transform.position = labelPosition;
        labelObj.transform.SetParent(this.transform); // 设为当前物体的子物体
                                                      // 强制旋转为朝上（Y轴向上，X/Z轴归零）
        labelObj.transform.rotation = Quaternion.Euler(90, 0, 0); // X轴旋转90度（3D文本默认朝前）
        // 添加文本组件

        // 使用预设的中文字体资产
        TMP_FontAsset chineseFont = Resources.Load<TMP_FontAsset>("Resources/KaiTi SDF");
        TextMeshPro labelText = labelObj.AddComponent<TextMeshPro>();
        labelText.font = chineseFont;
        labelText.text = room.id;
        labelText.fontSize = 15f;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = Color.white;
        
        
    }
}