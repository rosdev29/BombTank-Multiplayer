using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Mau : NetworkBehaviour
{
    [field: SerializeField] public int MauToiDa { get; private set; } = 100;

    public NetworkVariable<int> MauHienTai = new NetworkVariable<int>();
    public NetworkVariable<int> MauToiDaNet = new NetworkVariable<int>(100);

    private bool daChet;
    private TankPlayer lastAttacker;
    private float lastAttackerTime;
    private const float KillCreditWindowSeconds = 8f;

    public Action<Mau> KhiChet;

    [SerializeField] private AudioClip amThanhTrungDan;
    [SerializeField] private float amLuongTrungDan = 0.5f;
    [SerializeField] private AudioClip amThanhNo;
    [SerializeField] private float amLuongNo = 0.8f;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        daChet = false;
        lastAttacker = null;
        lastAttackerTime = 0f;

        if (!IsServer || !IsSpawned) { return; }

        MauToiDaNet.Value = MauToiDa;
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

        MauToiDa = max;
        MauToiDaNet.Value = max;
        MauHienTai.Value = max;
        daChet = false;
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
        lastAttacker = attacker;
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
        if (!IsServer) { return; }
        ThayDoiMau(giaTriHoi);
    }

    private void ThayDoiMau(int value)
    {
        if (daChet) { return; }

        int maxMau = IsSpawned ? MauToiDaNet.Value : MauToiDa;
        int mauCu = MauHienTai.Value;
        int mauMoi = mauCu + value;

        MauHienTai.Value = Mathf.Clamp(mauMoi, 0, maxMau);

        // Bị mất máu => phát tiếng trúng đạn
        if (value < 0 &&
            MauHienTai.Value > 0 &&
            amThanhTrungDan != null)
        {
            AudioSource.PlayClipAtPoint(
                amThanhTrungDan,
                transform.position,
                amLuongTrungDan);
        }

        if (MauHienTai.Value == 0)
        {
            daChet = true;

            // Phát tiếng nổ khi chết trước khi đối tượng bị biến mất
            if (amThanhNo != null)
            {
                AudioSource.PlayClipAtPoint(
                    amThanhNo,
                    transform.position,
                    amLuongNo);
            }

            KhiChet?.Invoke(this);

            if (IsServer)
            {
                XuLyChetTrenServer();
            }
        }
    }

    private void XuLyChetTrenServer()
    {
        TankPlayer tank = GetComponent<TankPlayer>();
        if (tank == null || !tank.IsCurrentlyBot()) { return; }

        BotBrain brain = GetComponent<BotBrain>();
        if (brain != null) { brain.enabled = false; }

        CoinWallet wallet = GetComponent<CoinWallet>();
        if (wallet != null)
        {
            wallet.ProcessDeathCoinDrop();
        }

        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn();
        }
    }
}