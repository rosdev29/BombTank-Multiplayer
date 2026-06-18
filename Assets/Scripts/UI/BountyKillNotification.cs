using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Hiện thông báo nhanh "Bounty Kill! +XX coin từ [tên]" cho kẻ giết crown.
///
/// Setup trong Inspector:
///   1. Attach script này vào GameObject "BountyKillNotification" (con của BountyCanvas).
///   2. Gán notificationText → TMP_Text cùng GameObject hoặc child.
///   3. GameObject này tự tắt mặc định — script bật/tắt tự động.
///   Xóa BountyNotificationBootstrap nếu còn trong project.
/// </summary>
public class BountyKillNotification : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
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
        // Tự tìm TMP_Text nếu chưa gán trong Inspector
        if (notificationText == null)
            notificationText = GetComponentInChildren<TMP_Text>(true);

        _instance = this;
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_instance == this) { _instance = null; }
    }

    // ─── Public API (gọi từ BountySystem ClientRpc) ──────────────────────────
    public static void Show(string victimName, int reward)
    {
        if (_instance == null)
        {
            Debug.LogWarning("[BountyKillNotification] Instance chưa tồn tại trong scene.");
            return;
        }

        _instance.ShowInternal(victimName, reward);
    }

    // ─── Dùng khi tạo bằng code (Bootstrap) ─────────────────────────────────
    public void Init(TMP_Text text)
    {
        notificationText = text;
    }

    // ─────────────────────────────────────────────────────────────────────────
    private void ShowInternal(string victimName, int reward)
    {
        if (notificationText != null)
        {
            notificationText.color = textColor;
            notificationText.text = $"<sprite name=\"BountyCrown\"> Bounty Kill: {victimName}\n+{reward} coin";

        }
        else
        {
            Debug.LogWarning("[BountyKillNotification] notificationText chưa được gán.");
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