using UnityEngine;
using Unity.Netcode;

public class CoinUIDisplay : MonoBehaviour
{
    private static CoinUIDisplay instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (instance != null) { return; }
        GameObject go = new GameObject("CoinUIDisplay");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<CoinUIDisplay>();
    }

    private Texture2D bgTexture;

    private void CreateBackgroundTexture()
    {
        bgTexture = new Texture2D(64, 64);
        Color bgColor = new Color(0.18f, 0.12f, 0.08f, 0.85f); // Nâu tối (rỉ sét)
        Color borderColor = new Color(0.7f, 0.35f, 0.1f, 1f); // Cam đất
        Color highlightColor = new Color(0.9f, 0.5f, 0.15f, 1f); // Viền sáng
        Color shadowColor = new Color(0.05f, 0.03f, 0.02f, 1f); // Viền đen

        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                // Viền ngoài cùng màu đen
                if (x < 2 || x > 61 || y < 2 || y > 61)
                {
                    bgTexture.SetPixel(x, y, shadowColor);
                }
                // Viền kim loại màu cam đất
                else if (x < 6 || x > 57 || y < 6 || y > 57)
                {
                    bgTexture.SetPixel(x, y, borderColor);
                }
                // Highlight mỏng bên trong
                else if (x == 6 || x == 57 || y == 6 || y == 57)
                {
                    bgTexture.SetPixel(x, y, highlightColor);
                }
                // Nền bên trong
                else
                {
                    // Tạo chút noise/grunge mờ để nhìn cũ kỹ
                    float noise = Random.Range(0.85f, 1.15f);
                    bgTexture.SetPixel(x, y, bgColor * noise);
                }
            }
        }
        bgTexture.Apply();
    }

    private void OnGUI()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient || !NetworkManager.Singleton.IsConnectedClient) return;

        var localClient = NetworkManager.Singleton.LocalClient;
        if (localClient == null || localClient.PlayerObject == null) return;

        TankPlayer localPlayer = localClient.PlayerObject.GetComponent<TankPlayer>();
        if (localPlayer == null || localPlayer.Wallet == null) return;

        BoPhongDan combat = localPlayer.GetComponent<BoPhongDan>();
        int currentCoins = localPlayer.Wallet.TotalCoins.Value;
        
        int cost = combat != null ? combat.GetShootingCost() : 5;
        bool canShoot = currentCoins >= cost;

        // Ensure we scale properly for different resolutions
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Screen.width / 1920f, Screen.height / 1080f, 1f));

        if (bgTexture == null)
        {
            CreateBackgroundTexture();
        }

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 48,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        
        string text = canShoot ? $"🪙 Coins: {currentCoins}" : $"🪙 Coins: {currentCoins}";
        Vector2 textSize = style.CalcSize(new GUIContent(text));
        
        // Căn chỉnh kích thước hộp
        float boxWidth = textSize.x + 80f;
        float boxHeight = 80f;
        Rect boxRect = new Rect(30f, 1080f - 110f, boxWidth, boxHeight);
        
        // Căn chữ vào giữa
        Rect rect = new Rect(boxRect.x, boxRect.y, boxWidth, boxHeight);
        Rect shadowRect = new Rect(rect.x + 3f, rect.y + 3f, rect.width, rect.height);

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.background = bgTexture;
        boxStyle.border = new RectOffset(8, 8, 8, 8); // Cắt góc để scale 9-slicing
        
        // Vẽ khung nền
        GUI.Box(boxRect, GUIContent.none, boxStyle);
        
        GUIStyle shadowStyle = new GUIStyle(style);
        shadowStyle.normal.textColor = new Color(0, 0, 0, 0.8f);
        
        GUI.Label(shadowRect, text, shadowStyle);
        
        if (canShoot)
        {
            style.normal.textColor = new Color(1f, 0.85f, 0f, 1f); // Gold
        }
        else
        {
            style.normal.textColor = new Color(1f, 0.3f, 0.3f, 1f); // Red
        }

        GUI.Label(rect, text, style);
    }
}
