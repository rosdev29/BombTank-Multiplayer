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

    public float DoubleBarrelDuration => doubleBarrelDuration;
    public float BuffCoinDuration => buffCoinDuration;
    public float TrapDuration => trapDuration;

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
        if (!IsOwner || (player != null && player.IsBot.Value)) { return; }
        
        if (inputReader != null)
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

    private float buffCoinTimer = 0f;
    private int trapTicksRemaining = 0;

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (!IsServer) { return; }

        if (!col.TryGetComponent<ItemPickup>(out ItemPickup item)) { return; }

        // Bẫy - chỉ trừ máu trong 5s (không trừ coin)
        if (item.Type == ItemType.Trap)
        {
            int ticksToAdd = Mathf.RoundToInt(trapDuration);
            if (ticksToAdd <= 0) ticksToAdd = 1;
            trapTicksRemaining += ticksToAdd;
            
            if (trapCoroutine == null)
            {
                trapCoroutine = StartCoroutine(TrapRoutine(item.TrapDamageAmount));
            }
            item.Collect();
            PlaySquishEffectClientRpc();
            TriggerPickedUpEventClientRpc(ItemType.Trap);
        }
        // Đồng Vàng (Buff Coin)
        else if (item.Type == ItemType.BuffCoin)
        {
            if (buffCoinTimer > 0)
            {
                buffCoinTimer += buffCoinDuration;
            }
            else
            {
                buffCoinTimer = buffCoinDuration;
                IsCoinBuffActive.Value = true;
            }
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

    private void Update()
    {
        if (IsServer && buffCoinTimer > 0)
        {
            buffCoinTimer -= Time.deltaTime;
            if (buffCoinTimer <= 0)
            {
                buffCoinTimer = 0;
                IsCoinBuffActive.Value = false;
            }
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
                colorDisplay.PlayEffect(Color.yellow, 1f, true);
                break;
            case ItemType.Trap:
                colorDisplay.PlayEffect(Color.red, 1f, true);
                break;
            case ItemType.DoubleBarrel:
                colorDisplay.PlayEffect(Color.cyan, doubleBarrelDuration, false);
                break;
        }
    }

    private IEnumerator TrapRoutine(int totalDamage)
    {
        IsTrapActive.Value = true;
        
        int dmgPerTick = totalDamage / Mathf.RoundToInt(trapDuration);

        while (trapTicksRemaining > 0)
        {
            health.NhanSatThuong(dmgPerTick);
            PlayEffectClientRpc(ItemType.Trap);
            yield return new WaitForSeconds(1f);
            trapTicksRemaining--;
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
