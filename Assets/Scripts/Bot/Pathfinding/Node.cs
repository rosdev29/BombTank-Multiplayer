using System;
using UnityEngine;

public class Node : IHeapItem<Node>
{
    public bool    Walkable;
    public Vector2 WorldPosition;
    public int     GridX;
    public int     GridY;

    public int  GCost;
    public int  HCost;
    public int  Penalty; // Điểm phạt khi đi gần tường
    public Node Parent;

    // ── IHeapItem ─────────────────────────────────────────────────────────────
    public int HeapIndex { get; set; } = -1;

    public int CompareTo(Node other)
    {
        int cmp = FCost.CompareTo(other.FCost);
        if (cmp == 0) cmp = HCost.CompareTo(other.HCost);
        return cmp; // min-heap: nhỏ hơn = ưu tiên cao hơn
    }

    // ── Constructor ───────────────────────────────────────────────────────────
    public Node(bool walkable, Vector2 worldPos, int gridX, int gridY)
    {
        Walkable      = walkable;
        WorldPosition = worldPos;
        GridX         = gridX;
        GridY         = gridY;
        GCost         = int.MaxValue;
    }

    public int FCost => GCost + HCost;

    /// <summary>Reset trạng thái tìm đường để tái sử dụng node giữa các lần FindPath.</summary>
    public void ResetPathData()
    {
        GCost      = int.MaxValue;
        HCost      = 0;
        Parent     = null;
        HeapIndex  = -1;
    }
}
