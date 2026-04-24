using UnityEngine;
using Invector.vCharacterController;

namespace Nator.Adapters
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10000)]
    public class NatAimModeAdapter : MonoBehaviour
    {
        #region References

        [Header("References")]
        [SerializeField] private vThirdPersonController controller;
        [SerializeField] private vThirdPersonInput input;

        #endregion

        #region Input

        [Header("Input")]
        [SerializeField] private KeyCode aimKey = KeyCode.Mouse1;

        #endregion

        #region Camera States

        [Header("Camera States")]
        [SerializeField] private string aimCameraState = "AimRightShoulder";
        [SerializeField] private string crouchAimState = "AimRightShoulderCrouch";
        [SerializeField] private bool smoothCameraTransition = true;
        [SerializeField] private bool useCrouchCamera = true;

        #endregion

        #region Aim Behavior

        [Header("Aim Behavior")]
        [SerializeField] private bool forceStrafeWhileAiming = true;
        [SerializeField] private bool blockSprintWhileAiming = true;
        [SerializeField] private bool blockRollWhileAiming = true;
        [SerializeField] private bool lockCameraToAimStateOnlyWhileHeld = true;

        #endregion

        #region Air Behavior

        [Header("Air Behavior")]
        [SerializeField] private bool keepStrafeInAir = true;
        [SerializeField] private bool rotateToCameraWhileAiming = true;
        [SerializeField] private bool forceJumpAndRotateWhileAiming = true;
        [SerializeField] private float aimRotationSpeed = 20f;

        #endregion

        #region Shoulder Swap

        [Header("Shoulder Swap")]
        [SerializeField] private bool allowShoulderSwap = true;
        [SerializeField] private KeyCode swapKey = KeyCode.Q;
        [SerializeField] private bool rightShoulder = true;
        [SerializeField] private bool keepLastShoulderWhenExitAim = true;
        [SerializeField] private float defaultShoulderValue = 1f;

        #endregion

        #region Debug

        [Header("Debug")]
        [SerializeField] private bool isAiming;
        [SerializeField] private float currentShoulderValue;
        [SerializeField] private float targetShoulderValue;

        #endregion

        #region Runtime Cache

        private bool originalJumpAndRotate;
        private bool cachedOriginalJumpAndRotate;

        private GenericInput originalRollInput;
        private GenericInput blockedRollInput;
        private bool isRollInputBlocked;

        #endregion

        #region Properties

        public bool IsAiming => isAiming;

        #endregion

        #region Unity

        private void Reset()
        {
            controller = GetComponent<vThirdPersonController>();
            input = GetComponent<vThirdPersonInput>();
        }

        private void Awake()
        {
            if (!controller)
                controller = GetComponent<vThirdPersonController>();

            if (!input)
                input = GetComponent<vThirdPersonInput>();

            CacheRollInput();
        }

        private void Start()
        {
            if (controller != null)
            {
                originalJumpAndRotate = controller.jumpAndRotate;
                cachedOriginalJumpAndRotate = true;
            }

            if (input != null && input.tpCamera != null)
            {
                currentShoulderValue = input.tpCamera.switchRight;

                if (Mathf.Approximately(currentShoulderValue, 0f))
                    currentShoulderValue = defaultShoulderValue;

                targetShoulderValue = currentShoulderValue;
            }
            else
            {
                currentShoulderValue = defaultShoulderValue;
                targetShoulderValue = defaultShoulderValue;
            }
        }

        private void Update()
        {
            if (controller == null || input == null)
                return;

            if (allowShoulderSwap && Input.GetKeyDown(swapKey))
                rightShoulder = !rightShoulder;

            isAiming = Input.GetKey(aimKey);

            ApplyRollBlock();
        }

        private void LateUpdate()
        {
            if (controller == null || input == null)
                return;

            ApplyAimMode();
            ApplyAimRotation();
            ApplyShoulderSwap();
        }

        private void OnDisable()
        {
            RestoreRollInput();

            if (cachedOriginalJumpAndRotate && controller != null)
                controller.jumpAndRotate = originalJumpAndRotate;
        }

        private void OnDestroy()
        {
            RestoreRollInput();
        }

        #endregion

        #region Aim Mode

        private void ApplyAimMode()
        {
            if (isAiming)
            {
                bool shiftHeld = input.sprintInput.GetButton();

                if (forceStrafeWhileAiming)
                    controller.isStrafing = true;

                // Bloquea Sprint real, pero permite pasar de Walk a Run con Shift
                if (blockSprintWhileAiming)
                    controller.isSprinting = false;

                // Sin Shift: Walk
                // Con Shift: Run
                controller.alwaysWalkByDefault = !shiftHeld;

                if (forceJumpAndRotateWhileAiming)
                    controller.jumpAndRotate = true;

                controller.keepDirection = false;

                input.changeCameraState = true;
                input.smoothCameraState = smoothCameraTransition;

                string targetState = aimCameraState;

                if (useCrouchCamera && controller.isCrouching)
                    targetState = crouchAimState;

                input.customCameraState = targetState;
            }
            else
            {
                if (forceStrafeWhileAiming)
                    controller.isStrafing = false;

                if (lockCameraToAimStateOnlyWhileHeld)
                {
                    input.changeCameraState = false;
                    input.customCameraState = string.Empty;
                }

                if (cachedOriginalJumpAndRotate)
                    controller.jumpAndRotate = originalJumpAndRotate;
            }
        }

        private void ApplyAimRotation()
        {
            if (!isAiming || !rotateToCameraWhileAiming)
                return;

            if (input.tpCamera == null)
                return;

            if (!keepStrafeInAir && !controller.isGrounded)
                return;

            Vector3 camForward = input.tpCamera.transform.forward;
            camForward.y = 0f;

            if (camForward.sqrMagnitude < 0.0001f)
                return;

            controller.RotateToDirection(camForward.normalized, aimRotationSpeed);

            if (keepStrafeInAir)
                controller.isStrafing = true;
        }

        #endregion

        #region Shoulder Swap

        private void ApplyShoulderSwap()
        {
            if (!allowShoulderSwap)
                return;

            if (input.tpCamera == null)
                return;

            if (isAiming)
            {
                targetShoulderValue = rightShoulder ? 1f : -1f;
            }
            else
            {
                if (!keepLastShoulderWhenExitAim)
                    targetShoulderValue = defaultShoulderValue;
                else
                    targetShoulderValue = rightShoulder ? 1f : -1f;
            }

            input.tpCamera.switchRight = targetShoulderValue;
        }

        #endregion

        #region Roll Block

        private void CacheRollInput()
        {
            if (input == null)
                return;

            originalRollInput = input.rollInput;

            blockedRollInput = new GenericInput("", "", "");
            blockedRollInput.useInput = false;
        }

        private void ApplyRollBlock()
        {
            if (!blockRollWhileAiming)
            {
                RestoreRollInput();
                return;
            }

            if (isAiming)
                BlockRollInput();
            else
                RestoreRollInput();
        }

        private void BlockRollInput()
        {
            if (input == null)
                return;

            if (isRollInputBlocked)
                return;

            if (originalRollInput == null)
                originalRollInput = input.rollInput;

            input.rollInput = blockedRollInput;
            isRollInputBlocked = true;
        }

        private void RestoreRollInput()
        {
            if (input == null)
                return;

            if (!isRollInputBlocked)
                return;

            if (originalRollInput != null)
                input.rollInput = originalRollInput;

            isRollInputBlocked = false;
        }

        #endregion
    }
}