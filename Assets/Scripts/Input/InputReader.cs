using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System;
using static Controls;

[CreateAssetMenu(fileName = "New Input Reader", menuName = "Input/Input Reader")]
public class InputReader : ScriptableObject, IPlayerActions
{
    public event Action<Vector2> MoveEvent;

    public event Action<bool> PrimaryFireEvent;

    public Vector2 ViTriNgam { get; private set; }

    private Controls controls;


    private void OnEnable()
    {
        if (controls == null)
        {
            controls = new Controls();
            controls.Player.SetCallbacks(this);
        }

        controls.Player.Enable();

    }

    private void OnDisable()
    {
        if (controls == null) { return; }

        controls.Player.Disable();
        controls.Player.SetCallbacks(null);
        if (controls.asset != null)
        {
            if (Application.isPlaying)
            {
                Destroy(controls.asset);
            }
            else
            {
                DestroyImmediate(controls.asset);
            }
        }
        controls = null;
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        MoveEvent?.Invoke(context.ReadValue<Vector2>());
    }

    public void OnPrimaryFire(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            PrimaryFireEvent?.Invoke(true);  
        }
        else if (context.canceled)
        {
            PrimaryFireEvent?.Invoke(false);
        }
    }

    public void OnAim(InputAction.CallbackContext context)
    {
        ViTriNgam = context.ReadValue<Vector2>();
    }

}