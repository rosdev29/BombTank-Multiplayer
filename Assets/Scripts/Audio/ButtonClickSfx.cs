using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Phát click ngay khi nhấn (PointerDown), không chờ onClick xử lý xong.</summary>
[DisallowMultipleComponent]
public class ButtonClickSfx : MonoBehaviour, IPointerDownHandler
{
    public void OnPointerDown(PointerEventData eventData)
    {
        if (!isActiveAndEnabled) { return; }
        AudioManager.GetInstance()?.PlayClick();
    }
}
