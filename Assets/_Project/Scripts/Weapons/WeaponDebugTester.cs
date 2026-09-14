using UnityEngine;


public class WeaponDebugTester : MonoBehaviour
{
    public WeaponController weapon;


    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.U))
        {
            weapon.IncreaseWeaponLevel();
        }
    }
}