using Unity.Netcode;
using UnityEngine;

public class TankDeathHandler : NetworkBehaviour
{
    [SerializeField] private Mau mau;

    private void Awake()
    {
        if (mau == null)
        {
            mau = GetComponent<Mau>();
        }
    }

    public override void OnNetworkSpawn()
    {
        if (mau == null)
        {
            mau = GetComponent<Mau>();
        }

        if (mau != null)
        {
            mau.KhiChet += HandleDeath;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (mau != null)
        {
            mau.KhiChet -= HandleDeath;
        }
    }

    private void HandleDeath(Mau deadMau)
    {
        if (!IsServer) { return; }

        NetworkObject networkObject = GetComponent<NetworkObject>();

        if (networkObject != null && networkObject.IsSpawned)
        {
            networkObject.Despawn();
        }
        else
        {
            Destroy(gameObject);
        }
    }
}