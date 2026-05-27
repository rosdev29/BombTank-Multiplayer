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
            return player;
        }

        return null;
    }

    private void OnGUI()
    {
        if (!isSpectating) { return; }

        int seconds = Mathf.CeilToInt(Mathf.Max(0f, countdownRemaining));
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        Rect messageRect = new Rect(0f, Screen.height * 0.38f, Screen.width, 40f);
        GUI.Label(messageRect, $"Ban da chet. Hoi sinh sau {seconds} giay...", style);

        GUIStyle hintStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 16,
            normal = { textColor = new Color(0.9f, 0.9f, 0.9f, 1f) }
        };
        Rect hintRect = new Rect(0f, Screen.height * 0.44f, Screen.width, 30f);
        GUI.Label(hintRect, "Che do theo doi: bam A/D hoac mui ten trai/phai de doi muc tieu", hintStyle);
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
