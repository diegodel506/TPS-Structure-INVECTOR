using UnityEngine;

[System.Serializable]
public class FootIKLeg
{
    public AvatarIKGoal Goal;
    public AvatarIKHint Hint;

    public Transform UpperLeg;
    public Transform LowerLeg;
    public Transform Foot;

    public bool IsLeft;

    public Vector3 TargetPosition;
    public Quaternion TargetRotation = Quaternion.identity;
    public Vector3 SmoothedPosition;
    public Quaternion SmoothedRotation = Quaternion.identity;

    public Vector3 GroundPoint;
    public Vector3 GroundNormal = Vector3.up;
    public bool HasGroundHit;
    public float TargetWeight;

    public Vector3 LastRayOrigin;
    public Vector3 LastHintPosition;
    public Vector3 LastAppliedFootPosition;

    public float UpperLegLength { get; private set; }
    public float LowerLegLength { get; private set; }
    public float TotalLegLength => UpperLegLength + LowerLegLength;

    public FootIKLeg(
        AvatarIKGoal goal,
        AvatarIKHint hint,
        Transform upperLeg,
        Transform lowerLeg,
        Transform foot,
        bool isLeft)
    {
        Goal = goal;
        Hint = hint;
        UpperLeg = upperLeg;
        LowerLeg = lowerLeg;
        Foot = foot;
        IsLeft = isLeft;

        if (UpperLeg && LowerLeg)
            UpperLegLength = Vector3.Distance(UpperLeg.position, LowerLeg.position);

        if (LowerLeg && Foot)
            LowerLegLength = Vector3.Distance(LowerLeg.position, Foot.position);
    }
}