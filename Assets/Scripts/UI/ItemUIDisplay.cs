using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class ItemUIDisplay : MonoBehaviour
{
    private static ItemUIDisplay instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (instance != null) { return; }
        GameObject go = new GameObject("ItemUIDisplay");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<ItemUIDisplay>();
    }

    private class ActiveItem
    {
        public ItemType Type;
        public float Timer;
        public Color ThemeColor;
        public Texture2D BgTexture;
        public Sprite IconSprite;
    }

    private List<ActiveItem> activeItems = new List<ActiveItem>();

    private Sprite buffCoinSprite;
    private Sprite doubleBarrelSprite;
    private Sprite trapSprite;

    private void TryLoadSprites()
    {
        if (buffCoinSprite != null && doubleBarrelSprite != null && trapSprite != null) return;
        
        ItemSpawner spawner = Object.FindFirstObjectByType<ItemSpawner>();
        if (spawner != null)
        {
            if (spawner.buffCoinPrefab != null)
            {
                var sr = spawner.buffCoinPrefab.spriteRenderer;
                if (sr == null) sr = spawner.buffCoinPrefab.GetComponentInChildren<SpriteRenderer>();
                if (sr != null) buffCoinSprite = sr.sprite;
            }
            
            if (spawner.doubleBarrelPrefab != null)
            {
                var sr = spawner.doubleBarrelPrefab.spriteRenderer;
                if (sr == null) sr = spawner.doubleBarrelPrefab.GetComponentInChildren<SpriteRenderer>();
                if (sr != null) doubleBarrelSprite = sr.sprite;
            }
            
            if (spawner.trapPrefab != null)
            {
                var sr = spawner.trapPrefab.spriteRenderer;
                if (sr == null) sr = spawner.trapPrefab.GetComponentInChildren<SpriteRenderer>();
                if (sr != null) trapSprite = sr.sprite;
            }
        }
    }

    private void OnEnable()
    {
        ItemInventory.OnItemPickedUp += HandleItemPickedUp;
    }

    private void OnDisable()
    {
        ItemInventory.OnItemPickedUp -= HandleItemPickedUp;
    }

    private void HandleItemPickedUp(ItemType type)
    {
        if (type == ItemType.None) return;
        
        float addedDuration = 10f;
        Color themeColor = Color.white;

        TryLoadSprites();

        Sprite icon = null;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
        {
            var localClient = NetworkManager.Singleton.LocalClient;
            if (localClient != null && localClient.PlayerObject != null)
            {
                ItemInventory inv = localClient.PlayerObject.GetComponent<ItemInventory>();
                if (inv != null)
                {
                    if (type == ItemType.BuffCoin) 
                    {
                        addedDuration = inv.BuffCoinDuration;
                        themeColor = new Color(1f, 0.85f, 0f, 1f); // Vàng
                        icon = buffCoinSprite;
                    }
                    else if (type == ItemType.DoubleBarrel) 
                    {
                        addedDuration = inv.DoubleBarrelDuration;
                        themeColor = new Color(0f, 0.8f, 1f, 1f); // Cyan
                        icon = doubleBarrelSprite;
                    }
                    else if (type == ItemType.Trap) 
                    {
                        addedDuration = inv.TrapDuration;
                        themeColor = new Color(1f, 0.3f, 0.3f, 1f); // Đỏ
                        icon = trapSprite;
                    }
                }
            }
        }
        else
        {
            if (type == ItemType.Trap) addedDuration = 5f;
        }

        ActiveItem existing = activeItems.Find(x => x.Type == type);
        if (existing != null)
        {
            existing.Timer += addedDuration; // Cộng dồn thời gian
        }
        else
        {
            ActiveItem newItem = new ActiveItem
            {
                Type = type,
                Timer = addedDuration,
                ThemeColor = themeColor,
                BgTexture = CreateBackgroundTexture(themeColor),
                IconSprite = icon
            };
            activeItems.Add(newItem);
        }
    }

    private void Update()
    {
        for (int i = activeItems.Count - 1; i >= 0; i--)
        {
            activeItems[i].Timer -= Time.deltaTime;
            if (activeItems[i].Timer <= 0)
            {
                if (activeItems[i].BgTexture != null)
                {
                    Destroy(activeItems[i].BgTexture);
                }
                activeItems.RemoveAt(i);
            }
        }
    }

    private Texture2D CreateBackgroundTexture(Color themeColor)
    {
        Texture2D bgTexture = new Texture2D(64, 64);
        Color bgColor = new Color(0.1f, 0.1f, 0.1f, 0.9f); 
        Color borderColor = themeColor * 0.7f; 
        Color highlightColor = themeColor; 
        Color shadowColor = new Color(0.05f, 0.05f, 0.05f, 1f); 

        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                if (x < 2 || x > 61 || y < 2 || y > 61)
                {
                    bgTexture.SetPixel(x, y, shadowColor);
                }
                else if (x < 6 || x > 57 || y < 6 || y > 57)
                {
                    bgTexture.SetPixel(x, y, borderColor);
                }
                else if (x == 6 || x == 57 || y == 6 || y == 57)
                {
                    bgTexture.SetPixel(x, y, highlightColor);
                }
                else
                {
                    float noise = UnityEngine.Random.Range(0.9f, 1.1f);
                    bgTexture.SetPixel(x, y, bgColor * noise);
                }
            }
        }
        bgTexture.Apply();
        return bgTexture;
    }

    private void OnGUI()
    {
        if (activeItems.Count == 0) return;

        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Screen.width / 1920f, Screen.height / 1080f, 1f));

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 40,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };

        float currentY = 970f - 20f; // Bắt đầu từ vị trí ngay trên CoinUI

        for (int i = 0; i < activeItems.Count; i++)
        {
            ActiveItem item = activeItems[i];
            
            string itemName = "";
            if (item.Type == ItemType.BuffCoin) itemName = "✨ X3 COIN ✨";
            else if (item.Type == ItemType.DoubleBarrel) itemName = "🔥 ĐẠN ĐÔI 🔥";
            else if (item.Type == ItemType.Trap) itemName = "💀 DÍNH BẪY 💀";

            string text = $"{itemName}\n⏳ {Mathf.CeilToInt(item.Timer)}s";
            
            Vector2 textSize = style.CalcSize(new GUIContent($"{itemName}\n⏳ 00s"));
            
            float iconSize = 70f;
            float padding = 20f;
            
            float boxWidth = 20f + iconSize + padding + textSize.x + 40f; 
            float boxHeight = Mathf.Max(textSize.y + 40f, iconSize + 40f);
            
            currentY -= boxHeight; // Đẩy khung lên trên để xếp dọc
            
            Rect boxRect = new Rect(30f, currentY, boxWidth, boxHeight);
            
            GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = item.BgTexture;
            boxStyle.border = new RectOffset(8, 8, 8, 8);
            
            // Vẽ hộp nền
            GUI.Box(boxRect, GUIContent.none, boxStyle);
            
            // Tính toán vị trí vẽ icon và text
            Rect iconRect = new Rect(boxRect.x + 20f, boxRect.y + (boxHeight - iconSize) / 2f, iconSize, iconSize);
            Rect textRect = new Rect(iconRect.xMax + padding, boxRect.y, textSize.x, boxHeight);
            Rect shadowTextRect = new Rect(textRect.x + 3f, textRect.y + 3f, textRect.width, textRect.height);

            // Vẽ Icon Sprite
            if (item.IconSprite != null && item.IconSprite.texture != null)
            {
                Rect tr = item.IconSprite.textureRect;
                Texture2D tex = item.IconSprite.texture;
                GUI.DrawTextureWithTexCoords(iconRect, tex, new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height));
            }
            
            GUIStyle shadowStyle = new GUIStyle(style);
            shadowStyle.normal.textColor = new Color(0, 0, 0, 0.8f);
            
            // Vẽ chữ bóng
            GUI.Label(shadowTextRect, text, shadowStyle);
            
            style.normal.textColor = item.ThemeColor;

            // Nhấp nháy khi sắp hết giờ (dưới 3s)
            if (item.Timer <= 3f)
            {
                float alpha = (Mathf.Sin(Time.time * 10f) + 1f) / 2f;
                style.normal.textColor = new Color(item.ThemeColor.r, item.ThemeColor.g, item.ThemeColor.b, 0.4f + alpha * 0.6f);
            }

            // Vẽ chữ sáng
            GUI.Label(textRect, text, style);
            
            currentY -= (boxHeight + 10f); // Khoảng cách giữa các hộp
        }
    }
}
