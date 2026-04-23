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