using System.Collections.Generic;
using UnityEngine;

public class SpatialPartition
{
    private float cellSize;
    private Dictionary<Vector3Int, List<FireControl>> grid = new Dictionary<Vector3Int, List<FireControl>>();

    public SpatialPartition(float cellSize)
    {
        this.cellSize = cellSize;
    }

    private Vector3Int PositionToCell(Vector3 position)
    {
        return new Vector3Int(
            Mathf.FloorToInt(position.x / cellSize),
            Mathf.FloorToInt(position.y / cellSize),
            Mathf.FloorToInt(position.z / cellSize));
    }

    public void Add(Vector3 position, FireControl fire)
    {
        Vector3Int cell = PositionToCell(position);

        if (!grid.ContainsKey(cell))
        {
            grid[cell] = new List<FireControl>();
        }

        grid[cell].Add(fire);
    }

    public void Remove(Vector3 position)
    {
        Vector3Int cell = PositionToCell(position);

        if (grid.ContainsKey(cell))
        {
            grid[cell].RemoveAll(f => f == null);
        }
    }

    public bool HasNearbyFire(Vector3 position, float radius)
    {
        int radiusInCells = Mathf.CeilToInt(radius / cellSize);
        Vector3Int centerCell = PositionToCell(position);

        for (int x = -radiusInCells; x <= radiusInCells; x++)
        {
            for (int y = -radiusInCells; y <= radiusInCells; y++)
            {
                for (int z = -radiusInCells; z <= radiusInCells; z++)
                {
                    Vector3Int cell = centerCell + new Vector3Int(x, y, z);

                    if (grid.TryGetValue(cell, out List<FireControl> fires))
                    {
                        foreach (FireControl fire in fires)
                        {
                            if (fire != null && Vector3.Distance(position, fire.transform.position) <= radius)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
        }

        return false;
    }
}