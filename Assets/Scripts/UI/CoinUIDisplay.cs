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

    private Texture2D coinsBgTexture;
    private Texture2D totalBgTexture;

    private Texture2D CreateBoxTexture(Color bgColor, Color borderColor)
    {
        Texture2D tex = new Texture2D(64, 64);
        Color highlightColor = borderColor * 1.2f;
        Color shadowColor = new Color(0.05f, 0.05f, 0.05f, 1f);

        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                if (x < 2 || x > 61 || y < 2 || y > 61)
                {
                    tex.SetPixel(x, y, shadowColor);
                }
                else if (x < 6 || x > 57 || y < 6 || y > 57)
                {
                    tex.SetPixel(x, y, borderColor);
                }
                else if (x == 6 || x == 57 || y == 6 || y == 57)
                {
                    tex.SetPixel(x, y, highlightColor);
                }
                else
                {
                    float noise = Random.Range(0.9f, 1.1f);
                    tex.SetPixel(x, y, bgColor * noise);
                }
            }
        }
        tex.Apply();
        return tex;
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
        int totalCollected = localPlayer.Wallet.LifetimeCoins.Value;
        
        int cost = combat != null ? combat.GetShootingCost() : 5;
        bool canShoot = currentCoins >= cost;

        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Screen.width / 1920f, Screen.height / 1080f, 1f));

        if (coinsBgTexture == null || totalBgTexture == null)
        {
            coinsBgTexture = CreateBoxTexture(new Color(0.2f, 0.12f, 0.05f, 0.95f), new Color(0.8f, 0.5f, 0.1f, 1f));
            totalBgTexture = CreateBoxTexture(new Color(0.05f, 0.2f, 0.05f, 0.95f), new Color(0.1f, 0.8f, 0.2f, 1f));
        }

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 34,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        
        // Kích thước cố định cho cả 2 box để chúng thẳng hàng tuyệt đối
        float boxWidth = 320f;
        float boxHeight = 65f;
        float spacing = 10f;
        float bottomMargin = 30f;
        
        // Y vị trí: Coins ở trên, Total ở dưới
        float totalY = 1080f - bottomMargin - boxHeight;
        float coinsY = totalY - boxHeight - spacing;

        Rect coinsRect = new Rect(30f, coinsY, boxWidth, boxHeight);
        Rect totalRect = new Rect(30f, totalY, boxWidth, boxHeight);

        // --- VẼ BOX COINS ---
        GUIStyle coinsBoxStyle = new GUIStyle(GUI.skin.box);
        coinsBoxStyle.normal.background = coinsBgTexture;
        coinsBoxStyle.border = new RectOffset(8, 8, 8, 8);
        GUI.Box(coinsRect, GUIContent.none, coinsBoxStyle);

        string coinsText = $"Coins: {currentCoins}";
        DrawTextWithShadow(coinsRect, coinsText, style, canShoot ? new Color(1f, 0.85f, 0f, 1f) : new Color(1f, 0.3f, 0.3f, 1f));

        // --- VẼ BOX TOTAL ---
        GUIStyle totalBoxStyle = new GUIStyle(GUI.skin.box);
        totalBoxStyle.normal.background = totalBgTexture;
        totalBoxStyle.border = new RectOffset(8, 8, 8, 8);
        GUI.Box(totalRect, GUIContent.none, totalBoxStyle);

        string totalText = $"Total: {totalCollected}";
        DrawTextWithShadow(totalRect, totalText, style, new Color(0.3f, 1f, 0.3f, 1f));
    }

    private void DrawTextWithShadow(Rect rect, string text, GUIStyle style, Color textColor)
    {
        Rect shadowRect = new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height);
        
        GUIStyle shadowStyle = new GUIStyle(style);
        shadowStyle.normal.textColor = new Color(0, 0, 0, 0.8f);
        GUI.Label(shadowRect, text, shadowStyle);
        
        style.normal.textColor = textColor;
        GUI.Label(rect, text, style);
    }
}
