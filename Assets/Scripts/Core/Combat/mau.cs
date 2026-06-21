using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Mau : NetworkBehaviour
{
    [field: SerializeField] public int MauToiDa { get; private set; } = 100;

    public NetworkVariable<int> MauHienTai = new NetworkVariable<int>();

    private bool daChet;
    private TankPlayer lastAttacker;
    private float lastAttackerTime;
    private const float KillCreditWindowSeconds = 8f;

    public Action<Mau> KhiChet;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        daChet           = false;
        lastAttacker     = null;
        lastAttackerTime = 0f;

        if (!IsServer || !IsSpawned) { return; }

        MauHienTai.Value = MauToiDa;
    }

    // ─────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Đặt máu tối đa và reset máu hiện tại về đầy.
    /// BotBrain.GanConfig() gọi method này ngay sau khi bot spawn (Server only).
    /// </summary>
    public void DatMauToiDa(int max)
    {
        if (max <= 0)
        {
            Debug.LogWarning("[Mau] DatMauToiDa: max phải > 0, bỏ qua.");
            return;
        }

        MauToiDa         = max;
        MauHienTai.Value = MauToiDa;
        Debug.Log($"[Mau] DatMauToiDa → {MauToiDa}");
    }
    // ─────────────────────────────────────────────────────────────────────

    public void NhanSatThuong(int giaTriSatThuong)
    {
        if (!IsServer) { return; }
        ThayDoiMau(-giaTriSatThuong);
    }

    public void GhiNhanSatThuongTu(TankPlayer attacker)
    {
        if (!IsServer || attacker == null) { return; }
        lastAttacker     = attacker;
        lastAttackerTime = Time.time;
    }

    public bool TryLayKeGiet(out TankPlayer killer)
    {
        killer = null;
        if (lastAttacker == null) { return false; }
        if (Time.time - lastAttackerTime > KillCreditWindowSeconds) { return false; }

        killer = lastAttacker;
        return killer != null;
    }

    public void HoiMau(int giaTriHoi)
    {
        ThayDoiMau(giaTriHoi);
    }

    private void ThayDoiMau(int value)
    {
        if (daChet) { return; }

        int MauMoi = MauHienTai.Value + value;
        MauHienTai.Value = Mathf.Clamp(MauMoi, 0, MauToiDa);

        if (MauHienTai.Value == 0)
        {
            KhiChet?.Invoke(this);
            daChet = true;
        }
    }
}
