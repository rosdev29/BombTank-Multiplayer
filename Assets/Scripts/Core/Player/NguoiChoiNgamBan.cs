using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NguoiChoiNgamBan : NetworkBehaviour
{
    [SerializeField] private InputReader inputReader;
    [SerializeField] private Transform turretTransform;

    private void LateUpdate()
    {
        if (!IsOwner) {  return; }

        Vector2 ViTriNgamTrenManHinh = inputReader.ViTriNgam;
        Vector2 ViTriNgamTrongTheGioi = Camera.main.ScreenToWorldPoint(ViTriNgamTrenManHinh);

        turretTransform.up = new Vector2(
            ViTriNgamTrongTheGioi.x - turretTransform.position.x,
            ViTriNgamTrongTheGioi.y - turretTransform.position.y);
    }
}
