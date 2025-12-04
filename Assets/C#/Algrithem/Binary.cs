using System.Collections.Generic;
using UnityEngine;

public class Binary : MonoBehaviour
{
    public int n = 10; // 分割数量
    public float minSize = 5f; // 最小面积
    public float minWidth = 3f; // 最小宽度
    public float minHeight = 3f; // 最小高度
    public Vector2 startPoint = new Vector2(0, 0); // 矩形左下角起点
    public float width = 100f; // 矩形宽度
    public float height = 100f; // 矩形高度

    private List<Rect> rectangles = new List<Rect>();

    void Start()
    {
        // 调用分割函数
        rectangles = SplitSpace(new Rect(startPoint.x, startPoint.y, width, height), n, minSize);
    }

    void OnDrawGizmos()
    {
        // 绘制所有矩形
        Gizmos.color = Color.green;
        foreach (var rect in rectangles)
        {
            DrawRect(rect);
        }
    }

    // 递归分割函数
    List<Rect> SplitSpace(Rect rect, int n, float minSize)
    {
        List<Rect> result = new List<Rect>();

        // 递归终止条件
        if (n == 1)
        {
            if (IsValidRectangle(rect, minSize))
            {
                result.Add(rect);
            }
            return result;
        }

        // 检查当前矩形是否有效
        if (!IsValidRectangle(rect, minSize))
        {
            return result;
        }

        // 选择分割方向
        bool splitVertical = Random.Range(0, 2) == 0; // 随机选择垂直或水平分割

        if (splitVertical)  //垂直分割，图形很长
        {
            // 垂直分割：随机选择分割点
            float minSplit = +minWidth; // 最小分割宽度
            float maxSplit = rect.width - minWidth; // 最大分割宽度
            float splitWidth = Random.Range(minSplit, maxSplit); // 随机分割点

            Rect leftRect = new Rect(rect.x, rect.y, splitWidth, rect.height);
            Rect rightRect = new Rect(rect.x + splitWidth, rect.y, rect.width - splitWidth, rect.height);

            result.AddRange(SplitSpace(leftRect, n / 2, minSize));
            result.AddRange(SplitSpace(rightRect, n - n / 2, minSize));
        }
        else
        {
            // 水平分割：随机选择分割点
            float minSplit = minHeight; // 最小分割高度
            float maxSplit = rect.height - minHeight; // 最大分割高度
            float splitHeight = Random.Range(minSplit, maxSplit); // 随机分割点

            Rect bottomRect = new Rect(rect.x, rect.y, rect.width, splitHeight);
            Rect topRect = new Rect(rect.x, rect.y + splitHeight, rect.width, rect.height - splitHeight);

            result.AddRange(SplitSpace(bottomRect, n / 2, minSize));
            result.AddRange(SplitSpace(topRect, n - n / 2, minSize));
        }

        return result;
    }

    // 检查矩形是否满足条件
    bool IsValidRectangle(Rect rect, float minSize)
    {
        float area = rect.width * rect.height;
        float aspectRatio = rect.width / rect.height;

        return area >= minSize &&
               rect.width >= minWidth &&
               rect.height >= minHeight &&
               aspectRatio >= 0.5f && aspectRatio <= 2f;
    }

    // 绘制矩形
    void DrawRect(Rect rect)
    {
        Vector3 bottomLeft = new Vector3(rect.x, rect.y, 0);
        Vector3 topLeft = new Vector3(rect.x, rect.y + rect.height, 0);
        Vector3 topRight = new Vector3(rect.x + rect.width, rect.y + rect.height, 0);
        Vector3 bottomRight = new Vector3(rect.x + rect.width, rect.y, 0);

        Gizmos.DrawLine(bottomLeft, topLeft);
        Gizmos.DrawLine(topLeft, topRight);
        Gizmos.DrawLine(topRight, bottomRight);
        Gizmos.DrawLine(bottomRight, bottomLeft);
    }
}