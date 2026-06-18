using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Hiện thông báo nhanh "+XX coin (Bounty Kill!)" cho kẻ giết crown.
///
/// Setup:
///   1. Tạo Canvas/GameObject "BountyKillNotification" trong Game scene.
///   2. Attach script này.
///   3. Gán notificationText (TMP_Text).
///   4. Đảm bảo GameObject này tự tắt mặc định — script sẽ bật/tắt tự động.
/// </summary>
public class BountyKillNotification : MonoBehaviour
{
    // ─── Singleton ─────────────────────────────────────────────────────────
    private static BountyKillNotification _instance;

    [Header("References")]
    [SerializeField] private TMP_Text notificationText;

    [Header("Settings")]
    [SerializeField] private float displayDuration = 2.5f;
    [SerializeField] private Color textColor = new Color(1f, 0.84f, 0f); // vàng

    private Coroutine _hideCoroutine;

    // ─────────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        _instance = this;
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_instance == this) { _instance = null; }
    }

    // ─── Public API (gọi từ BountySystem ClientRpc) ───────────────────────
    public static void Show(string victimName, int reward)
    {
        if (_instance == null)
        {
            Debug.LogWarning("[BountyKillNotification] Instance chưa tồn tại trong scene.");
            return;
        }

        _instance.ShowInternal(victimName, reward);
    }

    private void ShowInternal(string victimName, int reward)
    {
        if (notificationText != null)
        {
            notificationText.color = textColor;
            notificationText.text  = $"Bounty Kill!\n+{reward} coin từ {victimName}";
        }

        gameObject.SetActive(true);

        if (_hideCoroutine != null) { StopCoroutine(_hideCoroutine); }
        _hideCoroutine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(displayDuration);
        gameObject.SetActive(false);
        _hideCoroutine = null;
    }
}
