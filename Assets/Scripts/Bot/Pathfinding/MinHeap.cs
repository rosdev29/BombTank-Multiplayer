using System;
using System.Collections.Generic;

/// <summary>
/// Generic min-heap (priority queue) dùng cho openSet của A*.
/// Push/Pop/UpdatePriority đều O(log n) thay vì O(n) của List.
/// TItem phải implement IHeapItem&lt;TItem&gt; để lưu heap index.
/// </summary>
public class MinHeap<TItem> where TItem : IHeapItem<TItem>
{
    private TItem[] _items;
    private int _count;

    public int Count => _count;

    public MinHeap(int maxSize)
    {
        _items = new TItem[maxSize];
    }

    public void Push(TItem item)
    {
        item.HeapIndex = _count;
        _items[_count] = item;
        _count++;
        BubbleUp(item);
    }

    public TItem Pop()
    {
        TItem first = _items[0];
        first.HeapIndex = -1; // Đánh dấu không còn trong heap
        _count--;
        if (_count > 0)
        {
            _items[0] = _items[_count];
            _items[0].HeapIndex = 0;
            BubbleDown(_items[0]);
        }
        return first;
    }

    /// <summary>
    /// Gọi sau khi FCost của item giảm (cần đẩy lên trên trong heap).
    /// </summary>
    public void UpdateItem(TItem item)
    {
        BubbleUp(item);
    }

    public bool Contains(TItem item)
    {
        if (item.HeapIndex < 0 || item.HeapIndex >= _count) return false;
        return Equals(_items[item.HeapIndex], item);
    }

    public void Clear()
    {
        _count = 0;
    }

    // ── Internal ─────────────────────────────────────────────────────────────

    private void BubbleUp(TItem item)
    {
        int idx = item.HeapIndex;
        while (idx > 0)
        {
            int parentIdx = (idx - 1) / 2;
            TItem parent = _items[parentIdx];
            if (item.CompareTo(parent) < 0)
            {
                Swap(item, parent);
                idx = item.HeapIndex;
            }
            else break;
        }
    }

    private void BubbleDown(TItem item)
    {
        int idx = item.HeapIndex;
        while (true)
        {
            int left  = idx * 2 + 1;
            int right = idx * 2 + 2;
            int swap  = idx;

            if (left  < _count && _items[left ].CompareTo(_items[swap]) < 0) swap = left;
            if (right < _count && _items[right].CompareTo(_items[swap]) < 0) swap = right;

            if (swap == idx) break;
            Swap(item, _items[swap]);
            idx = item.HeapIndex;
        }
    }

    private void Swap(TItem a, TItem b)
    {
        _items[a.HeapIndex] = b;
        _items[b.HeapIndex] = a;
        int tmp = a.HeapIndex;
        a.HeapIndex = b.HeapIndex;
        b.HeapIndex = tmp;
    }
}

/// <summary>Item phải implement interface này để MinHeap lưu index.</summary>
public interface IHeapItem<T> : IComparable<T>
{
    int HeapIndex { get; set; }
}
