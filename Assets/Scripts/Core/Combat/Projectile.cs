using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    public int TeamIndex { get; private set; } = -1;

    public void Initialise(int teamIndex)
    {
        TeamIndex = teamIndex;
    }
}
