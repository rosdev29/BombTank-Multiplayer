using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Client-only runtime UI/logic for death countdown + spectator camera.
/// Automatically created at runtime, so no scene wiring is required.
/// Controls Cinemachine virtual cameras by boosting priority so the
/// main camera follows the chosen target.
/// </summary>
public class DeathSpectatorClient : MonoBehaviour
{
    private const int SpectatorBoostPriority = 100;
    private const float RebuildIntervalSeconds = 0.25f;

    private static DeathSpectatorClient instance;

    private float countdownRemaining;
    private bool isSpectating;
    private int targetIndex;
    private float nextRebuildTime;

    private readonly List<TankPlayer> spectateTargets = new List<TankPlayer>();

    private CinemachineVirtualCamera boostedCamera;
    private int boostedOriginalPriority;
    private TankPlayer boostedTarget;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (instance != null) { return; }
        GameObject go = new GameObject("DeathSpectatorClient");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<DeathSpectatorClient>();
    }

    public static void NotifyDeath(float delaySeconds)
    {
        if (instance == null)
        {
            EnsureInstance();
        }

        instance?.BeginDeath(delaySeconds);
    }

    public static void Dismiss()
    {
        if (instance == null) { return; }

        instance.isSpectating = false;
        instance.ReleaseBoost();
    }

    private void BeginDeath(float delaySeconds)
    {
        countdownRemaining = Mathf.Max(0f, delaySeconds);
        isSpectating = true;
        targetIndex = 0;
        nextRebuildTime = 0f;

        RebuildTargets(force: true);
        ApplyBoostToCurrentTarget();
    }

    private void Update()
    {
        if (!isSpectating) { return; }

        // Respawned: stop spectator mode automatically.
        if (GetOwnedAlivePlayer() != null)
        {
            EndSpectating();
            return;
        }

        if (countdownRemaining > 0f)
        {
            countdownRemaining -= Time.deltaTime;
        }

        if (Time.unscaledTime >= nextRebuildTime)
        {
            RebuildTargets(force: false);
            nextRebuildTime = Time.unscaledTime + RebuildIntervalSeconds;
            EnsureBoostMatchesTarget();
        }

        if (IsPreviousTargetPressed())
        {
            SwitchTarget(-1);
        }
        else if (IsNextTargetPressed())
        {
            SwitchTarget(1);
        }
    }

    private void EndSpectating()
    {
        isSpectating = false;
        ReleaseBoost();
    }

    private void RebuildTargets(bool force)
    {
        if (!force && Time.unscaledTime < nextRebuildTime) { return; }

        TankPlayer currentTarget = null;
        if (targetIndex >= 0 && targetIndex < spectateTargets.Count)
        {
            currentTarget = spectateTargets[targetIndex];
        }

        TankPlayer[] allPlayers = FindObjectsByType<TankPlayer>(FindObjectsSortMode.None);
        List<TankPlayer> realPlayers = new List<TankPlayer>();
        List<TankPlayer> bots = new List<TankPlayer>();

        foreach (TankPlayer player in allPlayers)
        {
            if (player == null || !player.IsSpawned) { continue; }
            if (player.IsOwner) { continue; }
            if (player.Health != null && player.Health.MauHienTai.Value <= 0) { continue; }
            if (player.IsCurrentlyBot())
            {
                bots.Add(player);
            }
            else
            {
                realPlayers.Add(player);
            }
        }

        spectateTargets.Clear();
        // Real players first, bots after (requirement).
        spectateTargets.AddRange(realPlayers);
        spectateTargets.AddRange(bots);

        if (spectateTargets.Count == 0)
        {
            targetIndex = 0;
            return;
        }

        // Keep current target if still in list to avoid resetting on every rebuild.
        if (currentTarget != null)
        {
            int preservedIndex = spectateTargets.IndexOf(currentTarget);
            if (preservedIndex >= 0)
            {
                targetIndex = preservedIndex;
                return;
            }
        }

        if (targetIndex < 0 || targetIndex >= spectateTargets.Count)
        {
            targetIndex = 0;
        }
    }

    private void SwitchTarget(int direction)
    {
        RebuildTargets(force: true);
        if (spectateTargets.Count == 0)
        {
            ReleaseBoost();
            return;
        }

        targetIndex = (targetIndex + direction + spectateTargets.Count) % spectateTargets.Count;
        ApplyBoostToCurrentTarget();
    }

    private void EnsureBoostMatchesTarget()
    {
        if (spectateTargets.Count == 0)
        {
            ReleaseBoost();
            return;
        }

        TankPlayer target = spectateTargets[targetIndex];
        if (target != boostedTarget || boostedCamera == null)
        {
            ApplyBoostToCurrentTarget();
        }
    }

    private void ApplyBoostToCurrentTarget()
    {
        if (spectateTargets.Count == 0)
        {
            ReleaseBoost();
            return;
        }

        TankPlayer target = spectateTargets[targetIndex];
        if (target == null) { return; }

        CinemachineVirtualCamera vcam = target.GetComponentInChildren<CinemachineVirtualCamera>(true);
        if (vcam == null) { return; }

        // If already boosting this target, nothing to do.
        if (boostedCamera == vcam && boostedTarget == target) { return; }

        ReleaseBoost();

        boostedCamera = vcam;
        boostedOriginalPriority = vcam.Priority;
        boostedTarget = target;
        vcam.Priority = SpectatorBoostPriority;
    }

    private void ReleaseBoost()
    {
        if (boostedCamera != null)
        {
            boostedCamera.Priority = boostedOriginalPriority;
        }

        boostedCamera = null;
        boostedTarget = null;
    }

    private TankPlayer GetOwnedAlivePlayer()
    {
        TankPlayer[] allPlayers = FindObjectsByType<TankPlayer>(FindObjectsSortMode.None);
        foreach (TankPlayer player in allPlayers)
        {
            if (player == null || !player.IsSpawned) { continue; }
            if (!player.IsOwner) { continue; }
            if (player.IsCurrentlyBot()) { continue; }
            if (player.Health != null && player.Health.MauHienTai.Value <= 0) { continue; }
            return player;
        }

        return null;
    }

    private void OnGUI()
    {
        if (!isSpectating) { return; }

        int seconds = Mathf.CeilToInt(Mathf.Max(0f, countdownRemaining));

        float width = 560f;
        float height = 330f;
        float x = (Screen.width - width) / 2f;
        float y = (Screen.height - height) / 2f;

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 34,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.red }
        };

        GUIStyle messageStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        GUIStyle hintStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 16,
            normal = { textColor = new Color(0.9f, 0.9f, 0.9f, 1f) }
        };

        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold
        };

        GUI.Box(new Rect(x, y, width, height), "", boxStyle);

        GUI.Label(new Rect(x, y + 25f, width, 50f), "BẠN ĐÃ CHẾT", titleStyle);

        GUI.Label(
            new Rect(x + 40f, y + 95f, width - 80f, 45f),
            $"Hồi sinh sau: {seconds} giây",
            messageStyle
        );

        GUI.Label(
            new Rect(x + 40f, y + 150f, width - 80f, 50f),
            "Nhấn A / D để xem người chơi khác",
            hintStyle
        );

        if (GUI.Button(new Rect(x + (width - 170f) / 2f, y + 235f, 170f, 55f), "VỀ MENU", buttonStyle))
        {
            GoHome();
        }
    }

    private void GoHome()
    {
        ClientSessionOverlay.ReturnToMenu();
    }


    private bool IsPreviousTargetPressed()
    {
        bool legacy = Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow);
        if (legacy) { return true; }

        if (Keyboard.current == null) { return false; }
        return Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame;
    }

    private bool IsNextTargetPressed()
    {
        bool legacy = Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow);
        if (legacy) { return true; }

        if (Keyboard.current == null) { return false; }
        return Keyboard.current.dKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame;
    }
}
