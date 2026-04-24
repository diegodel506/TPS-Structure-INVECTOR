using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class FootIKController : MonoBehaviour
{
    #region Inspector

    [Header("Core References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform rootReference;

    [Header("Optional Character State Source")]
    [Tooltip("Si está activo, este script intentará usar flags manuales o externas para decidir cuándo habilitar IK.")]
    [SerializeField] private bool useExternalStateFlags = true;

    [Header("External State Flags")]
    [SerializeField] private bool isGrounded = true;
    [SerializeField] private bool isJumping = false;
    [SerializeField] private bool isRolling = false;
    [SerializeField] private bool isCustomAction = false;

    [Header("Activation Rules")]
    [SerializeField] private bool disableIKWhenAirborne = true;
    [SerializeField] private bool disableIKWhenJumping = true;
    [SerializeField] private bool disableIKWhenRolling = true;
    [SerializeField] private bool disableIKWhenCustomAction = false;
    [SerializeField] private float globalIKBlendSpeed = 10f;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField] private float raycastStartHeight = 0.65f;
    [SerializeField] private float raycastLength = 1.6f;
    [SerializeField] private float footHeightOffset = 0.03f;
    [SerializeField] private float footSphereCastRadius = 0.06f;
    [SerializeField] private bool useSphereCast = true;

    [Header("IK Weights")]
    [Range(0f, 1f)] [SerializeField] private float maxFootPositionWeight = 1f;
    [Range(0f, 1f)] [SerializeField] private float maxFootRotationWeight = 1f;
    [Range(0f, 1f)] [SerializeField] private float maxHintWeight = 1f;
    [Range(0f, 1f)] [SerializeField] private float maxPelvisWeight = 0.35f;

    [Header("Per-Foot Curves (optional)")]
    [Tooltip("Si está activo, leerá curvas del Animator con estos nombres.")]
    [SerializeField] private bool useAnimatorCurves = false;
    [SerializeField] private string leftFootCurveName = "LeftFootIK";
    [SerializeField] private string rightFootCurveName = "RightFootIK";

    [Header("Smoothing")]
    [SerializeField] private float footPositionLerpSpeed = 18f;
    [SerializeField] private float footRotationLerpSpeed = 18f;
    [SerializeField] private float pelvisLerpSpeed = 10f;

    [Header("Pelvis")]
    [SerializeField] private bool enablePelvisAdjustment = true;
    [SerializeField] private float maxPelvisUpOffset = 0.25f;
    [SerializeField] private float maxPelvisDownOffset = 0.35f;

    [Header("Leg Limits")]
    [SerializeField] private float maxLegStretchMultiplier = 1.08f;
    [SerializeField] private float maxFootLiftFromAnimatedPose = 0.45f;
    [SerializeField] private float maxFootDropFromAnimatedPose = 0.60f;

    [Header("Knee Hint")]
    [SerializeField] private float kneeForwardDistance = 0.35f;
    [SerializeField] private float kneeOutwardDistance = 0.14f;
    [SerializeField] private float kneeHintLerpSpeed = 14f;

    [Header("Foot Rotation")]
    [SerializeField] private bool alignFootToGround = true;
    [SerializeField] private float footForwardBlend = 0.85f;

    [Header("Debug")]
    [SerializeField] private bool drawDebug = true;

    #endregion

    #region Internal Types

    [System.Serializable]
    private class LegData
    {
        public AvatarIKGoal ikGoal;
        public AvatarIKHint ikHint;
        public bool isLeft;

        public Transform upperLeg;
        public Transform lowerLeg;
        public Transform foot;

        public float upperLegLength;
        public float lowerLegLength;
        public float totalLegLength;

        public bool hasGroundHit;
        public RaycastHit groundHit;

        public Vector3 animatedFootPosition;
        public Quaternion animatedFootRotation;

        public Vector3 targetFootPosition;
        public Quaternion targetFootRotation = Quaternion.identity;

        public Vector3 smoothedFootPosition;
        public Quaternion smoothedFootRotation = Quaternion.identity;

        public Vector3 targetHintPosition;
        public Vector3 smoothedHintPosition;

        public float curveWeight = 1f;
        public float finalWeight = 0f;

        public Vector3 lastRayOrigin;
    }

    #endregion

    #region Fields

    private LegData leftLeg;
    private LegData rightLeg;

    private float globalWeight;
    private float pelvisOffsetY;

    private bool initialized;

    #endregion

    #region Unity

    private void Reset()
    {
        animator = GetComponent<Animator>();
        rootReference = transform;
    }

    private void Awake()
    {
        Initialize();
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (!enabled)
            return;

        if (!initialized)
            Initialize();

        if (!initialized || animator == null)
            return;

        float deltaTime = Application.isPlaying ? Time.deltaTime : 0.016f;

        CacheAnimatedPose();
        UpdateCurveWeights();
        UpdateGlobalIKWeight(deltaTime);
        SolveLeg(leftLeg, deltaTime);
        SolveLeg(rightLeg, deltaTime);
        UpdatePelvis(deltaTime);
        ApplyLegIK(leftLeg);
        ApplyLegIK(rightLeg);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebug)
            return;

        DrawLegDebug(leftLeg, Color.green);
        DrawLegDebug(rightLeg, Color.yellow);
    }

    #endregion

    #region Initialization

    private void Initialize()
    {
        if (initialized)
            return;

        if (animator == null)
            animator = GetComponent<Animator>();

        if (rootReference == null)
            rootReference = transform;

        if (animator == null)
        {
            Debug.LogError("[FootIKControllerV2] Animator no encontrado.", this);
            enabled = false;
            return;
        }

        if (!animator.isHuman)
        {
            Debug.LogError("[FootIKControllerV2] Este sistema espera un rig Humanoid.", this);
            enabled = false;
            return;
        }

        leftLeg = CreateLeg(
            AvatarIKGoal.LeftFoot,
            AvatarIKHint.LeftKnee,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.LeftFoot,
            true);

        rightLeg = CreateLeg(
            AvatarIKGoal.RightFoot,
            AvatarIKHint.RightKnee,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.RightFoot,
            false);

        if (leftLeg == null || rightLeg == null)
        {
            enabled = false;
            return;
        }

        initialized = true;
    }

    private LegData CreateLeg(
        AvatarIKGoal goal,
        AvatarIKHint hint,
        HumanBodyBones upperLegBone,
        HumanBodyBones lowerLegBone,
        HumanBodyBones footBone,
        bool isLeft)
    {
        Transform upperLeg = animator.GetBoneTransform(upperLegBone);
        Transform lowerLeg = animator.GetBoneTransform(lowerLegBone);
        Transform foot = animator.GetBoneTransform(footBone);

        if (upperLeg == null || lowerLeg == null || foot == null)
        {
            Debug.LogError($"[FootIKControllerV2] Faltan huesos para la pierna {(isLeft ? "izquierda" : "derecha")}.", this);
            return null;
        }

        LegData leg = new LegData
        {
            ikGoal = goal,
            ikHint = hint,
            isLeft = isLeft,
            upperLeg = upperLeg,
            lowerLeg = lowerLeg,
            foot = foot
        };

        leg.upperLegLength = Vector3.Distance(upperLeg.position, lowerLeg.position);
        leg.lowerLegLength = Vector3.Distance(lowerLeg.position, foot.position);
        leg.totalLegLength = leg.upperLegLength + leg.lowerLegLength;

        leg.smoothedFootPosition = foot.position;
        leg.smoothedFootRotation = foot.rotation;
        leg.smoothedHintPosition = lowerLeg.position + transform.forward * 0.25f;

        return leg;
    }

    #endregion

    #region State / Weights

    private void CacheAnimatedPose()
    {
        CacheLegAnimatedPose(leftLeg);
        CacheLegAnimatedPose(rightLeg);
    }

    private void CacheLegAnimatedPose(LegData leg)
    {
        if (leg == null || leg.foot == null)
            return;

        leg.animatedFootPosition = leg.foot.position;
        leg.animatedFootRotation = leg.foot.rotation;
    }

    private void UpdateCurveWeights()
    {
        if (!useAnimatorCurves || animator == null)
        {
            leftLeg.curveWeight = 1f;
            rightLeg.curveWeight = 1f;
            return;
        }

        leftLeg.curveWeight = SafeGetAnimatorCurve(leftFootCurveName);
        rightLeg.curveWeight = SafeGetAnimatorCurve(rightFootCurveName);
    }

    private float SafeGetAnimatorCurve(string curveName)
    {
        if (string.IsNullOrWhiteSpace(curveName))
            return 1f;

        try
        {
            return Mathf.Clamp01(animator.GetFloat(curveName));
        }
        catch
        {
            return 1f;
        }
    }

    private void UpdateGlobalIKWeight(float deltaTime)
    {
        bool shouldEnable = ShouldEnableIK();
        float target = shouldEnable ? 1f : 0f;
        globalWeight = Mathf.MoveTowards(globalWeight, target, deltaTime * globalIKBlendSpeed);
    }

    private bool ShouldEnableIK()
    {
        if (!useExternalStateFlags)
            return true;

        if (disableIKWhenAirborne && !isGrounded)
            return false;

        if (disableIKWhenJumping && isJumping)
            return false;

        if (disableIKWhenRolling && isRolling)
            return false;

        if (disableIKWhenCustomAction && isCustomAction)
            return false;

        return true;
    }

    #endregion

    #region Solve

    private void SolveLeg(LegData leg, float deltaTime)
    {
        if (leg == null)
            return;

        DetectGround(leg);

        float targetWeight = leg.hasGroundHit ? leg.curveWeight * globalWeight : 0f;
        leg.finalWeight = Mathf.MoveTowards(leg.finalWeight, targetWeight, deltaTime * globalIKBlendSpeed * 2f);

        if (!leg.hasGroundHit)
        {
            leg.targetFootPosition = leg.animatedFootPosition;
            leg.targetFootRotation = leg.animatedFootRotation;
            leg.targetHintPosition = GetDefaultHintPosition(leg);
        }
        else
        {
            ComputeFootTarget(leg);
            ComputeHintTarget(leg);
        }

        leg.smoothedFootPosition = Vector3.Lerp(
            leg.smoothedFootPosition,
            leg.targetFootPosition,
            deltaTime * footPositionLerpSpeed);

        leg.smoothedFootRotation = Quaternion.Slerp(
            leg.smoothedFootRotation,
            leg.targetFootRotation,
            deltaTime * footRotationLerpSpeed);

        leg.smoothedHintPosition = Vector3.Lerp(
            leg.smoothedHintPosition,
            leg.targetHintPosition,
            deltaTime * kneeHintLerpSpeed);
    }

    private void DetectGround(LegData leg)
    {
        Vector3 origin = leg.animatedFootPosition + rootReference.up * raycastStartHeight;
        leg.lastRayOrigin = origin;

        bool hitFound;

        if (useSphereCast)
        {
            hitFound = Physics.SphereCast(
                origin,
                footSphereCastRadius,
                -rootReference.up,
                out leg.groundHit,
                raycastLength,
                groundLayers,
                QueryTriggerInteraction.Ignore);
        }
        else
        {
            hitFound = Physics.Raycast(
                origin,
                -rootReference.up,
                out leg.groundHit,
                raycastLength,
                groundLayers,
                QueryTriggerInteraction.Ignore);
        }

        leg.hasGroundHit = hitFound;
    }

    private void ComputeFootTarget(LegData leg)
    {
        Vector3 desiredPos = leg.groundHit.point + leg.groundHit.normal * footHeightOffset;

        float verticalDelta = Vector3.Dot(desiredPos - leg.animatedFootPosition, rootReference.up);
        verticalDelta = Mathf.Clamp(verticalDelta, -maxFootDropFromAnimatedPose, maxFootLiftFromAnimatedPose);

        desiredPos = leg.animatedFootPosition + rootReference.up * verticalDelta;

        Vector3 hipPos = leg.upperLeg.position;
        Vector3 fromHip = desiredPos - hipPos;
        float maxDistance = leg.totalLegLength * maxLegStretchMultiplier;

        if (fromHip.magnitude > maxDistance)
        {
            desiredPos = hipPos + fromHip.normalized * maxDistance;
        }

        leg.targetFootPosition = desiredPos;

        if (!alignFootToGround)
        {
            leg.targetFootRotation = leg.animatedFootRotation;
            return;
        }

        Vector3 animatedForward = leg.animatedFootRotation * Vector3.forward;
        Vector3 rootForward = rootReference.forward;
        Vector3 blendedForward = Vector3.Slerp(animatedForward, rootForward, footForwardBlend).normalized;

        Vector3 projectedForward = Vector3.ProjectOnPlane(blendedForward, leg.groundHit.normal).normalized;
        if (projectedForward.sqrMagnitude < 0.0001f)
            projectedForward = Vector3.ProjectOnPlane(rootForward, leg.groundHit.normal).normalized;

        if (projectedForward.sqrMagnitude < 0.0001f)
            projectedForward = Vector3.forward;

        leg.targetFootRotation = Quaternion.LookRotation(projectedForward, leg.groundHit.normal);
    }

    private void ComputeHintTarget(LegData leg)
    {
        Vector3 hipPos = leg.upperLeg.position;
        Vector3 footPos = leg.targetFootPosition;
        Vector3 legDirection = (footPos - hipPos).normalized;

        Vector3 forward = Vector3.ProjectOnPlane(rootReference.forward, rootReference.up).normalized;
        if (forward.sqrMagnitude < 0.0001f)
            forward = transform.forward;

        Vector3 outward = leg.isLeft ? -rootReference.right : rootReference.right;

        Vector3 projectedForward = Vector3.ProjectOnPlane(forward, legDirection).normalized;
        if (projectedForward.sqrMagnitude < 0.0001f)
            projectedForward = Vector3.ProjectOnPlane(rootReference.forward, legDirection).normalized;

        if (projectedForward.sqrMagnitude < 0.0001f)
            projectedForward = outward;

        Vector3 hintPos = hipPos
                        + projectedForward * kneeForwardDistance
                        + outward * kneeOutwardDistance;

        leg.targetHintPosition = hintPos;
    }

    private Vector3 GetDefaultHintPosition(LegData leg)
    {
        Vector3 outward = leg.isLeft ? -rootReference.right : rootReference.right;
        return leg.lowerLeg.position + rootReference.forward * kneeForwardDistance + outward * kneeOutwardDistance;
    }

    #endregion

    #region Pelvis

    private void UpdatePelvis(float deltaTime)
    {
        if (!enablePelvisAdjustment || animator == null)
            return;

        float leftOffset = GetLegVerticalOffset(leftLeg);
        float rightOffset = GetLegVerticalOffset(rightLeg);

        float targetOffset = Mathf.Min(leftOffset, rightOffset);
        targetOffset *= maxPelvisWeight;
        targetOffset = Mathf.Clamp(targetOffset, -maxPelvisDownOffset, maxPelvisUpOffset);

        pelvisOffsetY = Mathf.Lerp(pelvisOffsetY, targetOffset, deltaTime * pelvisLerpSpeed);

        animator.bodyPosition += rootReference.up * pelvisOffsetY * globalWeight;
    }

    private float GetLegVerticalOffset(LegData leg)
    {
        if (leg == null || !leg.hasGroundHit)
            return 0f;

        return Vector3.Dot(leg.smoothedFootPosition - leg.animatedFootPosition, rootReference.up);
    }

    #endregion

    #region Apply IK

    private void ApplyLegIK(LegData leg)
    {
        if (leg == null)
            return;

        float posWeight = maxFootPositionWeight * leg.finalWeight;
        float rotWeight = maxFootRotationWeight * leg.finalWeight;
        float hintWeight = maxHintWeight * leg.finalWeight;

        animator.SetIKPositionWeight(leg.ikGoal, posWeight);
        animator.SetIKRotationWeight(leg.ikGoal, rotWeight);
        animator.SetIKHintPositionWeight(leg.ikHint, hintWeight);

        animator.SetIKPosition(leg.ikGoal, leg.smoothedFootPosition);
        animator.SetIKRotation(leg.ikGoal, leg.smoothedFootRotation);
        animator.SetIKHintPosition(leg.ikHint, leg.smoothedHintPosition);
    }

    #endregion

    #region Public API

    public void SetGrounded(bool value)
    {
        isGrounded = value;
    }

    public void SetJumping(bool value)
    {
        isJumping = value;
    }

    public void SetRolling(bool value)
    {
        isRolling = value;
    }

    public void SetCustomAction(bool value)
    {
        isCustomAction = value;
    }

    public void SetExternalStates(bool grounded, bool jumping, bool rolling, bool customAction = false)
    {
        isGrounded = grounded;
        isJumping = jumping;
        isRolling = rolling;
        isCustomAction = customAction;
    }

    public void ForceIKEnabled(bool enabledState)
    {
        useExternalStateFlags = !enabledState;
        globalWeight = enabledState ? 1f : 0f;
    }

    #endregion

    #region Debug

    private void DrawLegDebug(LegData leg, Color color)
    {
        if (leg == null || leg.foot == null)
            return;

        Gizmos.color = color;
        Gizmos.DrawLine(leg.lastRayOrigin, leg.lastRayOrigin - rootReference.up * raycastLength);

        if (leg.hasGroundHit)
        {
            Gizmos.DrawSphere(leg.groundHit.point, 0.025f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(leg.groundHit.point, leg.groundHit.point + leg.groundHit.normal * 0.2f);

            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(leg.smoothedHintPosition, 0.03f);
            Gizmos.DrawLine(leg.upperLeg.position, leg.smoothedHintPosition);
            Gizmos.DrawLine(leg.smoothedHintPosition, leg.smoothedFootPosition);

            Gizmos.color = Color.white;
            Gizmos.DrawSphere(leg.smoothedFootPosition, 0.025f);
        }
    }

    #endregion
}