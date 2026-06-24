#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

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

        // ─────────────────────────────────────────────────────────────
        // CrownDisplay
        // ─────────────────────────────────────────────────────────────
        CrownDisplay crownDisplay =
            prefabRoot.GetComponent<CrownDisplay>();

        if (crownDisplay == null)
        {
            crownDisplay =
                prefabRoot.AddComponent<CrownDisplay>();

            Debug.Log("[BountySetup] Added CrownDisplay.");
        }

        // ─────────────────────────────────────────────────────────────
        // Load Crown Sprite
        // ─────────────────────────────────────────────────────────────
        Sprite bountySprite =
            AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Art/BountyCrown.png");

        if (bountySprite == null)
        {
            Debug.LogWarning(
                "[BountySetup] Không tìm thấy Assets/Art/BountyCrown.png.");
        }

        // ─────────────────────────────────────────────────────────────
        // CrownIcon
        // ─────────────────────────────────────────────────────────────
        GameObject crownObject =
            FindOrCreateChild(prefabRoot, "CrownIcon");

        if (crownObject != null)
        {
            crownObject.transform.localPosition =
                new Vector3(0f, 2.5f, 0f);

            crownObject.SetActive(false);

            SpriteRenderer renderer =
                crownObject.GetComponent<SpriteRenderer>();

            if (renderer == null)
            {
                renderer =
                    crownObject.AddComponent<SpriteRenderer>();
            }

            if (bountySprite != null)
            {
                renderer.sprite = bountySprite;
                renderer.color = new Color(1f, 0.84f, 0f, 1f);
                renderer.sortingOrder = 25;
            }
        }

        // ─────────────────────────────────────────────────────────────
        // MinimapIcon (gốc)
        // ─────────────────────────────────────────────────────────────
        Transform minimapTransform =
            prefabRoot.transform.Find("MinimapIcon");

        GameObject minimapObject =
            minimapTransform != null
                ? minimapTransform.gameObject
                : null;

        if (minimapObject == null)
        {
            Debug.LogWarning(
                "[BountySetup] Không tìm thấy MinimapIcon.");
        }

        // ─────────────────────────────────────────────────────────────
        // CrownMinimapIcon
        // ─────────────────────────────────────────────────────────────
        GameObject crownMinimapObject =
            FindOrCreateChild(prefabRoot, "CrownMinimapIcon");

        if (crownMinimapObject != null)
        {
            crownMinimapObject.transform.localPosition =
                new Vector3(0f, 0f, 10f);

            crownMinimapObject.transform.localScale =
                new Vector3(1.5f, 1.5f, 1.5f);

            crownMinimapObject.layer = 8;

            crownMinimapObject.SetActive(false);

            SpriteRenderer renderer =
                crownMinimapObject.GetComponent<SpriteRenderer>();

            if (renderer == null)
            {
                renderer =
                    crownMinimapObject.AddComponent<SpriteRenderer>();
            }

            if (bountySprite != null)
            {
                renderer.sprite = bountySprite;
                renderer.color = new Color(1f, 0.84f, 0f, 1f);
                renderer.sortingOrder = 150;
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Gán references vào CrownDisplay
        // ─────────────────────────────────────────────────────────────
        SerializedObject so =
            new SerializedObject(crownDisplay);

        SerializedProperty crownField =
            so.FindProperty("crownObject");

        if (crownField != null)
        {
            crownField.objectReferenceValue =
                crownObject;
        }

        SerializedProperty minimapField =
            so.FindProperty("minimapIcon");

        if (minimapField != null)
        {
            minimapField.objectReferenceValue =
                minimapObject;
        }

        SerializedProperty crownMinimapField =
            so.FindProperty("crownMinimapObject");

        if (crownMinimapField != null)
        {
            crownMinimapField.objectReferenceValue =
                crownMinimapObject;
        }

        so.ApplyModifiedProperties();

        // ─────────────────────────────────────────────────────────────
        // Setup BountySystem trong Game Scene
        // ─────────────────────────────────────────────────────────────

        string scenePath = "Assets/Scenes/Game.unity";

        Scene scene = EditorSceneManager.OpenScene(
            scenePath,
            OpenSceneMode.Single);

        GameObject respawnHandler =
            GameObject.Find("RespawnHandler");

        if (respawnHandler == null)
        {
            Debug.LogWarning(
                "[BountySetup] Không tìm thấy RespawnHandler trong Game scene.");
        }
        else
        {
            BountySystem bountySystem =
                respawnHandler.GetComponent<BountySystem>();

            if (bountySystem == null)
            {
                bountySystem =
                    Undo.AddComponent<BountySystem>(respawnHandler);

                Debug.Log("[BountySetup] Added BountySystem.");
            }

            SerializedObject bountySO =
                new SerializedObject(bountySystem);

            bountySO.FindProperty("bountyThreshold")
                .intValue = 100;

            bountySO.FindProperty("bountyRewardPercent")
                .floatValue = 20f;

            bountySO.FindProperty("updateInterval")
                .floatValue = 0.2f;

            bountySO.ApplyModifiedProperties();

            EditorUtility.SetDirty(respawnHandler);

            Debug.Log(
                "[BountySetup] Configured BountySystem on RespawnHandler.");
        }

        EditorSceneManager.SaveScene(scene);

        // ─────────────────────────────────────────────────────────────
        // Save
        // ─────────────────────────────────────────────────────────────
        PrefabUtility.SaveAsPrefabAsset(
            prefabRoot,
            prefabPath);

        PrefabUtility.UnloadPrefabContents(
            prefabRoot);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[BountySetup] Hoàn tất setup CrownDisplay: {prefabPath}");
    }

    private static GameObject FindOrCreateChild(
        GameObject parent,
        string childName)
    {
        Transform child =
            parent.transform.Find(childName);

        if (child != null)
        {
            return child.gameObject;
        }

        GameObject go =
            new GameObject(childName);

        go.transform.SetParent(
            parent.transform,
            false);

        go.SetActive(false);

        Debug.Log(
            $"[BountySetup] Created {childName}.");

        return go;
    }
}

#endif