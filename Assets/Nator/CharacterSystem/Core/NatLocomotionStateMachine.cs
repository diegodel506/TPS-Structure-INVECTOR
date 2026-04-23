using UnityEngine;
using Nator.CharacterSystem.Animation;

namespace Nator.CharacterSystem.Core
{
    [DisallowMultipleComponent]
    public class NatLocomotionStateMachine : MonoBehaviour
    {
        [Header("Current State")]
        [SerializeField] private NatLocomotionMode locomotionMode = NatLocomotionMode.Free;
        [SerializeField] private NatLocomotionState locomotionState = NatLocomotionState.Grounded;
        [SerializeField] private NatStance stance = NatStance.Standing;

        [Header("Flags")]
        [SerializeField] private bool isGrounded = true;
        [SerializeField] private bool isSprinting;
        [SerializeField] private bool isSliding;
        [SerializeField] private bool isRolling;
        [SerializeField] private bool isDead;
        [SerializeField] private bool wantsToStrafe;
        [SerializeField] private bool wantsToCrouch;
        [SerializeField] private bool wantsToSprint;
        [SerializeField] private bool wantsToRoll;
        [SerializeField] private bool wantsToJump;

        [Header("Read-only Output")]
        [SerializeField] private bool canMove = true;
        [SerializeField] private bool canRotate = true;
        [SerializeField] private bool useRootMotion;
        [SerializeField] private bool ignoreGroundSnap;

        public NatLocomotionMode LocomotionMode => locomotionMode;
        public NatLocomotionState LocomotionState => locomotionState;
        public NatStance Stance => stance;

        public bool IsGrounded => isGrounded;
        public bool IsSprinting => isSprinting;
        public bool IsSliding => isSliding;
        public bool IsRolling => isRolling;
        public bool IsDead => isDead;

        public bool CanMove => canMove;
        public bool CanRotate => canRotate;
        public bool UseRootMotion => useRootMotion;
        public bool IgnoreGroundSnap => ignoreGroundSnap;

        public void SetInputs(
            bool strafe,
            bool crouch,
            bool sprint,
            bool roll,
            bool jump)
        {
            wantsToStrafe = strafe;
            wantsToCrouch = crouch;
            wantsToSprint = sprint;
            wantsToRoll = roll;
            wantsToJump = jump;
        }

        public void SetPhysicalState(
            bool grounded,
            bool sliding,
            bool dead)
        {
            isGrounded = grounded;
            isSliding = sliding;
            isDead = dead;
        }

        public void Evaluate(NatAnimatorBridge animatorBridge)
        {
            ResolveMode();
            ResolveStance();
            ResolveState(animatorBridge);
            ResolvePermissions(animatorBridge);
        }

        private void ResolveMode()
        {
            locomotionMode = wantsToStrafe
                ? NatLocomotionMode.Strafe
                : NatLocomotionMode.Free;
        }

        private void ResolveStance()
        {
            stance = wantsToCrouch
                ? NatStance.Crouching
                : NatStance.Standing;
        }

        private void ResolveState(NatAnimatorBridge animatorBridge)
        {
            if (isDead)
            {
                locomotionState = NatLocomotionState.Dead;
                isRolling = false;
                isSprinting = false;
                return;
            }

            if (animatorBridge != null && animatorBridge.CustomAction)
            {
                locomotionState = NatLocomotionState.CustomAction;
                isRolling = animatorBridge.IsRolling;
                isSprinting = false;
                return;
            }

            if (wantsToRoll)
            {
                locomotionState = NatLocomotionState.Rolling;
                isRolling = true;
                isSprinting = false;
                return;
            }

            if (isSliding)
            {
                locomotionState = NatLocomotionState.Sliding;
                isRolling = false;
                isSprinting = false;
                return;
            }

            if (!isGrounded)
            {
                if (wantsToJump)
                    locomotionState = NatLocomotionState.Jumping;
                else
                    locomotionState = NatLocomotionState.Falling;

                isRolling = false;
                isSprinting = false;
                return;
            }

            locomotionState = NatLocomotionState.Grounded;
            isRolling = false;
            isSprinting = wantsToSprint && stance == NatStance.Standing && locomotionMode == NatLocomotionMode.Free;
        }

        private void ResolvePermissions(NatAnimatorBridge animatorBridge)
        {
            canMove = true;
            canRotate = true;
            useRootMotion = false;
            ignoreGroundSnap = false;

            if (isDead)
            {
                canMove = false;
                canRotate = false;
                useRootMotion = false;
                ignoreGroundSnap = true;
                return;
            }

            if (animatorBridge != null)
            {
                if (animatorBridge.LockMovement)
                    canMove = false;

                if (animatorBridge.LockRotation)
                    canRotate = false;

                if (animatorBridge.CustomAction)
                {
                    useRootMotion = true;
                    ignoreGroundSnap = true;
                }

                if (animatorBridge.IsRolling)
                {
                    useRootMotion = true;
                }
            }

            if (locomotionState == NatLocomotionState.Rolling)
            {
                useRootMotion = true;
            }

            if (locomotionState == NatLocomotionState.Falling || locomotionState == NatLocomotionState.Jumping)
            {
                ignoreGroundSnap = true;
            }
        }
    }
}