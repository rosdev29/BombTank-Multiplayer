#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

public static class BountySetupEditor
{
    [MenuItem("Tools/Bounty/Setup Crown Display")]
    public static void Setup()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab Player");

        if (guids.Length == 0)
        {
            Debug.LogError("[BountySetup] Không tìm thấy prefab tên 'Player'.");
            return;
        }

        string prefabPath = AssetDatabase.GUIDToAssetPath(guids[0]);

        GameObject prefabRoot =
            PrefabUtility.LoadPrefabContents(prefabPath);

        if (prefabRoot == null)
        {
            Debug.LogError("[BountySetup] Không load được prefab.");
            return;
        }

        CrownDisplay crownDisplay =
            prefabRoot.GetComponent<CrownDisplay>();

        if (crownDisplay == null)
        {
            crownDisplay =
                prefabRoot.AddComponent<CrownDisplay>();

            Debug.Log("[BountySetup] Added CrownDisplay.");
        }

        Transform crownTransform =
            prefabRoot.transform.Find("CrownIcon");

        GameObject crownObject;

        if (crownTransform == null)
        {
            crownObject = new GameObject("CrownIcon");

            crownObject.transform.SetParent(
                prefabRoot.transform,
                false);

            crownObject.transform.localPosition =
                new Vector3(0f, 1.5f, 0f);

            crownObject.SetActive(false);

            Debug.Log("[BountySetup] Created CrownIcon.");
        }
        else
        {
            crownObject = crownTransform.gameObject;
        }

        SerializedObject so =
            new SerializedObject(crownDisplay);

        SerializedProperty crownField =
            so.FindProperty("crownObject");

        if (crownField != null)
        {
            crownField.objectReferenceValue = crownObject;
            so.ApplyModifiedProperties();
        }

        SpriteRenderer spriteRenderer =
            crownObject.GetComponent<SpriteRenderer>();

        if (spriteRenderer == null)
        {
            spriteRenderer = crownObject.AddComponent<SpriteRenderer>();
        }

        Sprite bountySprite =
            AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Art/BountyCrown.png");

        if (bountySprite != null)
        {
            spriteRenderer.sprite = bountySprite;
            spriteRenderer.color = new Color(1f, 0.84f, 0f, 1f);
            spriteRenderer.sortingOrder = 25;
        }
        else
        {
            Debug.LogWarning(
                "[BountySetup] Không tìm thấy Assets/Art/BountyCrown.png.");
        }

        PrefabUtility.SaveAsPrefabAsset(
            prefabRoot,
            prefabPath);

        PrefabUtility.UnloadPrefabContents(
            prefabRoot);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[BountySetup] Hoàn tất setup prefab: {prefabPath}");
    }
}

#endif