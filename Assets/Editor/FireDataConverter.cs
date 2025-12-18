using UnityEngine;
using UnityEditor;
using System.IO;
using System.Globalization;

public class FireDataConverter : EditorWindow
{
    [MenuItem("Tools/Convert Fire CSV to Binary")]
    public static void Convert()
    {
        string csvPath = Application.dataPath + "/Resources/FireData/MultiARL_Firedata.csv";
        string binaryPath = Application.dataPath + "/Resources/FireData/MultiARL_Firedata_byte.bytes";

        if (!File.Exists(csvPath))
        {
            Debug.LogError($"未找到文件: {csvPath}");
            return;
        }

        string[] lines = File.ReadAllLines(csvPath);

        using (FileStream fs = new FileStream(binaryPath, FileMode.Create))
        using (BinaryWriter writer = new BinaryWriter(fs))
        {
            // CSV结构: X,Y,Z,mol/mol,C,m,time
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                string[] cols = lines[i].Split(',');

                // 1. 解析基础坐标
                float x = float.Parse(cols[0], CultureInfo.InvariantCulture);
                float y_csv = float.Parse(cols[1], CultureInfo.InvariantCulture); // Unity Z
                float z_height = float.Parse(cols[2], CultureInfo.InvariantCulture); // Unity Y

                // 2. 解析数据列
                float densityRaw = float.Parse(cols[3], CultureInfo.InvariantCulture);
                float density = densityRaw * 1000000; // 烟雾浓度 (ppm)

                float valC = float.Parse(cols[4], CultureInfo.InvariantCulture); // 第4列 C
                float valM = float.Parse(cols[5], CultureInfo.InvariantCulture); // 第5列 m

                float time = float.Parse(cols[6], CultureInfo.InvariantCulture); // 第6列 Time

                // 阈值过滤：你可以根据需求决定是否保留空数据，这里为了保险暂时不过滤，或者仅过滤极小值
                // if (density < 0.01f && valC < 0.01f) continue; 

                // 3. 写入二进制 (顺序必须严格记忆!)
                writer.Write(time);      // 0
                writer.Write(x);         // 1
                writer.Write(y_csv);     // 2 (Unity Z)
                writer.Write(z_height);  // 3 (Unity Y)
                writer.Write(density);   // 4 (Smoke)
                writer.Write(valC);      // 5 (C) -> 新增
                writer.Write(valM);      // 6 (m) -> 新增
            }
        }
        AssetDatabase.Refresh();
        Debug.Log($"全量数据转换完成！已包含 C 和 m 列。");
    }
}