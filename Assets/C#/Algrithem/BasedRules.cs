using System.Collections.Generic;
using UnityEngine;

public class BasedRules: MonoBehaviour
{
    public int gridSize = 10; // 网格大小
    public int roomCount = 5; // 房间数量
    public int doorCount = 4; // 门数量
    public int exitCount = 2; // 出口数量
    public GameObject roomPrefab; // 房间预制体
    public GameObject doorPrefab; // 门预制体
    public GameObject wallPrefab; // 墙预制体
    public GameObject exitPrefab; // 出口预制体

    private enum CellType { Room, Door, Wall, Exit }
    private CellType[,] grid; // 网格

    void Start()
    {
        InitializeGrid();
        GenerateLayout();
        RenderGrid();
    }

    // 初始化网格
    void InitializeGrid()
    {
        grid = new CellType[gridSize, gridSize];
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                grid[x, y] = CellType.Wall; // 初始化为墙
            }
        }
    }

    // 生成布局
    void GenerateLayout()
    {
        // 放置房间
        for (int i = 0; i < roomCount; i++)
        {
            Vector2Int roomPos = GetRandomPosition();
            grid[roomPos.x, roomPos.y] = CellType.Room;
        }

        // 放置门
        for (int i = 0; i < doorCount; i++)
        {
            Vector2Int doorPos = GetRandomPosition();
            if (IsAdjacentToRoom(doorPos))
            {
                grid[doorPos.x, doorPos.y] = CellType.Door;
            }
        }

        // 放置出口
        for (int i = 0; i < exitCount; i++)
        {
            Vector2Int exitPos = GetRandomEdgePosition();
            grid[exitPos.x, exitPos.y] = CellType.Exit;
        }
    }

    // 获取随机位置
    Vector2Int GetRandomPosition()
    {
        int x = Random.Range(0, gridSize);
        int y = Random.Range(0, gridSize);
        return new Vector2Int(x, y);
    }

    // 获取随机边缘位置
    Vector2Int GetRandomEdgePosition()
    {
        int edge = Random.Range(0, 4);
        int x = 0, y = 0;
        switch (edge)
        {
            case 0: x = 0; y = Random.Range(0, gridSize); break; // 左边缘
            case 1: x = gridSize - 1; y = Random.Range(0, gridSize); break; // 右边缘
            case 2: x = Random.Range(0, gridSize); y = 0; break; // 下边缘
            case 3: x = Random.Range(0, gridSize); y = gridSize - 1; break; // 上边缘
        }
        return new Vector2Int(x, y);
    }

    // 检查是否与房间相邻
    bool IsAdjacentToRoom(Vector2Int pos)
    {
        int x = pos.x;
        int y = pos.y;
        if (x > 0 && grid[x - 1, y] == CellType.Room) return true;
        if (x < gridSize - 1 && grid[x + 1, y] == CellType.Room) return true;
        if (y > 0 && grid[x, y - 1] == CellType.Room) return true;
        if (y < gridSize - 1 && grid[x, y + 1] == CellType.Room) return true;
        return false;
    }

    // 渲染网格
    void RenderGrid()
    {
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                Vector3 position = new Vector3(x, y, 0);
                switch (grid[x, y])
                {
                    case CellType.Room:
                        Instantiate(roomPrefab, position, Quaternion.identity);
                        break;
                    case CellType.Door:
                        Instantiate(doorPrefab, position, Quaternion.identity);
                        break;
                    case CellType.Wall:
                        Instantiate(wallPrefab, position, Quaternion.identity);
                        break;
                    case CellType.Exit:
                        Instantiate(exitPrefab, position, Quaternion.identity);
                        break;
                }
            }
        }
    }
}