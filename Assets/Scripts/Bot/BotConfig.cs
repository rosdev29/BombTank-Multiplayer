using UnityEngine;

[CreateAssetMenu(fileName = "NewBotConfig", menuName = "BombTank/Bot Config")]
public class BotConfig : ScriptableObject
{
    [Header("Cấp độ khó")]
    public string TenCapDo = "Medium";

    [Header("Thông số bắn")]
    [Tooltip("Thời gian chờ giữa 2 lần bắn (giây)")]
    public float ThoiGianChoBan = 1.0f;
    
    [Tooltip("Sai số ngắm (độ lệch góc tối đa)")]
    public float SaiSoNgam = 2.0f;

    [Header("Thông số sinh tồn")]
    [Tooltip("Ngưỡng máu chuyển sang Rút lui (0.0 - 1.0)")]
    public float NguongMauRutLui = 0.3f;
}
