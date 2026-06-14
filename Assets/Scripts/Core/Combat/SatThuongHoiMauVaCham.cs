using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class SatThuongHoiMauVaCham : MonoBehaviour
{
    [SerializeField] private int sathuong = 5;
    [SerializeField] private Projectile projectile;

    private ulong ownerClientId;
    private int ownerTeamIndex = -1;
    private TankPlayer ownerTank;

    private void Awake()
    {
        if (projectile == null)
        {
            projectile = GetComponent<Projectile>();
        }
    }

    public void SetOwner(ulong ownerClientId)
    {
        this.ownerClientId = ownerClientId;
    }

    public void SetOwner(ulong ownerClientId, int ownerTeamIndex)
    {
        this.ownerClientId = ownerClientId;
        this.ownerTeamIndex = ownerTeamIndex;
    }

    public void SetOwner(TankPlayer attacker, int teamIndex)
    {
        ownerTank = attacker;
        ownerClientId = attacker != null ? attacker.OwnerClientId : 0;
        ownerTeamIndex = teamIndex;
    }


    private void OnTriggerEnter2D(Collider2D col)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) { return; }
        if (col.attachedRigidbody == null) { return; }

        TankPlayer targetPlayer = col.attachedRigidbody.GetComponentInParent<TankPlayer>();
        if (targetPlayer != null)
        {
            if (ownerTank != null && targetPlayer == ownerTank)
            {
                return;
            }
        }

        int teamIndex = ownerTeamIndex != -1
            ? ownerTeamIndex
            : (projectile != null ? projectile.TeamIndex : -1);

        if (teamIndex != -1 && targetPlayer != null)
        {
            if (targetPlayer.TeamIndex.Value == teamIndex)
            {
                return;
            }
        }

        Mau mau = col.attachedRigidbody.GetComponentInParent<Mau>();
        if (mau == null) { return; }

        if (ownerTank != null)
        {
            mau.GhiNhanSatThuongTu(ownerTank);
        }

        mau.NhanSatThuong(sathuong);
    }
}
