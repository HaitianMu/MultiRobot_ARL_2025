using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public partial class EnvControl : MonoBehaviour
{
    // 奖励统计可视化
    private Dictionary<string, float> rewardLog = new Dictionary<string, float>();

    [System.Serializable]
    private class RewardData
    {
        public string timestamp;
        public Dictionary<string, float> rewards;
    }
    public void LogReward(string type, float value)
    {
        rewardLog.TryGetValue(type, out float current);
        rewardLog[type] = current + value;
    }

    void OnDestroy()
    {
        // 定义保存路径（使用persistentDataPath）
        string directoryPath = Path.Combine(Application.persistentDataPath, "Test_Data");
        string filePath = Path.Combine(directoryPath, $"DifferentScene_{DateTime.Now:yyyyMMdd_HHmmss}.json");//名称要不一样，不然会把之前的覆盖

        try
        {
            // 确保目录存在
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            string result = "";
            foreach (var kv in rewardLog)
            {
                Debug.Log($"{kv.Key}: {kv.Value}");
                result += $"{kv.Key}: {kv.Value}\n";
            }
            // 写入文件
            File.WriteAllText(filePath, result);

            // 日志输出
            Debug.Log($"测试数据已保存到: {filePath}\n{result}");
        }
        catch (Exception e)
        {
            Debug.LogError($"保存奖励数据失败: {e.Message}");
        }
    }
}
