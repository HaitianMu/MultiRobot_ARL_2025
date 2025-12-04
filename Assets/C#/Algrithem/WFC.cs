using UnityEngine;

public class WFC : MonoBehaviour
{
    public int gridSize = 20; // 网格大小
    public int maxRooms = 10;  // 最大房间数量
    public int roomMinSize = 5;  // 房间最小尺寸
    public int roomMaxSize = 40;  // 房间最大尺寸
    public int maxDoors = 10;  // 最大门数量
    public int maxExits = 1;  // 固定一个出口

    private enum CellType { Floor, Wall, Door, Exit, Collapsed }
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

        // 初始化整个网格为地板
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                grid[x, y] = CellType.Floor;
            }
        }

        // 四周设置为墙
        for (int x = 0; x < gridSize; x++)
        {
            grid[x, 0] = CellType.Wall;
            grid[x, gridSize - 1] = CellType.Wall;
        }
        for (int y = 0; y < gridSize; y++)
        {
            grid[0, y] = CellType.Wall;
            grid[gridSize - 1, y] = CellType.Wall;
        }
    }

    // 生成布局
    void GenerateLayout()
    {
        // 固定一个出口
        Vector2Int exitPos = GetRandomEdgePosition();
        grid[exitPos.x, exitPos.y] = CellType.Exit;

        // 放置房间（地板），并生成墙和分隔
        for (int i = 0; i < maxRooms; i++)
        {
            Vector2Int roomPos = GetRandomPosition();
            Vector2Int roomSize = new Vector2Int(Random.Range(roomMinSize, roomMaxSize), Random.Range(roomMinSize, roomMaxSize));
            if (CanPlaceRoom(roomPos, roomSize))
            {
                PlaceRoom(roomPos, roomSize);
            }
        }

        // 在房间之间生成墙壁（分隔房间）
        GenerateWalls();

        // 放置门
        for (int i = 0; i < maxDoors; i++)
        {
            Vector2Int doorPos = GetRandomPosition();
            if (IsAdjacentToFloor(doorPos) && CanPlaceDoor(doorPos))
            {
                grid[doorPos.x, doorPos.y] = CellType.Door;
            }
        }
    }

    // 获取随机位置
    Vector2Int GetRandomPosition()
    {
        int x = Random.Range(1, gridSize - 1); // 避免生成在四周的墙上
        int y = Random.Range(1, gridSize - 1);
        return new Vector2Int(x, y);
    }

    // 获取随机边缘位置
    Vector2Int GetRandomEdgePosition()
    {
        int edge = Random.Range(0, 4);
        int x = 0, y = 0;
        switch (edge)
        {
            case 0: x = 0; y = Random.Range(1, gridSize - 1); break; // 左边缘
            case 1: x = gridSize - 1; y = Random.Range(1, gridSize - 1); break; // 右边缘
            case 2: x = Random.Range(1, gridSize - 1); y = 0; break; // 下边缘
            case 3: x = Random.Range(1, gridSize - 1); y = gridSize - 1; break; // 上边缘
        }
        return new Vector2Int(x, y);
    }

    // 检查是否能放置房间
    bool CanPlaceRoom(Vector2Int position, Vector2Int roomSize)
    {
        for (int x = position.x; x < position.x + roomSize.x; x++)
        {
            for (int y = position.y; y < position.y + roomSize.y; y++)
            {
                if (x >= gridSize || y >= gridSize || grid[x, y] != CellType.Floor)
                    return false;
            }
        }
        return true;
    }

    // 放置房间
    void PlaceRoom(Vector2Int position, Vector2Int roomSize)
    {
        // 将房间区域设置为地板
        for (int x = position.x; x < position.x + roomSize.x; x++)
        {
            for (int y = position.y; y < position.y + roomSize.y; y++)
            {
                grid[x, y] = CellType.Floor;
            }
        }

        // 设置房间四周为墙
        for (int x = position.x - 1; x < position.x + roomSize.x + 1; x++)
        {
            for (int y = position.y - 1; y < position.y + roomSize.y + 1; y++)
            {
                if (x >= 0 && y >= 0 && x < gridSize && y < gridSize)
                {
                    if (x == position.x - 1 || x == position.x + roomSize.x || y == position.y - 1 || y == position.y + roomSize.y)
                    {
                        grid[x, y] = CellType.Wall; // 墙
                    }
                }
            }
        }
    }

    // 生成连续的墙壁，分隔房间
    void GenerateWalls()
    {
        // 随机生成一些墙壁来分隔房间，使得房间布局更加复杂
        for (int x = 1; x < gridSize - 1; x++)
        {
            for (int y = 1; y < gridSize - 1; y++)
            {
                if (grid[x, y] == CellType.Floor && Random.Range(0, 10) > 7) // 随机生成墙壁
                {
                    grid[x, y] = CellType.Wall;
                }
            }
        }
    }

    // 检查是否与地板相邻
    bool IsAdjacentToFloor(Vector2Int pos)
    {
        int x = pos.x;
        int y = pos.y;
        if (x > 0 && grid[x - 1, y] == CellType.Floor) return true;
        if (x < gridSize - 1 && grid[x + 1, y] == CellType.Floor) return true;
        if (y > 0 && grid[x, y - 1] == CellType.Floor) return true;
        if (y < gridSize - 1 && grid[x, y + 1] == CellType.Floor) return true;
        return false;
    }

    // 检查门的放置条件
    bool CanPlaceDoor(Vector2Int doorPos)
    {
        // 检查门的四个方向，至少有两个方向是墙，并且是相对的墙
        bool topWall = doorPos.y < gridSize - 1 && grid[doorPos.x, doorPos.y + 1] == CellType.Wall;
        bool bottomWall = doorPos.y > 0 && grid[doorPos.x, doorPos.y - 1] == CellType.Wall;
        bool leftWall = doorPos.x > 0 && grid[doorPos.x - 1, doorPos.y] == CellType.Wall;
        bool rightWall = doorPos.x < gridSize - 1 && grid[doorPos.x + 1, doorPos.y] == CellType.Wall;

        // 至少有两个方向是墙，且必须是相对的墙
        return (topWall && bottomWall) || (leftWall && rightWall);
    }

    // 渲染网格
    void RenderGrid()
    {
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                Vector3 position = new Vector3(x, y, 0);
                GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Quad);
                tile.transform.position = position;

                switch (grid[x, y])
                {
                    case CellType.Floor:
                        tile.GetComponent<Renderer>().material.color = Color.green; // 地板
                        break;
                    case CellType.Wall:
                        tile.GetComponent<Renderer>().material.color = Color.gray; // 墙
                        break;
                    case CellType.Door:
                        tile.GetComponent<Renderer>().material.color = Color.blue; // 门
                        break;
                    case CellType.Exit:
                        tile.GetComponent<Renderer>().material.color = Color.red; // 出口
                        break;
                    case CellType.Collapsed:
                        tile.GetComponent<Renderer>().material.color = Color.black; // 坍塌
                        break;
                }
            }
        }
    }
}
