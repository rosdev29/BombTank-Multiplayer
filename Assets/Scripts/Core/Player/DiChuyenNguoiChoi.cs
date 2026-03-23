using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class DiChuyenNguoiChoi : NetworkBehaviour
{
    [Header("Tham chieu")]
    [SerializeField] private InputReader docInput;
    [SerializeField] private Transform bodyTransform;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private ParticleSystem dustTrail;

    [Header("Cai dat")]
    [SerializeField] private float tocDoDiChuyen = 5f;
    [SerializeField] private float tocDoXoay = 30f;
    [SerializeField] private float tocDoEmission = 10f;

    private ParticleSystem.EmissionModule emissionModule;
    private Vector2 inputDiChuyenTruoc;
    private Vector3 viTriTruoc;

    private const float NguongDungHat = 0.005f;

    private void Awake()
    {
        if (dustTrail != null)
        {
            emissionModule = dustTrail.emission;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) { return; }

        docInput.MoveEvent += XuLyDiChuyen;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) { return; }

        docInput.MoveEvent -= XuLyDiChuyen;
    }

    private void Update()
    {
        if (!IsOwner) { return; }

        float gocXoayZ = inputDiChuyenTruoc.x * -tocDoXoay * Time.deltaTime;
        bodyTransform.Rotate(0f, 0f, gocXoayZ);
    }

    private void FixedUpdate()
    {
        if (dustTrail != null)
        {
            if ((transform.position - viTriTruoc).sqrMagnitude > NguongDungHat)
            {
                emissionModule.rateOverTime = tocDoEmission;
            }
            else
            {
                emissionModule.rateOverTime = 0f;
            }
        }
        viTriTruoc = transform.position;

        if (!IsOwner) { return; }

        rb.velocity = (Vector2)bodyTransform.up * inputDiChuyenTruoc.y * tocDoDiChuyen;
    }

    private void XuLyDiChuyen(Vector2 inputDiChuyen)
    {
        inputDiChuyenTruoc = inputDiChuyen;
    }
}