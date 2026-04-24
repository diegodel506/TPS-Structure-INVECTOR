using UnityEngine;

[DisallowMultipleComponent]
public class FootIKStateAdapter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FootIKController footIK;

    [Header("Manual State Source")]
    [SerializeField] private bool grounded = true;
    [SerializeField] private bool jumping = false;
    [SerializeField] private bool rolling = false;
    [SerializeField] private bool customAction = false;

    private void Reset()
    {
        if (!footIK)
            footIK = GetComponent<FootIKController>();
    }

    private void LateUpdate()
    {
        if (!footIK)
            return;

        footIK.SetExternalStates(grounded, jumping, rolling, customAction);
    }

    public void SetGrounded(bool value) => grounded = value;
    public void SetJumping(bool value) => jumping = value;
    public void SetRolling(bool value) => rolling = value;
    public void SetCustomAction(bool value) => customAction = value;

    public void SetStates(bool isGrounded, bool isJumping, bool isRolling, bool isCustomAction = false)
    {
        grounded = isGrounded;
        jumping = isJumping;
        rolling = isRolling;
        customAction = isCustomAction;
    }
}