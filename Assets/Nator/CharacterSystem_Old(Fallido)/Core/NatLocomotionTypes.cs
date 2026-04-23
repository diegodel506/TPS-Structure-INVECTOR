namespace Nator.CharacterSystem.Core
{
    public enum NatLocomotionMode
    {
        Free = 0,
        Strafe = 1
    }

    public enum NatLocomotionState
    {
        Grounded = 0,
        Jumping = 1,
        Falling = 2,
        Landing = 3,
        Rolling = 4,
        Sliding = 5,
        CustomAction = 6,
        Dead = 7
    }

    public enum NatStance
    {
        Standing = 0,
        Crouching = 1
    }
}
