using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using TMPro;

public class ItemUIDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI itemText;
    private Coroutine hideCoroutine;

    private void Start()
    {
        if (itemText != null) itemText.text = "";
        ItemInventory.OnItemPickedUp += HandleItemPickedUp;
    }

    private void OnDestroy()
    {
        ItemInventory.OnItemPickedUp -= HandleItemPickedUp;
    }

    private void HandleItemPickedUp(ItemType type)
    {
        // Đã tắt text theo yêu cầu
        if (itemText != null) itemText.text = "";
    }

    private IEnumerator HideTextRoutine()
    {
        yield return new WaitForSeconds(1.5f);
        if (itemText != null) itemText.text = "";
    }
}
