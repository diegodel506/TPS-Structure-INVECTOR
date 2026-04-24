/* 
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
        [SerializeField] private bool forceWalkByDefaultOnStart = true;
        [SerializeField] private bool uncrouchWhenSprintPressed = true;

        [Header("Sprint Release Transition")]
        [SerializeField] private bool enableSprintToRunToWalk = true;
        [SerializeField] private float runToWalkDelay = 0.35f;
        [SerializeField] private float movingThreshold = 0.1f;

        [Header("Debug")]
        [SerializeField] private bool shiftHeld;
        [SerializeField] private bool wasShiftHeld;
        [SerializeField] private bool isAiming;
        [SerializeField] private bool isMoving;
        [SerializeField] private float runToWalkTimer;

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

        private void Start()
        {
            if (!enableAdapter || controller == null)
                return;

            if (forceWalkByDefaultOnStart)
                controller.alwaysWalkByDefault = true;
        }

        private void Update()
        {
            if (!enableAdapter || controller == null || input == null)
                return;

            wasShiftHeld = shiftHeld;
            shiftHeld = input.sprintInput.GetButton();

            isAiming = aimAdapter != null && aimAdapter.IsAiming;
            isMoving = controller.inputMagnitude > movingThreshold;

            UpdateSprintReleaseTimer();
        }

        private void LateUpdate()
        {
            if (!enableAdapter || controller == null)
                return;

            ApplySprintMode();
        }

        private void UpdateSprintReleaseTimer()
        {
            if (!enableSprintToRunToWalk)
            {
                runToWalkTimer = 0f;
                return;
            }

            if (isAiming)
            {
                runToWalkTimer = 0f;
                return;
            }

            if (controller.isCrouching)
            {
                runToWalkTimer = 0f;
                return;
            }

            if (shiftHeld)
            {
                runToWalkTimer = 0f;
                return;
            }

            bool justReleasedShift = wasShiftHeld && !shiftHeld;

            if (justReleasedShift && isMoving)
            {
                runToWalkTimer = runToWalkDelay;
                return;
            }

            if (runToWalkTimer > 0f)
            {
                if (!isMoving)
                {
                    runToWalkTimer = 0f;
                    return;
                }

                runToWalkTimer -= Time.deltaTime;

                if (runToWalkTimer < 0f)
                    runToWalkTimer = 0f;
            }
        }

        private void ApplySprintMode()
        {
            // En Aim no tocamos nada.
            if (isAiming)
                return;

            // Si está en crouch y NO está intentando correr,
            // dejamos el comportamiento original de Invector intacto.
            if (controller.isCrouching && !shiftHeld)
            {
                controller.alwaysWalkByDefault = false;
                return;
            }

            if (shiftHeld)
            {
                if (uncrouchWhenSprintPressed && controller.isCrouching)
                    controller.isCrouching = false;

                // Shift presionado:
                // permite Run/Sprint.
                controller.alwaysWalkByDefault = false;
                return;
            }

            // Shift soltado, pero seguimos en movimiento:
            // mantenemos Run por un pequeño tiempo antes de volver a Walk.
            if (enableSprintToRunToWalk && runToWalkTimer > 0f && isMoving)
            {
                controller.alwaysWalkByDefault = false;
                return;
            }

            // Sin shift:
            // vuelve a walk default solo si está de pie.
            if (!controller.isCrouching)
                controller.alwaysWalkByDefault = true;
        }
    }
}
 */

using UnityEngine;
using Invector.vCharacterController;

// IMPORTANTE:
// El crouch debe conservar el comportamiento original de Invector.
// El modo walk-by-default solo se aplica a locomoción normal de pie.

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
        [SerializeField] private bool forceWalkByDefaultOnStart = true;
        [SerializeField] private bool uncrouchWhenSprintPressed = true;

        [Header("Debug")]
        [SerializeField] private bool shiftHeld;
        [SerializeField] private bool isAiming;
        //[SerializeField] private bool usingWalkDefault = true;

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

        private void Start()
        {
            if (!enableAdapter || controller == null)
                return;

            if (forceWalkByDefaultOnStart)
            {
                controller.alwaysWalkByDefault = true;
                //usingWalkDefault = true;
            }
        }

        private void Update()
        {
            if (!enableAdapter || controller == null || input == null)
                return;

            shiftHeld = input.sprintInput.GetButton();
            isAiming = aimAdapter != null && aimAdapter.IsAiming;
        }

        private void LateUpdate()
        {
            if (!enableAdapter || controller == null)
                return;

            ApplySprintMode();
        }

        private void ApplySprintMode()
        {
            // En Aim no tocamos nada
            if (isAiming)
                return;

            // Si está en crouch y NO está intentando correr,
            // dejamos el comportamiento original de Invector intacto.
            if (controller.isCrouching && !shiftHeld)
            {
                controller.alwaysWalkByDefault = false;
                //usingWalkDefault = false;
                return;
            }

            if (shiftHeld)
            {
                // Si quiere correr/sprintar y está en crouch, salir del crouch
                if (uncrouchWhenSprintPressed && controller.isCrouching)
                {
                    controller.isCrouching = false;

                }

                // Solo en locomoción normal de pie:
                // quitamos walk por defecto para permitir sprint / run
                controller.alwaysWalkByDefault = false;
                //usingWalkDefault = false;
            }
            else
            {
                // Solo volvemos a walk default si NO está en crouch
                if (!controller.isCrouching)
                {
                    controller.alwaysWalkByDefault = true;
                    //usingWalkDefault = true;
                }
            }
        }
    }
} 