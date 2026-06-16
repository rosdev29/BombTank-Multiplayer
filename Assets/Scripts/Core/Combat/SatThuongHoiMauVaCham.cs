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
        if (col.attachedRigidbody == null ) {  return; }

        if (col.attachedRigidbody.TryGetComponent<TankPlayer>(out TankPlayer targetTank))
        {
            // Nếu có ownerTank, so sánh object để tránh tự sát thương (thay vì dùng OwnerClientId vì các bot đều là 0)
            if (ownerTank != null && ownerTank == targetTank)
            {
                return;
            }
            // Fallback cho player thường nếu không set ownerTank
            else if (ownerTank == null && ownerClientId == targetTank.OwnerClientId)
            {
                return;
            }
        }
        else if (col.attachedRigidbody.TryGetComponent<NetworkObject>(out NetworkObject netObj))
        {
            if (ownerClientId == netObj.OwnerClientId)
            {
                return;
            }
        }

        int teamIndex = ownerTeamIndex != -1
            ? ownerTeamIndex
            : (projectile != null ? projectile.TeamIndex : -1);

        if (teamIndex != -1)
        {
            if (col.attachedRigidbody.TryGetComponent<TankPlayer>(out TankPlayer player))
            {
                if (player.TeamIndex.Value == teamIndex)
                {
                    return;
                }
            }
        }

        if (col.attachedRigidbody.TryGetComponent<Mau>(out Mau mau))
        {
            if (ownerTank != null)
            {
                mau.GhiNhanSatThuongTu(ownerTank);
            }

            mau.NhanSatThuong(sathuong);
        }
    }
}
