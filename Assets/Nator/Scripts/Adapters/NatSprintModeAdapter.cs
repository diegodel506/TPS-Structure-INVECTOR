using UnityEngine;
using Invector.vCharacterController;

namespace Nator.Adapters
{
    [DisallowMultipleComponent]
    public class NatSprintModeAdapter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private vThirdPersonController controller;
        [SerializeField] private vThirdPersonInput input;
        [SerializeField] private NatAimModeAdapter aimAdapter;

        [Header("Behavior")]
        [SerializeField] private bool enableAdapter = true;
        [SerializeField] private bool returnToWalkWhenShiftReleased = true;
        [SerializeField] private bool forceWalkByDefaultOnStart = true;

        [Header("Debug")]
        [SerializeField] private bool shiftHeld;
        [SerializeField] private bool staminaForcedRun;
        [SerializeField] private bool walkModeActive = true;

        private bool eventsHooked;

        private void Reset()
        {
            if (!controller)
                controller = GetComponent<vThirdPersonController>();

            if (!input)
                input = GetComponent<vThirdPersonInput>();

            if (!aimAdapter)
                aimAdapter = GetComponent<NatAimModeAdapter>();
        }

        private void Awake()
        {
            if (!controller)
                controller = GetComponent<vThirdPersonController>();

            if (!input)
                input = GetComponent<vThirdPersonInput>();

            if (!aimAdapter)
                aimAdapter = GetComponent<NatAimModeAdapter>();
        }

        private void OnEnable()
        {
            HookEvents();
        }

        private void Start()
        {
            if (!enableAdapter || controller == null)
                return;

            if (forceWalkByDefaultOnStart)
            {
                controller.alwaysWalkByDefault = true;
                walkModeActive = true;
                staminaForcedRun = false;
            }
        }

        private void OnDisable()
        {
            UnhookEvents();
        }

        private void Update()
        {
            if (!enableAdapter || controller == null || input == null)
                return;

            shiftHeld = input.sprintInput.GetButton();
        }

        private void LateUpdate()
        {
            if (!enableAdapter || controller == null)
                return;

            ApplyMode();
        }

        private void ApplyMode()
        {
            bool isAiming = aimAdapter != null && aimAdapter.IsAiming;

            if (isAiming)
            {
                // AIM MODE
                controller.alwaysWalkByDefault = true;

                if (shiftHeld)
                {
                    // SHIFT = RUN (no sprint)
                    controller.isSprinting = false;
                    controller.alwaysWalkByDefault = false;
                }
                else
                {
                    // default = WALK
                    controller.isSprinting = false;
                    controller.alwaysWalkByDefault = true;
                }

                walkModeActive = true;
                return;
            }

            // NORMAL MODE (tu lógica original)
            if (shiftHeld)
            {
                controller.alwaysWalkByDefault = false;
                controller.isSprinting = true;
                walkModeActive = false;
            }
            else
            {
                if (returnToWalkWhenShiftReleased)
                {
                    controller.alwaysWalkByDefault = true;
                    controller.isSprinting = false;
                    staminaForcedRun = false;
                    walkModeActive = true;
                }
            }
        }

        private void HookEvents()
        {
            if (eventsHooked || controller == null)
                return;

            if (controller.OnStaminaEnd != null)
                controller.OnStaminaEnd.AddListener(HandleStaminaEnd);

            if (controller.OnFinishSprintingByStamina != null)
                controller.OnFinishSprintingByStamina.AddListener(HandleFinishSprintByStamina);

            eventsHooked = true;
        }

        private void UnhookEvents()
        {
            if (!eventsHooked || controller == null)
                return;

            if (controller.OnStaminaEnd != null)
                controller.OnStaminaEnd.RemoveListener(HandleStaminaEnd);

            if (controller.OnFinishSprintingByStamina != null)
                controller.OnFinishSprintingByStamina.RemoveListener(HandleFinishSprintByStamina);

            eventsHooked = false;
        }

        private void HandleStaminaEnd()
        {
            if (!enableAdapter || controller == null)
                return;

            if (shiftHeld)
            {
                controller.alwaysWalkByDefault = false;
                staminaForcedRun = true;
                walkModeActive = false;
            }
        }

        private void HandleFinishSprintByStamina()
        {
            if (!enableAdapter || controller == null)
                return;

            if (shiftHeld)
            {
                controller.alwaysWalkByDefault = false;
                staminaForcedRun = true;
                walkModeActive = false;
            }
        }
    }
}