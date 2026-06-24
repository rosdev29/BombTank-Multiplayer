using UnityEngine;

/// <summary>
/// Cấu hình độ khó của bot.
/// Tạo 3 asset qua menu Bot > Bot Config: BotConfig_Easy / BotConfig_Medium / BotConfig_Hard
/// Folder gợi ý: Assets/ScriptableObjects/Bot/
/// </summary>
[CreateAssetMenu(fileName = "BotConfig", menuName = "Bot/Bot Config")]
public class BotConfig : ScriptableObject
{
    [Header("Tốc độ bắn")]
    [Tooltip("Thời gian (giây) giữa hai viên đạn. Dễ=1.5 / Trung=1.0 / Khó=0.6")]
    public float thoiGianGiuaHaiVien = 1.0f;

    [Header("Độ chính xác")]
    [Tooltip("Sai số ngắm (độ ±). Dễ=5 / Trung=2 / Khó=0.5")]
    public float saiSoNgamDo = 2f;

    [Header("Máu")]
    [Tooltip("Máu tối đa. Dễ=70 / Trung=100 / Khó=130")]
    public int mauToiDa = 100;

    [Header("Ngưỡng rút lui")]
    [Tooltip("Tỷ lệ HP (0–1) dưới ngưỡng này bot rút lui. Dễ=0.5 / Trung=0.3 / Khó=0.15")]
    [Range(0f, 1f)]
    public float nguongRutLui = 0.30f;

    [Tooltip("Tỷ lệ HP (0–1) để thoát rút lui. Phải > nguongRutLui. Dễ=0.6 / Trung=0.45 / Khó=0.3")]
    [Range(0f, 1f)]
    public float nguongThoatRutLui = 0.45f;
}
