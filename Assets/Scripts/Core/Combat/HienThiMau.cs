using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class HienThiMau : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Mau mau;
    [SerializeField] private Image ThanhMauImage;


    public override void OnNetworkSpawn()
    {
        if (!IsClient) { return; }

        mau.MauHienTai.OnValueChanged += XuLyKhiMauThayDoi;
        XuLyKhiMauThayDoi(0, mau.MauHienTai.Value);
    }

    public override void OnNetworkDespawn()
    {
        if(!IsClient) { return; }
        mau.MauHienTai.OnValueChanged -= XuLyKhiMauThayDoi;

    }

    private void XuLyKhiMauThayDoi(int mauOld, int mauMoi)
    {
        ThanhMauImage.fillAmount = (float)mauMoi / mau.MauToiDa;
    }
}
