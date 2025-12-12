using UnityEngine;
using UnityEditor;
using System.IO;
using System.Globalization;
using System.Collections.Generic;

public class FireDataConverter : EditorWindow
{
    [MenuItem("Tools/Convert Fire CSV to Binary")]
    public static void Convert()
    {
        // 1. 设置路径 (请修改为你实际的CSV路径)
        string csvPath = Application.dataPath + "/Resources/FireData/MultiARL_Firedata.csv";
        string binaryPath = Application.dataPath + "/Resources/FireData/MultiARL_Firedata.bytes";

        string[] lines = File.ReadAllLines(csvPath);

        // 使用BinaryWriter写入数据
        using (FileStream fs = new FileStream(binaryPath, FileMode.Create))
        using (BinaryWriter writer = new BinaryWriter(fs))
        {
            // 写入行数（或者你可以先写入版本号等头信息）
            // 这里我们直接写数据
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                // 优化：使用Span或直接处理字符串避免GC，但在Editor里慢点没关系
                string[] cols = lines[i].Split(',');

                // 解析数据
                float x = float.Parse(cols[0], CultureInfo.InvariantCulture);
                float y_csv = float.Parse(cols[1], CultureInfo.InvariantCulture);
                float z_height = float.Parse(cols[2], CultureInfo.InvariantCulture);
                float density = float.Parse(cols[3], CultureInfo.InvariantCulture) * 1000000;
                float time = float.Parse(cols[6], CultureInfo.InvariantCulture);

                if (density < 0.01f) continue;

                // 写入二进制：不需要写 key 名称，只写值，顺序要记住！
                writer.Write(time);     // 0: time
                writer.Write(x);        // 1: x
                writer.Write(y_csv);    // 2: y (csv)
                writer.Write(z_height); // 3: z
                writer.Write(density);  // 4: density
            }
        }
        AssetDatabase.Refresh();
        Debug.Log("转换完成！二进制文件已生成。");
    }
}