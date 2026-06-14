#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tự động tạo 3 asset BotConfig (Easy / Medium / Hard).
/// Chạy: menu Tools > Bot > Tạo BotConfig Assets
/// </summary>
public static class BotConfigCreator
{
    private const string FOLDER = "Assets/Resources/Bot";

    [MenuItem("Tools/Bot/Tạo BotConfig Assets")]
    public static void TaoBotConfigAssets()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(FOLDER))
            AssetDatabase.CreateFolder("Assets/Resources", "Bot");

        TaoAsset("BotConfig_Easy",   thoiGian: 1.5f, saiSo: 5f,   mau: 70,  rutLui: 0.50f);
        TaoAsset("BotConfig_Medium", thoiGian: 1.0f, saiSo: 2f,   mau: 100, rutLui: 0.30f);
        TaoAsset("BotConfig_Hard",   thoiGian: 0.6f, saiSo: 0.5f, mau: 130, rutLui: 0.15f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[BotConfigCreator] Đã tạo 3 BotConfig asset tại " + FOLDER);
    }

    private static void TaoAsset(string ten, float thoiGian, float saiSo, int mau, float rutLui)
    {
        string duongDan = $"{FOLDER}/{ten}.asset";

        if (AssetDatabase.LoadAssetAtPath<BotConfig>(duongDan) != null)
        {
            Debug.Log($"[BotConfigCreator] {ten} đã tồn tại, bỏ qua.");
            return;
        }

        var config = ScriptableObject.CreateInstance<BotConfig>();
        config.thoiGianGiuaHaiVien = thoiGian;
        config.saiSoNgamDo         = saiSo;
        config.mauToiDa            = mau;
        config.nguongRutLui        = rutLui;

        AssetDatabase.CreateAsset(config, duongDan);
        Debug.Log($"[BotConfigCreator] Tạo {duongDan}");
    }
}
#endif
