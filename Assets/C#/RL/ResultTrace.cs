using System.IO;
using System.Text;
using UnityEngine;

public static class CSVLogger
{
    private static string filePath;

    // 记录最终统计结果
    public static void LogFinalResult(string panicState, int totalEpisodes, float finalSuccessRate, float finalAvgTime, float finalAvgHealth)
    {
        // 文件名：FinalResult_TestType_时间.csv
        string fileName = $"FinalResult_{panicState}_{System.DateTime.Now:MMdd_HHmm}.csv";

#if UNITY_EDITOR
        filePath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", fileName);
#else
        filePath = Path.Combine(Application.persistentDataPath, fileName);
#endif

        string directory = Path.GetDirectoryName(filePath);
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

        // 写入内容
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("PanicMode,TotalEpisodes,SuccessRate(%),AvgTime(s),AvgHealth"); // 表头
        sb.AppendLine($"{panicState},{totalEpisodes},{finalSuccessRate:F2},{finalAvgTime:F2},{finalAvgHealth:F2}"); // 数据

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        Debug.Log($"[Test Finished] Final Report Saved to: {filePath}");
    }
}