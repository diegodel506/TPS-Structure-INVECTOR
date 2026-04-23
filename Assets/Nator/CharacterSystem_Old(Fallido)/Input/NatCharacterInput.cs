using UnityEngine;

namespace Nator.CharacterSystem.Input
{
    [DisallowMultipleComponent]
    public class NatCharacterInput : MonoBehaviour
    {
        [Header("Input Axes")]
        [SerializeField] private string horizontalAxis = "Horizontal";
        [SerializeField] private string verticalAxis = "Vertical";
        [SerializeField] private string mouseXAxis = "Mouse X";
        [SerializeField] private string mouseYAxis = "Mouse Y";

        [Header("Buttons")]
        [SerializeField] private KeyCode jumpKey = KeyCode.Space;
        [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;
        [SerializeField] private KeyCode crouchKey = KeyCode.C;
        [SerializeField] private KeyCode strafeKey = KeyCode.Tab;
        [SerializeField] private KeyCode rollKey = KeyCode.LeftAlt;

        [Header("Options")]
        [SerializeField] private bool invertLookY = false;
        [SerializeField] private bool normalizeMoveInput = true;
        [SerializeField] private bool holdToSprint = true;
        [SerializeField] private bool toggleCrouch = true;
        [SerializeField] private bool toggleStrafe = true;

        [Header("Debug")]
        [SerializeField] private Vector2 move;
        [SerializeField] private Vector2 look;
        [SerializeField] private bool jumpPressed;
        [SerializeField] private bool sprintHeld;
        [SerializeField] private bool crouchActive;
        [SerializeField] private bool strafeActive;
        [SerializeField] private bool rollPressed;

        public Vector2 Move => move;
        public Vector2 Look => look;

        public bool JumpPressed => jumpPressed;
        public bool SprintHeld => sprintHeld;
        public bool CrouchActive => crouchActive;
        public bool StrafeActive => strafeActive;
        public bool RollPressed => rollPressed;

        public void ReadInput()
        {
            ReadMove();
            ReadLook();
            ReadActions();
        }

        public void ClearOneFrameInputs()
        {
            jumpPressed = false;
            rollPressed = false;
        }

        public void ForceClearAll()
        {
            move = Vector2.zero;
            look = Vector2.zero;
            jumpPressed = false;
            sprintHeld = false;
            rollPressed = false;
        }

        private void ReadMove()
        {
            float x = UnityEngine.Input.GetAxisRaw(horizontalAxis);
            float y = UnityEngine.Input.GetAxisRaw(verticalAxis);

            move = new Vector2(x, y);

            if (normalizeMoveInput && move.sqrMagnitude > 1f)
                move = move.normalized;
        }

        private void ReadLook()
        {
            float x = UnityEngine.Input.GetAxis(mouseXAxis);
            float y = UnityEngine.Input.GetAxis(mouseYAxis);

            if (invertLookY)
                y = -y;

            look = new Vector2(x, y);
        }

        private void ReadActions()
        {
            jumpPressed = UnityEngine.Input.GetKeyDown(jumpKey);
            rollPressed = UnityEngine.Input.GetKeyDown(rollKey);

            if (holdToSprint)
            {
                sprintHeld = UnityEngine.Input.GetKey(sprintKey);
            }
            else
            {
                if (UnityEngine.Input.GetKeyDown(sprintKey))
                    sprintHeld = !sprintHeld;
            }

            if (toggleCrouch)
            {
                if (UnityEngine.Input.GetKeyDown(crouchKey))
                    crouchActive = !crouchActive;
            }
            else
            {
                crouchActive = UnityEngine.Input.GetKey(crouchKey);
            }

            if (toggleStrafe)
            {
                if (UnityEngine.Input.GetKeyDown(strafeKey))
                    strafeActive = !strafeActive;
            }
            else
            {
                strafeActive = UnityEngine.Input.GetKey(strafeKey);
            }
        }
    }
}