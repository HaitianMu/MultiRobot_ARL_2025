using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using CsvHelper;
using System.IO;
using CsvHelper.Configuration;
using System.Globalization;
using System;


/*Resources路径设置：

您的文件在 Assets/Resources/FireData/Plot3D_23.5.csv

Resources加载路径应该是："FireData/Plot3D_23.5"（不要包含.csv扩展名）

文件要求：

确保CSV文件在 Assets/Resources/ 或其子文件夹中

Unity会自动将.csv文件作为TextAsset处理*/


public class CSVRead : MonoBehaviour
{
    [Header("测试设置")]
    public string resourcePath = "FireData/FireData1"; // Resources下的路径
    public bool logDetailedData = true;

    public List<FireData> dataList = new List<FireData>();
    public Dictionary<Key,FireData> FireMap = new Dictionary<Key,FireData>();
    /*   void Start()
       {
           Debug.Log("=== 开始测试FireData读取 ===");
           Debug.Log($"资源路径: {resourcePath}");

           TestFireDataLoading();
       }*/

    public void TestFireDataLoading()
    {
        try
        {
            // 使用Resources.Load加载文本资源
            TextAsset csvFile = Resources.Load<TextAsset>(resourcePath);

            if (csvFile == null)
            {
                Debug.LogError($"找不到CSV文件: {resourcePath}");
                Debug.Log("请检查：");
                Debug.Log("1. 文件是否在Assets/Resources/FireData/文件夹中");
                Debug.Log("2. 文件名是否为Plot3D_23.5（不需要.csv扩展名）");
                Debug.Log("3. 文件格式是否为TextAsset");
                return;
            }

            Debug.Log($"成功加载文本资源，大小: {csvFile.bytes.Length} 字节");

            // 读取CSV数据
             LoadFireDataFromTextAsset(csvFile);

            Debug.Log($"=== 数据加载完成 ===");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"加载数据时出错: {ex.Message}");
            Debug.LogError($"堆栈跟踪: {ex.StackTrace}");
        }
    }

    private void LoadFireDataFromTextAsset(TextAsset textAsset)
    {
        print("开始读取数据");
        using (var reader = new StringReader(textAsset.text))
        using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            Delimiter = ",",
            MissingFieldFound = null,
            BadDataFound = null,
            HeaderValidated = null,
            PrepareHeaderForMatch = args => args.Header.Trim()
        }))

        {
            // 读取第一行表头
            bool hasFirstLine = csv.Read();
            print($"读取第一行结果: {hasFirstLine}");
            if (hasFirstLine)
            {
                csv.ReadHeader();
                print("第一行表头: " + string.Join("|", csv.HeaderRecord));
            }

            // 跳过第二行表头（单位行）
            csv.Read();

            print("跳过第二行表头");

            while (csv.Read())
            {
                FireData fTemp = new FireData();
                Key kTem = new Key();//将键值设置为人类的坐标和时间
                try
                {
                    fTemp.X = float.Parse(csv.GetField(0), CultureInfo.InvariantCulture);
                    fTemp.Y = float.Parse(csv.GetField(1), CultureInfo.InvariantCulture);
                    fTemp.Z = float.Parse(csv.GetField(2), CultureInfo.InvariantCulture);
                    fTemp.COConcentration = float.Parse(csv.GetField(3), CultureInfo.InvariantCulture);
                    fTemp.Temperature = float.Parse(csv.GetField(4), CultureInfo.InvariantCulture);
                    fTemp.Visibility = float.Parse(csv.GetField(5), CultureInfo.InvariantCulture);


                    kTem.X = float.Parse(csv.GetField(0), CultureInfo.InvariantCulture);
                    kTem.Y = float.Parse(csv.GetField(1), CultureInfo.InvariantCulture);
                    kTem.Z = 0f;
                    kTem.Time = float.Parse(csv.GetField(6), CultureInfo.InvariantCulture);
                    if (fTemp.Z == 1.0f)//只保留Z=1处的数据
                    {
                        FireMap.Add(kTem, fTemp);

                        //print($"key是："+kTem+$"，保存的数据是："+fTemp);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"解析行失败: {ex.Message}");
                    continue;
                }
            }
        }
    }
}
   
   
