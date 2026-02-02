using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Mau : NetworkBehaviour
{
    [field: SerializeField] public int MauToiDa {  get; private set; } = 100;

    public NetworkVariable<int> MauHienTai = new NetworkVariable<int>();

    private bool daChet;

    public Action<Mau> KhiChet;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) { return; }

        MauHienTai.Value = MauToiDa;
    }

    public void NhanSatThuong(int giaTriSatThuong)
    {
        ThayDoiMau(-giaTriSatThuong);
    }

    public void HoiMau(int giaTriHoi)
    {
        ThayDoiMau(giaTriHoi);
    }

    private void ThayDoiMau(int value)
    {
        if (daChet) { return;  }

        int MauMoi = MauHienTai.Value + value;
        MauHienTai.Value = Mathf.Clamp(MauMoi, 0, MauToiDa);

        if(MauHienTai.Value == 0)
        {
            KhiChet?.Invoke(this);
            daChet = true;
        }
    }
}
