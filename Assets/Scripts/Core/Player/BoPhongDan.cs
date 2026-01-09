using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class BoPhongDan : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private InputReader inputReader;
    [SerializeField] private Transform DiemSpawnDan;
    [SerializeField] private GameObject ServerDanPrefab;
    [SerializeField] private GameObject ClientDanPrefab;

    [Header("Settings")]
    [SerializeField] private float TocDoDan;

    private void Update()
    {
        
    }
}
