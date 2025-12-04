using CsvHelper.Configuration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;

public class FireData { 
    // 空间坐标 (单位: m)
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }

    //CO浓度 (单位: mol/mol)
    public float COConcentration { get; set; }

    // 温度 (单位: °C)
    public float Temperature { get; set; }

    // 能见度 (单位: m)
    public float Visibility { get; set; }
    public override string ToString()
    {
        return $"Position: ({X}, {Y}, {Z}), CO: {COConcentration}, Temp: {Temperature}°C, Visibility: {Visibility}m";
    }
}

public class Key  //键类必须重写equeals和hashcode函数
{

    // 空间坐标 (单位: m)
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Time { get;  set; }
    // 必须重写 Equals 方法
    public override bool Equals(object obj)
    {
        if (obj is Key other)
        {
            // 使用适当的精度比较浮点数
            return Mathf.Approximately(X, other.X) &&
                   Mathf.Approximately(Y, other.Y) &&
                   Mathf.Approximately(Z, other.Z) &&
                   Mathf.Approximately(Time, other.Time);
        }
        return false;
    }

    // 必须重写 GetHashCode 方法
    public override int GetHashCode()
    {
        // 使用 HashCode.Combine 并适当处理浮点数精度
        return HashCode.Combine(
            Mathf.Round(X * 1000f), // 保留3位小数精度
            Mathf.Round(Y * 1000f),
            Mathf.Round(Z * 1000f),
            Mathf.Round(Time * 1000f)
        );
    }

    // 可选：重写 ToString 方法便于调试
    public override string ToString()
    {
        return $"Key(X: {X:F2}, Y: {Y:F2}, Z: {Z:F2}, Time: {Time:F2})";
    }
}
