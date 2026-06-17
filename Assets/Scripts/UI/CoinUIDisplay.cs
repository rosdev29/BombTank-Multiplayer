using Unity.Netcode;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class CoinUIDisplay : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI coinText;
    [SerializeField] private Image coinIcon;
    
    [Header("Settings")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color emptyColor = Color.red;
    [SerializeField] private string outOfCoinText = "Hết coin!";
    [SerializeField] private string coinPrefix = "";

    private CoinWallet localWallet;
    private BoPhongDan localWeapon;

    private void Update()
    {
        // Tìm Local Player khi người chơi vừa join game
        if (localWallet == null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient && NetworkManager.Singleton.IsConnectedClient)
        {
            var localPlayerObj = NetworkManager.Singleton.LocalClient?.PlayerObject;
            if (localPlayerObj != null)
            {
                localWallet = localPlayerObj.GetComponent<CoinWallet>();
                localWeapon = localPlayerObj.GetComponent<BoPhongDan>();
                
                if (localWallet != null)
                {
                    localWallet.TotalCoins.OnValueChanged += HandleCoinsChanged;
                    if (localWeapon != null)
                    {
                        localWeapon.IsDoubleBarrelActive.OnValueChanged += HandleDoubleBarrelChanged;
                    }
                    UpdateUI();
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (localWallet != null)
        {
            localWallet.TotalCoins.OnValueChanged -= HandleCoinsChanged;
        }
        if (localWeapon != null)
        {
            localWeapon.IsDoubleBarrelActive.OnValueChanged -= HandleDoubleBarrelChanged;
        }
    }

    private void HandleCoinsChanged(int previousValue, int newValue)
    {
        UpdateUI();
    }
    
    private void HandleDoubleBarrelChanged(bool previousValue, bool newValue)
    {
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (coinText == null || localWallet == null) return;

        int currentCoins = localWallet.TotalCoins.Value;
        int costPerShot = localWeapon != null ? localWeapon.GetChiPhiBan() : 5; 
        
        if (localWeapon != null && localWeapon.IsDoubleBarrelActive.Value)
        {
            costPerShot *= 2; // Bắn 2 nòng tốn x2 coin
        }

        if (currentCoins < costPerShot)
        {
            // Cảnh báo hết coin không bắn được
            coinText.text = outOfCoinText + $" ({currentCoins})";
            coinText.color = emptyColor;
            if (coinIcon != null) coinIcon.color = new Color(1f, 0.5f, 0.5f, 0.8f); // Đỏ mờ
        }
        else
        {
            // Hiển thị bình thường
            coinText.text = coinPrefix + currentCoins;
            coinText.color = normalColor;
            if (coinIcon != null) coinIcon.color = Color.white;
        }
    }
}
