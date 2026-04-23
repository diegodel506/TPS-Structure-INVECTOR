using UnityEngine;
using Invector.vCharacterController;

namespace Nator.Adapters
{
    [DisallowMultipleComponent]
    public class NatAimModeAdapter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private vThirdPersonController controller;
        [SerializeField] private vThirdPersonInput input;

        [Header("Input")]
        [SerializeField] private KeyCode aimKey = KeyCode.Mouse1;

        [Header("Camera States")]
        [SerializeField] private string aimCameraState = "AimRightShoulder";
        [SerializeField] private string crouchAimState = "AimRightShoulderCrouch";
        [SerializeField] private bool smoothCameraTransition = true;
        [SerializeField] private bool useCrouchCamera = true;

        [Header("Aim Behavior")]
        [SerializeField] private bool forceStrafeWhileAiming = true;
        [SerializeField] private bool blockSprintWhileAiming = true;
        [SerializeField] private bool lockCameraToAimStateOnlyWhileHeld = true;

        [Header("Air Behavior")]
        [SerializeField] private bool keepStrafeInAir = true;
        [SerializeField] private bool rotateToCameraWhileAiming = true;
        [SerializeField] private bool forceJumpAndRotateWhileAiming = true;
        [SerializeField] private float aimRotationSpeed = 20f;

        [Header("Shoulder Swap")]
        [SerializeField] private bool allowShoulderSwap = true;
        [SerializeField] private KeyCode swapKey = KeyCode.Q;
        [SerializeField] private bool rightShoulder = true;
        [SerializeField] private bool keepLastShoulderWhenExitAim = true;
        [SerializeField] private float defaultShoulderValue = 1f;

        [Header("Debug")]
        [SerializeField] private bool isAiming;
        [SerializeField] private float currentShoulderValue;
        [SerializeField] private float targetShoulderValue;

        private float shoulderVelocity;
        private bool originalJumpAndRotate;
        private bool cachedOriginalJumpAndRotate;

        public bool IsAiming => isAiming;

        private void Reset()
        {
            if (!controller)
                controller = GetComponent<vThirdPersonController>();

            if (!input)
                input = GetComponent<vThirdPersonInput>();
        }

        private void Awake()
        {
            if (!controller)
                controller = GetComponent<vThirdPersonController>();

            if (!input)
                input = GetComponent<vThirdPersonInput>();
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
        }

        private void LateUpdate()
        {
            if (controller == null || input == null)
                return;

            ApplyAimMode();
            ApplyAimRotation();
            ApplyShoulderSwap();
        }

        private void ApplyAimMode()
        {
            if (isAiming)
            {
                if (forceStrafeWhileAiming)
                    controller.isStrafing = true;

                if (blockSprintWhileAiming)
                    controller.isSprinting = false;

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
    }
}