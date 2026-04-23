namespace Nator.CharacterSystem.Animation
{
    [System.Serializable]
    public struct NatAnimatorTagInfo
    {
        public bool IsRolling;
        public bool LockMovement;
        public bool LockRotation;
        public bool CustomAction;
        public bool Airborne;
        public bool Dead;
        public bool IgnoreIK;
        public bool IgnoreHeadtrack;
        public bool ClimbLadder;
        public bool LadderSlideDown;
    }
}