using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class HienThiMau : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Mau mau;
    [SerializeField] private Image ThanhMauImage;

    private Image buffCoinImage;
    private Image doubleBarrelImage;
    private Image trapImage;
    private Text coinTextUI;
    private TankPlayer player;

    public override void OnNetworkSpawn()
    {
        if (!IsClient) { return; }

        mau.MauHienTai.OnValueChanged += XuLyKhiMauThayDoi;
        XuLyKhiMauThayDoi(0, mau.MauHienTai.Value);

        player = GetComponentInParent<TankPlayer>();
        if (player != null)
        {
            if (player.Inventory != null)
            {
                player.Inventory.IsCoinBuffActive.OnValueChanged += HandleBuffChanged;
                player.Inventory.IsTrapActive.OnValueChanged += HandleBuffChanged;
            }
            if (player.TryGetComponent<BoPhongDan>(out BoPhongDan combat))
                combat.IsDoubleBarrelActive.OnValueChanged += HandleBuffChanged;
        }

        RectTransform healthRect = ThanhMauImage.rectTransform;
        float iconSize = healthRect.rect.height * 5.0f; // Icon to gấp 5 lần chiều cao thanh máu (gấp đôi cũ)
        float offset = healthRect.rect.width / 2f + iconSize * 0.6f; // Căn ra mép thanh máu

        // Tạo Image đại diện cho BuffCoin
        GameObject imgObj1 = new GameObject("BuffCoinImage");
        imgObj1.transform.SetParent(ThanhMauImage.transform.parent, false);
        imgObj1.transform.localPosition = new Vector3(-offset, 0f, 0); 
        buffCoinImage = imgObj1.AddComponent<Image>();
        buffCoinImage.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
        buffCoinImage.gameObject.SetActive(false);

        // Tạo Image đại diện cho DoubleBarrel
        GameObject imgObj2 = new GameObject("DoubleBarrelImage");
        imgObj2.transform.SetParent(ThanhMauImage.transform.parent, false);
        imgObj2.transform.localPosition = new Vector3(offset, 0f, 0); 
        doubleBarrelImage = imgObj2.AddComponent<Image>();
        doubleBarrelImage.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
        doubleBarrelImage.gameObject.SetActive(false);

        // Tạo Image đại diện cho Trap
        GameObject imgObj3 = new GameObject("TrapImage");
        imgObj3.transform.SetParent(ThanhMauImage.transform.parent, false);
        imgObj3.transform.localPosition = new Vector3(0f, offset, 0); // Hiện phía trên thanh máu
        trapImage = imgObj3.AddComponent<Image>();
        trapImage.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
        trapImage.gameObject.SetActive(false);

        // Tìm ItemSpawner để lấy Sprite gốc của item
        ItemSpawner spawner = FindObjectOfType<ItemSpawner>();
        if (spawner != null)
        {
            if (spawner.buffCoinPrefab != null)
            {
                foreach (var sr in spawner.buffCoinPrefab.GetComponentsInChildren<SpriteRenderer>())
                {
                    if (sr.sprite != null && !sr.sprite.name.Contains("Circle") && !sr.sprite.name.Contains("Knob") && !sr.sprite.name.Contains("Shadow"))
                    {
                        buffCoinImage.sprite = sr.sprite;
                        break;
                    }
                }
            }
            if (spawner.doubleBarrelPrefab != null)
            {
                foreach (var sr in spawner.doubleBarrelPrefab.GetComponentsInChildren<SpriteRenderer>())
                {
                    if (sr.sprite != null && !sr.sprite.name.Contains("Circle") && !sr.sprite.name.Contains("Knob") && !sr.sprite.name.Contains("Shadow"))
                    {
                        doubleBarrelImage.sprite = sr.sprite;
                        break;
                    }
                }
            }
            if (spawner.trapPrefab != null)
            {
                foreach (var sr in spawner.trapPrefab.GetComponentsInChildren<SpriteRenderer>())
                {
                    if (sr.sprite != null && !sr.sprite.name.Contains("Circle") && !sr.sprite.name.Contains("Knob") && !sr.sprite.name.Contains("Shadow"))
                    {
                        trapImage.sprite = sr.sprite;
                        break;
                    }
                }
            }
        }

        // Tạo Text hiển thị Coin
        GameObject textObj = new GameObject("CoinText");
        textObj.transform.SetParent(ThanhMauImage.transform.parent, false);
        textObj.transform.localPosition = new Vector3(0f, -healthRect.rect.height * 4.0f, 0); 
        coinTextUI = textObj.AddComponent<Text>();
        coinTextUI.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        coinTextUI.fontSize = Mathf.RoundToInt(healthRect.rect.height * 8f);
        if (coinTextUI.fontSize == 0) coinTextUI.fontSize = 20;
        coinTextUI.alignment = TextAnchor.MiddleCenter;
        coinTextUI.horizontalOverflow = HorizontalWrapMode.Overflow;
        coinTextUI.verticalOverflow = VerticalWrapMode.Overflow;
        coinTextUI.rectTransform.sizeDelta = new Vector2(iconSize * 5f, iconSize);
        coinTextUI.rectTransform.localScale = Vector3.one;

        if (player != null && player.Wallet != null)
        {
            player.Wallet.TotalCoins.OnValueChanged += HandleCoinChanged;
            HandleCoinChanged(0, player.Wallet.TotalCoins.Value);
        }

        UpdateBuffUI();
    }

    public override void OnNetworkDespawn()
    {
        if(!IsClient) { return; }
        mau.MauHienTai.OnValueChanged -= XuLyKhiMauThayDoi;

        if (player != null)
        {
            if (player.Inventory != null)
            {
                player.Inventory.IsCoinBuffActive.OnValueChanged -= HandleBuffChanged;
                player.Inventory.IsTrapActive.OnValueChanged -= HandleBuffChanged;
            }
            if (player.TryGetComponent<BoPhongDan>(out BoPhongDan combat))
                combat.IsDoubleBarrelActive.OnValueChanged -= HandleBuffChanged;
            if (player.Wallet != null)
                player.Wallet.TotalCoins.OnValueChanged -= HandleCoinChanged;
        }
    }

    private void HandleBuffChanged(bool oldVal, bool newVal)
    {
        UpdateBuffUI();
    }

    private void UpdateBuffUI()
    {
        if (player == null) return;

        bool hasCoinBuff = player.Inventory != null && player.Inventory.IsCoinBuffActive.Value;
        bool hasDoubleBarrel = false;
        
        if (player.TryGetComponent<BoPhongDan>(out BoPhongDan combat))
        {
            hasDoubleBarrel = combat.IsDoubleBarrelActive.Value;
        }

        if (buffCoinImage != null) buffCoinImage.gameObject.SetActive(hasCoinBuff);
        if (doubleBarrelImage != null) doubleBarrelImage.gameObject.SetActive(hasDoubleBarrel);
    }

    private void XuLyKhiMauThayDoi(int mauOld, int mauMoi)
    {
        ThanhMauImage.fillAmount = (float)mauMoi / mau.MauToiDa;
    }

    private void HandleCoinChanged(int oldVal, int newVal)
    {
        if (coinTextUI == null || player == null) return;

        int requiredCoin = 0;
        if (player.TryGetComponent<BoPhongDan>(out BoPhongDan combat))
        {
            requiredCoin = combat.GetChiPhiBan();
            if (combat.IsDoubleBarrelActive.Value) requiredCoin *= 2;
        }

        if (newVal < requiredCoin)
        {
            coinTextUI.text = "HẾT COIN!";
            coinTextUI.color = Color.red;
        }
        else
        {
            coinTextUI.text = "🪙 " + newVal.ToString();
            coinTextUI.color = Color.yellow;
        }
    }
}
