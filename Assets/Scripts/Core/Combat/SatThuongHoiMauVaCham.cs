using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class SatThuongHoiMauVaCham : MonoBehaviour
{
    [SerializeField] private int sathuong = 5;

    private ulong ownerClientId;

    public void SetOwner(ulong ownerClientId)
    {
        this.ownerClientId = ownerClientId;
    }


    private void OnTriggerEnter2D(Collider2D col)
    {
        if (col.attachedRigidbody == null ) {  return; }

        if(col.attachedRigidbody.TryGetComponent<NetworkObject>(out NetworkObject netObj))
        {
            if(ownerClientId == netObj.OwnerClientId)
            {
                return;
            }
        }


        if(col.attachedRigidbody.TryGetComponent<Mau>(out Mau mau))
        {
            mau.NhanSatThuong(sathuong);
        }
    }
}
