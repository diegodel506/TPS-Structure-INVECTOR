namespace Nator.CharacterSystem.Animation
{
    [System.Serializable]
    public struct NatAnimatorFrameData
    {
        public float InputHorizontal;
        public float InputVertical;
        public float InputDirection;
        public float InputMagnitude;
        public float RotationMagnitude;

        public int ActionState;

        public bool IsDead;
        public bool IsGrounded;
        public bool IsCrouching;
        public bool IsStrafing;
        public bool IsSprinting;
        public bool IsSliding;

        public float GroundDistance;
        public float GroundAngle;
        public float VerticalVelocity;

        public float MoveSetId;
        public int IdleRandom;

        public bool TriggerIdleRandom;
        public bool TriggerResetState;
    }
}