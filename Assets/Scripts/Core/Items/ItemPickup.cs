using Unity.Netcode;
using UnityEngine;

public enum ItemType
{
    None = 0,
    BuffCoin = 1,
    Trap = 2,
    DoubleBarrel = 3
}

public class ItemPickup : NetworkBehaviour
{
    [SerializeField] private ItemType itemType;
    public SpriteRenderer spriteRenderer;

    // Các thông số tác dụng (tuỳ item)
    [SerializeField] private int buffCoinAmount = 50;
    [SerializeField] private int trapDamageAmount = 30;
    [SerializeField] private int trapCoinPenalty = 20;

    public ItemType Type => itemType;
    public int BuffCoinAmount => buffCoinAmount;
    public int TrapDamageAmount => trapDamageAmount;
    public int TrapCoinPenalty => trapCoinPenalty;

    public System.Action<ItemPickup> OnCollected;

    public void Collect()
    {
        if (!IsServer) { return; }
        OnCollected?.Invoke(this);
        // Sau khi nhặt thì xoá khỏi mạng
        NetworkObject.Despawn();
    }
}
