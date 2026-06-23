using UnityEngine;

public class Node
{
    public bool Walkable;
    public Vector2 WorldPosition;
    public int GridX;
    public int GridY;

    public int GCost;
    public int HCost;
    public int Penalty; // Diem phat neu dung gan tuong
    public Node Parent;

    public Node(bool walkable, Vector2 worldPos, int gridX, int gridY)
    {
        Walkable = walkable;
        WorldPosition = worldPos;
        GridX = gridX;
        GridY = gridY;
    }

    public int FCost
    {
        get { return GCost + HCost; }
    }
}
