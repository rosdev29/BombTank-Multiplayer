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
    [SerializeField] private ParticleSystem[] vetBanhTrails;

    [Header("Cai dat")]
    [SerializeField] private float tocDoDiChuyen = 5f;
    [SerializeField] private float tocDoXoay = 30f;
    [SerializeField] private float tocDoEmission = 10f;
    [SerializeField] private float tocDoEmissionVetBanh = 26f;
    [SerializeField] private float thoiGianTonTaiVetBanh = 2.5f;
    [SerializeField] private float nguongDiChuyenDePhatBui = 0.0004f;

    private ParticleSystem.EmissionModule emissionModule;
    private Vector2 inputDiChuyenTruoc;
    private Vector3 viTriTruoc;
    private bool daKhoiTaoViTri;
    private readonly NetworkVariable<bool> dangDiChuyen =
        new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private void Awake()
    {
        if (dustTrail != null)
        {
            emissionModule = dustTrail.emission;
        }

        if (vetBanhTrails == null || vetBanhTrails.Length == 0)
        {
            vetBanhTrails = TimVetBanhTheoTen();
        }

        CauHinhVetBanh();
    }

    public override void OnNetworkSpawn()
    {
        viTriTruoc = bodyTransform != null ? bodyTransform.position : transform.position;
        daKhoiTaoViTri = true;

        if (dustTrail != null && !dustTrail.isPlaying)
        {
            dustTrail.Play();
        }

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
        dangDiChuyen.Value = Mathf.Abs(inputDiChuyenTruoc.y) > 0.01f;
    }

    private void FixedUpdate()
    {
        bool nenPhat = CaphatBuiTheoTrangThai();
        CapNhatVetBanh(nenPhat);

        if (!IsOwner) { return; }

        rb.velocity = (Vector2)bodyTransform.up * inputDiChuyenTruoc.y * tocDoDiChuyen;
    }

    private void XuLyDiChuyen(Vector2 inputDiChuyen)
    {
        inputDiChuyenTruoc = inputDiChuyen;
    }

    private bool CaphatBuiTheoTrangThai()
    {
        Vector3 viTriHienTai = bodyTransform != null ? bodyTransform.position : transform.position;
        bool coDiChuyenTheoViTri = false;

        if (daKhoiTaoViTri)
        {
            float khoangCachBinhPhuong = (viTriHienTai - viTriTruoc).sqrMagnitude;
            coDiChuyenTheoViTri = khoangCachBinhPhuong > nguongDiChuyenDePhatBui;
        }

        viTriTruoc = viTriHienTai;
        daKhoiTaoViTri = true;

        bool nenPhatBui = dangDiChuyen.Value || coDiChuyenTheoViTri;
        if (dustTrail != null)
        {
            emissionModule.rateOverTime = nenPhatBui ? tocDoEmission : 0f;
        }

        if (dustTrail != null && nenPhatBui && !dustTrail.isPlaying)
        {
            dustTrail.Play();
        }

        return nenPhatBui;
    }

    private void CapNhatVetBanh(bool dangChay)
    {
        if (vetBanhTrails == null || vetBanhTrails.Length == 0) { return; }

        for (int i = 0; i < vetBanhTrails.Length; i++)
        {
            ParticleSystem vetBanh = vetBanhTrails[i];
            if (vetBanh == null) { continue; }

            var emission = vetBanh.emission;
            emission.rateOverTime = dangChay ? tocDoEmissionVetBanh : 0f;

            if (dangChay && !vetBanh.isPlaying)
            {
                vetBanh.Play();
            }
        }
    }

    private void CauHinhVetBanh()
    {
        if (vetBanhTrails == null || vetBanhTrails.Length == 0) { return; }

        for (int i = 0; i < vetBanhTrails.Length; i++)
        {
            ParticleSystem vetBanh = vetBanhTrails[i];
            if (vetBanh == null) { continue; }

            ParticleSystem.MainModule main = vetBanh.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = thoiGianTonTaiVetBanh;
        }
    }

    private ParticleSystem[] TimVetBanhTheoTen()
    {
        ParticleSystem[] tatCaParticle = GetComponentsInChildren<ParticleSystem>(true);
        List<ParticleSystem> ketQua = new List<ParticleSystem>();
        for (int i = 0; i < tatCaParticle.Length; i++)
        {
            ParticleSystem ps = tatCaParticle[i];
            if (ps == null || ps.gameObject == null) { continue; }

            string ten = ps.gameObject.name;
            if (ten == "LeftTracks" || ten == "RightTracks")
            {
                ketQua.Add(ps);
            }
        }

        return ketQua.ToArray();
    }
}