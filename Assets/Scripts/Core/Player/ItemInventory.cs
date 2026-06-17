using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ItemInventory : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private TankPlayer player;
    [SerializeField] private Mau health;
    [SerializeField] private CoinWallet wallet;
    [SerializeField] private BoPhongDan combat;
    [SerializeField] private InputReader inputReader;
    [SerializeField] private PlayerColourDisplay colorDisplay;

    [Header("Item Settings")]
    [SerializeField] private float doubleBarrelDuration = 10f;
    [SerializeField] private float buffCoinDuration = 10f; // Tăng lên 10s
    [SerializeField] private float trapDuration = 5f;

    public NetworkVariable<ItemType> CurrentItem = new NetworkVariable<ItemType>(ItemType.None);
    public NetworkVariable<bool> IsCoinBuffActive = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> IsTrapActive = new NetworkVariable<bool>(false);
    
    private Coroutine buffCoinCoroutine;
    private Coroutine trapCoroutine;

    // Events dành cho Phúc gắn SFX
    public static event Action<ItemType> OnItemPickedUp;
    public static event Action<ItemType> OnItemUsed;


    public override void OnNetworkSpawn()
    {
        if (!IsOwner) { return; }
        
        if (player != null && !player.IsBot.Value && inputReader != null)
        {
            inputReader.UseItemEvent += HandleUseItemInput;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner || (player != null && player.IsBot.Value)) { return; }
        
        if (inputReader != null)
        {
            inputReader.UseItemEvent -= HandleUseItemInput;
        }
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (!IsServer) { return; }

        if (!col.TryGetComponent<ItemPickup>(out ItemPickup item)) { return; }

        // AI Bot đã có tỉ lệ nhận diện và quyết định nhặt ở TankAgentUltra.cs
        // Khi bot đã chạm vào item thì luôn nhặt thành công, giống người chơi thật.

        // Bẫy - Trừ máu, trừ coin từ từ trong 5s
        if (item.Type == ItemType.Trap)
        {
            if (trapCoroutine != null) StopCoroutine(trapCoroutine);
            trapCoroutine = StartCoroutine(TrapRoutine(item.TrapDamageAmount, item.TrapCoinPenalty));
            item.Collect();
            PlaySquishEffectClientRpc();
            TriggerPickedUpEventClientRpc(ItemType.Trap);
        }
        // Đồng Vàng (Buff Coin)
        else if (item.Type == ItemType.BuffCoin)
        {
            if (buffCoinCoroutine != null) StopCoroutine(buffCoinCoroutine);
            buffCoinCoroutine = StartCoroutine(BuffCoinRoutine());
            item.Collect();
            PlayEffectClientRpc(ItemType.BuffCoin);
            PlaySquishEffectClientRpc();
            TriggerPickedUpEventClientRpc(ItemType.BuffCoin);
            TriggerUsedEventClientRpc(ItemType.BuffCoin); 
        }
        // Súng 2 nòng
        else if (item.Type == ItemType.DoubleBarrel)
        {
            combat.ActivateDoubleBarrel(doubleBarrelDuration);
            item.Collect();
            PlayEffectClientRpc(ItemType.DoubleBarrel);
            PlaySquishEffectClientRpc();
            TriggerPickedUpEventClientRpc(ItemType.DoubleBarrel);
            TriggerUsedEventClientRpc(ItemType.DoubleBarrel);
        }
    }

    [ClientRpc]
    private void TriggerPickedUpEventClientRpc(ItemType type)
    {
        if (IsOwner && !player.IsCurrentlyBot())
        {
            OnItemPickedUp?.Invoke(type);
        }
    }

    private void HandleUseItemInput()
    {
    }

    public void UseItem()
    {
    }

    [ServerRpc]
    private void UseItemServerRpc()
    {
    }

    private IEnumerator BuffCoinRoutine()
    {
        IsCoinBuffActive.Value = true;
        yield return new WaitForSeconds(buffCoinDuration);
        IsCoinBuffActive.Value = false;
        buffCoinCoroutine = null;
    }

    [ClientRpc]
    private void TriggerUsedEventClientRpc(ItemType type)
    {
        if (IsOwner && !player.IsCurrentlyBot())
        {
            OnItemUsed?.Invoke(type);
        }
    }

    [ClientRpc]
    private void PlayEffectClientRpc(ItemType type)
    {
        if (colorDisplay == null) return;
        switch (type)
        {
            case ItemType.BuffCoin:
                colorDisplay.PlayEffect(Color.yellow, 2f, true); // Hiệu ứng sáng vàng 2s
                break;
            case ItemType.Trap:
                colorDisplay.PlayEffect(Color.red, 2f, true); // Hiệu ứng sáng đỏ 2s
                break;
            case ItemType.DoubleBarrel:
                colorDisplay.PlayEffect(Color.cyan, 2f, false); // Hiệu ứng sáng xanh 2s
                break;
        }
    }

    private IEnumerator TrapRoutine(int totalDamage, int totalCoinPenalty)
    {
        IsTrapActive.Value = true;
        int ticks = Mathf.RoundToInt(trapDuration);
        if (ticks <= 0) ticks = 1;
        
        int dmgPerTick = totalDamage / ticks;
        int coinPerTick = totalCoinPenalty / ticks;

        for (int i = 0; i < ticks; i++)
        {
            health.NhanSatThuong(dmgPerTick);
            wallet.SpendCoins(coinPerTick);
            PlayEffectClientRpc(ItemType.Trap); // Chớp đỏ mỗi giây
            yield return new WaitForSeconds(1f);
        }
        
        IsTrapActive.Value = false;
        trapCoroutine = null;
    }

    [ClientRpc]
    private void PlaySquishEffectClientRpc()
    {
        if (player == null) return;
        
        List<Transform> partsToSquish = new List<Transform>();
        foreach (Transform child in player.GetComponentsInChildren<Transform>())
        {
            if (child.name == "TankBody" || child.name == "TurretPivot" || child.name == "LeftTracks" || child.name == "RightTracks")
            {
                partsToSquish.Add(child);
            }
        }

        if (partsToSquish.Count > 0)
        {
            StartCoroutine(SquishRoutine(partsToSquish));
        }
    }

    private IEnumerator SquishRoutine(List<Transform> targets)
    {
        if (targets.Count == 0) yield break;
        
        // Lưu lại scale gốc của từng bộ phận
        Dictionary<Transform, Vector3> originalScales = new Dictionary<Transform, Vector3>();
        foreach (var t in targets)
        {
            if (t != null) originalScales[t] = t.localScale;
        }
        
        // Phình to ngang, dẹp dọc
        float time = 0;
        while (time < 0.1f)
        {
            time += Time.deltaTime;
            float ratio = time / 0.1f;
            foreach (var t in targets)
            {
                if (t != null) t.localScale = Vector3.Lerp(originalScales[t], new Vector3(originalScales[t].x * 1.3f, originalScales[t].y * 0.7f, originalScales[t].z), ratio);
            }
            yield return null;
        }
        
        // Co ngang, giãn dọc
        time = 0;
        while (time < 0.1f)
        {
            time += Time.deltaTime;
            float ratio = time / 0.1f;
            foreach (var t in targets)
            {
                if (t != null) t.localScale = Vector3.Lerp(new Vector3(originalScales[t].x * 1.3f, originalScales[t].y * 0.7f, originalScales[t].z), new Vector3(originalScales[t].x * 0.8f, originalScales[t].y * 1.2f, originalScales[t].z), ratio);
            }
            yield return null;
        }

        // Trở về bình thường
        time = 0;
        while (time < 0.1f)
        {
            time += Time.deltaTime;
            float ratio = time / 0.1f;
            foreach (var t in targets)
            {
                if (t != null) t.localScale = Vector3.Lerp(new Vector3(originalScales[t].x * 0.8f, originalScales[t].y * 1.2f, originalScales[t].z), originalScales[t], ratio);
            }
            yield return null;
        }
        
        foreach (var t in targets)
        {
            if (t != null) t.localScale = originalScales[t];
        }
    }
}
