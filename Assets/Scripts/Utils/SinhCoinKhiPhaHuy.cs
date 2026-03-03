using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SinhCoinKhiPhaHuy : MonoBehaviour
{
    [SerializeField] private GameObject prefab;

    private void OnDestroy()
    {
        Instantiate(prefab, transform.position, Quaternion.identity);
    }
}
