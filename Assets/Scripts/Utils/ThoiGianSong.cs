using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ThoiGianSong : MonoBehaviour
{
    [SerializeField] private float thoigiansong = 1f;

    private void Start()
    {
        Destroy(gameObject, thoigiansong);
    }

}
