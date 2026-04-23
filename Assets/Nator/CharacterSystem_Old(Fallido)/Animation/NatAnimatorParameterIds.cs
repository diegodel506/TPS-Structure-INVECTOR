using UnityEngine;

namespace Nator.CharacterSystem.Animation
{
    public static class NatAnimatorParameterIds
    {
        public static readonly int InputHorizontal = Animator.StringToHash("InputHorizontal");
        public static readonly int InputVertical = Animator.StringToHash("InputVertical");
        public static readonly int InputDirection = Animator.StringToHash("InputDirection");
        public static readonly int InputMagnitude = Animator.StringToHash("InputMagnitude");
        public static readonly int RotationMagnitude = Animator.StringToHash("RotationMagnitude");

        public static readonly int ActionState = Animator.StringToHash("ActionState");
        public static readonly int ResetState = Animator.StringToHash("ResetState");

        public static readonly int IsDead = Animator.StringToHash("isDead");
        public static readonly int IsGrounded = Animator.StringToHash("IsGrounded");
        public static readonly int IsCrouching = Animator.StringToHash("IsCrouching");
        public static readonly int IsStrafing = Animator.StringToHash("IsStrafing");
        public static readonly int IsSprinting = Animator.StringToHash("IsSprinting");
        public static readonly int IsSliding = Animator.StringToHash("IsSliding");

        public static readonly int GroundDistance = Animator.StringToHash("GroundDistance");
        public static readonly int GroundAngle = Animator.StringToHash("GroundAngle");
        public static readonly int VerticalVelocity = Animator.StringToHash("VerticalVelocity");

        public static readonly int MoveSetId = Animator.StringToHash("MoveSet_ID");
        public static readonly int IdleRandom = Animator.StringToHash("IdleRandom");
        public static readonly int IdleRandomTrigger = Animator.StringToHash("IdleRandomTrigger");
    }
}