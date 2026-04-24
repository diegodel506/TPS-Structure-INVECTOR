using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif


/// <summary>
/// Makes a field read-only in the Unity Inspector.
/// Runtime-safe: the drawer only exists inside UNITY_EDITOR.
/// </summary>
public class ReadOnlyAttribute : PropertyAttribute { }

/// <summary>
/// Enables a field in the Inspector only when a boolean field matches the expected value.
/// </summary>
public class EnableIfAttribute : PropertyAttribute
{
    public readonly string ConditionFieldName;
    public readonly bool ExpectedValue;

    public EnableIfAttribute(string conditionFieldName, bool expectedValue = true)
    {
        ConditionFieldName = conditionFieldName;
        ExpectedValue = expectedValue;
    }
}

namespace Nator.FootIK
{
    [AddComponentMenu("NatorTools/Foot IK/Nat Foot Placement")]
    public class NatFootPlacement : MonoBehaviour
    {
        #region Runtime State

        private bool Started = false;

        [HideInInspector] public bool BlockBodyPositioning;

        private Animator anim;

        private RaycastHit LeftHitPlaceBase;
        private RaycastHit RightHitPlaceBase;
        private RaycastHit HitGroundBodyPlacement;

        private Transform RightFootPlaceBase;
        private Transform LeftFootPlaceBase;

        private Transform LeftFoot;
        private Transform LeftFootBase_UP;
        private Transform RightFoot;
        private Transform RightFootBase_UP;

        private Vector3 SmothedLeftFootPosition;
        private Vector3 SmothedRightFootPosition;

        private Quaternion SmothedLeftFootRotation;
        private Quaternion SmothedRightFootRotation;

        private float TransitionIKtoFKWeight;

        private float LeftFootHeight;
        private float RightFootHeight;

        private float AnimationLeftFootPositionY;
        private float AnimationRightFootPositionY;

        private bool LeftHit;
        private bool RightHit;

        [HideInInspector] public float LeftFootHeightFromGround;
        [HideInInspector] public float RightFootHeightFromGround;

        [HideInInspector] public float LeftFootRotationWeight;
        [HideInInspector] public float RightFootRotationWeight;

        [HideInInspector] public float LastBodyPositionY;
        [HideInInspector] public Vector3 NewAnimationBodyPosition;
        [HideInInspector] public float Animation_Y_BodyPosition;

        private float BodyPositionOffset;
        private float GroundAngle;

        private readonly float MinBodyHeightPosition = 0.005f;
        private readonly float MaxBodyPositionHeight = 1f;

        #endregion

        #region Inspector - Foot Placement

        [Header("FOOT PLACEMENT")]
        public bool EnableFootPlacement = true;
        public bool AdvancedMode = false;

        [Header("Raycasts Settings")]
        [Space]
        public LayerMask GroundLayers;

        [EnableIf(nameof(AdvancedMode))] public float RaycastMaxDistance = 2f;
        [EnableIf(nameof(AdvancedMode))] public float RaycastHeight = 1f;
        [EnableIf(nameof(AdvancedMode))] public float radius = 0.1f;

        [Header("Foot Placing System")]
        [Space]
        public float FootHeight = 0.1f;
        [EnableIf(nameof(AdvancedMode))] public float MaxStepHeight = 0.6f;

        public bool UseDynamicFootPlacing = true;
        [EnableIf(nameof(UseDynamicFootPlacing), false)] public string LeftFootHeightCurveName = "LeftFootHeight";
        [EnableIf(nameof(UseDynamicFootPlacing), false)] public string RightFootHeightCurveName = "RightFootHeight";

        [EnableIf(nameof(AdvancedMode))] public bool SmoothIKTransition = true;
        public float SpeedInTransition = 6f;
        public float SpeedOutTransition = 6f;
        [EnableIf(nameof(AdvancedMode))] public float FootHeightMultiplier = 0.6f;

        [Range(0, 1)]
        public float GlobalWeight = 1f;

        #endregion

        #region Inspector - Dynamic Body Placement

        [Header("DYNAMIC BODY PLACEMENT")]
        [Space]

        [Tooltip("When enabled, it will change your character's position according to the terrain.")]
        public bool EnableDynamicBodyPlacing = true;

        [EnableIf(nameof(EnableDynamicBodyPlacing))]
        public float UpAndDownForce = 10f;

        [EnableIf(nameof(AdvancedMode))]
        public float MaxBodyCrouchHeight = 0.65f;

        [Tooltip("If true, it will only calculate the ideal body position, but it will not affect the body position of the character. Use GetCalculatedAnimatorCenterOfMass() to get the calculated position.")]
        [EnableIf(nameof(AdvancedMode))]
        public bool JustCalculateBodyPosition = false;

        [Space]

        [Tooltip("This will keep your character grounded.")]
        public bool KeepCharacterOnGround = false;

        [EnableIf(nameof(KeepCharacterOnGround))] public float RaycastDistanceToGround = 1.2f;
        [EnableIf(nameof(KeepCharacterOnGround))] public float BodyHeightPosition = 0.01f;
        [EnableIf(nameof(KeepCharacterOnGround))] public float Force = 10f;

        #endregion

        #region Inspector - Ground Filtering

        [Header("Ground Filtering")]
        public bool FilterLowerGroundHits = true;
        public float MaxGroundDropFromCharacter = 0.35f;
        public float MaxBodyDropFromCharacter = 0.2f;

        #endregion

        #region Inspector - Ground Check

        [Header("Ground Check")]
        [ReadOnly]
        [Space]
        public bool TheresGroundBelow;

        [EnableIf(nameof(AdvancedMode))]
        public float GroundCheckRadius = 0.1f;

        #endregion

        #region Unity

        private void Start()
        {
            LeftFoot = null;
            RightFoot = null;

            Invoke(nameof(StartFootPlacement), 0.1f);
            GetFootPlacementDependencies();
            Invoke(nameof(GetFootPlacementDependencies), 0.01f);
        }

        private void LateUpdate()
        {
            if (!Started)
                return;
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (Vector3.Angle(transform.up, Vector3.up) > 30f && EnableFootPlacement)
            {
                SmoothIKTransition = false;
            }

            if (layerIndex != 0)
                return;

            FootPlacementPositions();

            Animation_Y_BodyPosition = anim.bodyPosition.y;

            if (TransitionIKtoFKWeight < 0.1f || GlobalWeight < 0.01f)
                return;

            if (!EnableFootPlacement)
                return;

            // Posición de los pies antes de la corrección IK
            AnimationLeftFootPositionY = transform.position.y - (LeftFoot.position.y - FootHeight);
            AnimationRightFootPositionY = transform.position.y - (RightFoot.position.y - FootHeight);

            AnimationLeftFootPositionY = Mathf.Abs(AnimationLeftFootPositionY);
            AnimationRightFootPositionY = Mathf.Abs(AnimationRightFootPositionY);

            AnimationLeftFootPositionY = Mathf.Clamp(AnimationLeftFootPositionY, 0f, 1f);
            AnimationRightFootPositionY = Mathf.Clamp(AnimationRightFootPositionY, 0f, 1f);

            if (Vector3.Angle(transform.up, Vector3.up) < 40f)
            {
                BodyPlacement();
            }

            if (LeftHit && LeftHitPlaceBase.collider != null && LeftHitPlaceBase.point.y < transform.position.y + RaycastHeight)
            {
                Vector3 pos = new Vector3(LeftFoot.position.x, SmothedLeftFootPosition.y, LeftFoot.position.z);

                anim.SetIKPosition(AvatarIKGoal.LeftFoot, pos);
                anim.SetIKPositionWeight(AvatarIKGoal.LeftFoot, GlobalWeight * TransitionIKtoFKWeight);

                anim.SetIKRotationWeight(AvatarIKGoal.LeftFoot, GlobalWeight * TransitionIKtoFKWeight * LeftFootRotationWeight);
                anim.SetIKRotation(AvatarIKGoal.LeftFoot, SmothedLeftFootRotation * anim.GetIKRotation(AvatarIKGoal.LeftFoot));
            }

            if (RightHit && RightHitPlaceBase.collider != null && RightHitPlaceBase.point.y < transform.position.y + RaycastHeight)
            {
                Vector3 pos = new Vector3(RightFoot.position.x, SmothedRightFootPosition.y, RightFoot.position.z);

                anim.SetIKPosition(AvatarIKGoal.RightFoot, pos);
                anim.SetIKPositionWeight(AvatarIKGoal.RightFoot, GlobalWeight * TransitionIKtoFKWeight);

                anim.SetIKRotationWeight(AvatarIKGoal.RightFoot, GlobalWeight * TransitionIKtoFKWeight * RightFootRotationWeight);
                anim.SetIKRotation(AvatarIKGoal.RightFoot, SmothedRightFootRotation * anim.GetIKRotation(AvatarIKGoal.RightFoot));
            }
        }

        #endregion

        #region Setup

        public void StartFootPlacement()
        {
            if (LeftFoot == null || RightFoot == null || LeftFootPlaceBase == null || RightFootPlaceBase == null)
                return;

            Started = true;
            LeftFootPlaceBase.position = LeftFoot.position;
            RightFootPlaceBase.position = RightFoot.position;
        }

        private void GetFootPlacementDependencies()
        {
            if (GroundLayers.value == 0)
                GroundLayers = LayerMask.GetMask("Default");

            if (LeftFoot != null && RightFoot != null)
                return;

            anim = GetComponent<Animator>();
            if (anim == null)
                return;

            LeftFoot = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            RightFoot = anim.GetBoneTransform(HumanBodyBones.RightFoot);

            if (LeftFoot == null || RightFoot == null)
                return;

            SmothedLeftFootPosition = LeftFoot.position - transform.forward * 0.1f;
            SmothedRightFootPosition = RightFoot.position - transform.forward * 0.1f;
            SmothedLeftFootRotation = LeftFoot.rotation;
            SmothedRightFootRotation = RightFoot.rotation;

            LeftFootPlaceBase = new GameObject("Left Foot Position").transform;
            RightFootPlaceBase = new GameObject("Right Foot Position").transform;
            LeftFootPlaceBase.position = LeftFoot.position;
            RightFootPlaceBase.position = RightFoot.position;
            LeftFootPlaceBase.gameObject.hideFlags = HideFlags.HideAndDontSave;
            RightFootPlaceBase.gameObject.hideFlags = HideFlags.HideAndDontSave;

            LeftFootBase_UP = new GameObject("Left Foot BASE UP").transform;
            RightFootBase_UP = new GameObject("Right Foot BASE UP").transform;
            LeftFootBase_UP.position = LeftFoot.position;
            RightFootBase_UP.position = RightFoot.position;
            LeftFootBase_UP.SetParent(LeftFoot);
            RightFootBase_UP.SetParent(RightFoot);
            LeftFootBase_UP.gameObject.hideFlags = HideFlags.HideAndDontSave;
            RightFootBase_UP.gameObject.hideFlags = HideFlags.HideAndDontSave;
        }

        #endregion

        #region Ground Filtering

        private bool IsValidFootHit(RaycastHit hit)
        {
            if (hit.collider == null)
                return false;

            if (!FilterLowerGroundHits)
                return true;

            float minAllowedY = transform.position.y - MaxGroundDropFromCharacter;
            return hit.point.y >= minAllowedY;
        }

        #endregion

        #region Foot Placement

        private void FootPlacementPositions()
        {
            if (RightFoot == null || LeftFoot == null || LeftFootBase_UP == null || RightFootBase_UP == null)
                return;

            // Altura extra de pies desde curvas o desde la animación actual
            if (UseDynamicFootPlacing)
            {
                LeftFootHeightFromGround = FootHeightMultiplier * AnimationLeftFootPositionY;
                RightFootHeightFromGround = FootHeightMultiplier * AnimationRightFootPositionY;
            }
            else
            {
                LeftFootHeightFromGround = Mathf.Lerp(
                    LeftFootHeightFromGround,
                    anim.GetFloat(LeftFootHeightCurveName) / 2f,
                    20f * Time.deltaTime
                );

                RightFootHeightFromGround = Mathf.Lerp(
                    RightFootHeightFromGround,
                    anim.GetFloat(RightFootHeightCurveName) / 2f,
                    20f * Time.deltaTime
                );
            }

            // Raycasts filtrados de pies
            RaycastHit leftRawHit;
            RaycastHit rightRawHit;

            bool leftHasRawHit = Physics.SphereCast(
                LeftFoot.position + transform.up * RaycastHeight + LeftFootBase_UP.forward * 0.12f,
                radius,
                -transform.up,
                out leftRawHit,
                RaycastMaxDistance,
                GroundLayers,
                QueryTriggerInteraction.Ignore
            );

            bool rightHasRawHit = Physics.SphereCast(
                RightFoot.position + transform.up * RaycastHeight + RightFootBase_UP.forward * 0.12f,
                radius,
                -transform.up,
                out rightRawHit,
                RaycastMaxDistance,
                GroundLayers,
                QueryTriggerInteraction.Ignore
            );

            LeftHitPlaceBase = default;
            RightHitPlaceBase = default;

            if (leftHasRawHit && IsValidFootHit(leftRawHit))
                LeftHitPlaceBase = leftRawHit;

            if (rightHasRawHit && IsValidFootHit(rightRawHit))
                RightHitPlaceBase = rightRawHit;

            // Validación de hits
            if (LeftHitPlaceBase.collider != null)
            {
                LeftFootPlaceBase.position = LeftHitPlaceBase.point;
                LeftFootPlaceBase.rotation = Quaternion.FromToRotation(transform.up, LeftHitPlaceBase.normal) * transform.rotation;
                LeftHit = true;
            }
            else
            {
                LeftFootPlaceBase.position = LeftFoot.position;
                LeftHit = false;
            }

            if (RightHitPlaceBase.collider != null)
            {
                RightFootPlaceBase.position = RightHitPlaceBase.point;
                RightFootPlaceBase.rotation = Quaternion.FromToRotation(transform.up, RightHitPlaceBase.normal) * transform.rotation;
                RightHit = true;
            }
            else
            {
                RightFootPlaceBase.position = RightFoot.position;
                RightHit = false;
            }

            // Corrección de altura del pie según orientación
            LeftFootHeight = FootHeight - Vector3.SignedAngle(LeftFootBase_UP.up, transform.up, transform.right) / 500f;
            RightFootHeight = FootHeight - Vector3.SignedAngle(RightFootBase_UP.up, transform.up, transform.right) / 500f;
            LeftFootHeight = Mathf.Clamp(LeftFootHeight, -0.2f, 0.2f);
            RightFootHeight = Mathf.Clamp(RightFootHeight, -0.2f, 0.2f);

            // Suavizado de posiciones
            if (LeftHit)
            {
                if (LeftHitPlaceBase.point.y < transform.position.y + MaxStepHeight)
                {
                    SmothedLeftFootPosition = Vector3.Lerp(
                        SmothedLeftFootPosition,
                        LeftFootPlaceBase.position + LeftHitPlaceBase.normal * LeftFootHeight + transform.up * LeftFootHeightFromGround,
                        15f * Time.deltaTime
                    );
                }
                else
                {
                    SmothedLeftFootPosition = Vector3.Lerp(
                        SmothedLeftFootPosition,
                        transform.position + transform.up * FootHeight + transform.up * LeftFootHeightFromGround,
                        15f * Time.deltaTime
                    );
                }
            }
            else
            {
                SmothedLeftFootPosition = LeftFoot.position;
            }

            if (RightHit)
            {
                if (RightHitPlaceBase.point.y < transform.position.y + MaxStepHeight)
                {
                    SmothedRightFootPosition = Vector3.Lerp(
                        SmothedRightFootPosition,
                        RightFootPlaceBase.position + RightHitPlaceBase.normal * RightFootHeight + transform.up * RightFootHeightFromGround,
                        20f * Time.deltaTime
                    );
                }
                else
                {
                    SmothedRightFootPosition = Vector3.Lerp(
                        SmothedRightFootPosition,
                        transform.position + transform.up * FootHeight + transform.up * RightFootHeightFromGround,
                        20f * Time.deltaTime
                    );
                }
            }
            else
            {
                SmothedRightFootPosition = RightFoot.position;
            }

            // Rotación pie izquierdo
            if (LeftHitPlaceBase.collider != null)
            {
                Vector3 rotAxisLF = Vector3.Cross(Vector3.up, LeftHitPlaceBase.normal);
                float angleLF = Vector3.Angle(Vector3.up, LeftHitPlaceBase.normal);
                Quaternion rotLF = Quaternion.AngleAxis(angleLF * GlobalWeight, rotAxisLF);
                LeftFootPlaceBase.rotation = rotLF;
                SmothedLeftFootRotation = Quaternion.Lerp(SmothedLeftFootRotation, LeftFootPlaceBase.rotation, 20f * Time.deltaTime);
            }

            // Rotación pie derecho
            if (RightHitPlaceBase.collider != null)
            {
                Vector3 rotAxisRF = Vector3.Cross(Vector3.up, RightHitPlaceBase.normal);
                float angleRF = Vector3.Angle(Vector3.up, RightHitPlaceBase.normal);
                Quaternion rotRF = Quaternion.AngleAxis(angleRF * GlobalWeight, rotAxisRF);
                RightFootPlaceBase.rotation = rotRF;
                SmothedRightFootRotation = Quaternion.Lerp(SmothedRightFootRotation, RightFootPlaceBase.rotation, 20f * Time.deltaTime);
            }

            // Peso de rotación
            LeftFootRotationWeight = Mathf.Lerp(
                LeftFootRotationWeight,
                LeftFootHeightFromGround < 0.3f ? 1f : 0f,
                (LeftFootHeightFromGround < 0.3f ? 8f : 1f) * Time.deltaTime
            );

            RightFootRotationWeight = Mathf.Lerp(
                RightFootRotationWeight,
                RightFootHeightFromGround < 0.3f ? 1f : 0f,
                (RightFootHeightFromGround < 0.3f ? 8f : 1f) * Time.deltaTime
            );

            // Transición IK/FK
            TransitionIKtoFKWeight = Mathf.Lerp(
                TransitionIKtoFKWeight,
                SmoothIKTransition ? 1f : 0f,
                (SmoothIKTransition ? SpeedInTransition : SpeedOutTransition) * Time.deltaTime
            );
        }

        #endregion

        #region Body Placement

        private void BodyPlacement()
        {
            Physics.SphereCast(
                transform.position + transform.up * RaycastDistanceToGround,
                GroundCheckRadius,
                -transform.up,
                out HitGroundBodyPlacement,
                RaycastDistanceToGround + 0.2f,
                GroundLayers,
                QueryTriggerInteraction.Ignore
            );

            TheresGroundBelow = HitGroundBodyPlacement.collider != null;
            GroundAngle = TheresGroundBelow ? Vector3.Angle(Vector3.up, HitGroundBodyPlacement.normal) : 0f;

            if (KeepCharacterOnGround)
            {
                BodyHeightPosition = Mathf.Clamp(BodyHeightPosition, MinBodyHeightPosition, MaxBodyPositionHeight);

                if (TheresGroundBelow)
                {
                    float groundPosition = HitGroundBodyPlacement.point.y - BodyHeightPosition;
                    float smoothedGroundNewPosition = Mathf.Lerp(transform.position.y, groundPosition, Force * Time.fixedDeltaTime);
                    Vector3 characterNewPosition = new Vector3(transform.position.x, smoothedGroundNewPosition, transform.position.z);
                    transform.position = characterNewPosition;
                }
            }

            if (TheresGroundBelow && !IsInvoking(nameof(DisableBlock)) && BlockBodyPositioning)
            {
                Invoke(nameof(DisableBlock), 0.5f);
            }

            if (EnableDynamicBodyPlacing && !BlockBodyPositioning)
            {
                bool leftValid = LeftHitPlaceBase.collider != null && IsValidFootHit(LeftHitPlaceBase);
                bool rightValid = RightHitPlaceBase.collider != null && IsValidFootHit(RightHitPlaceBase);

                if (!leftValid || !rightValid || LastBodyPositionY == 0f)
                {
                    LastBodyPositionY = Animation_Y_BodyPosition;
                    BodyPositionOffset = 0f;
                    NewAnimationBodyPosition = anim.bodyPosition;
                    return;
                }

                float leftOffsetBodyPosition =
                    LeftHitPlaceBase.point.y - transform.position.y - RightFootHeightFromGround / 2f;

                float rightOffsetBodyPosition =
                    RightHitPlaceBase.point.y - transform.position.y - LeftFootHeightFromGround / 2f;

                BodyPositionOffset = (leftOffsetBodyPosition < rightOffsetBodyPosition)
                    ? leftOffsetBodyPosition
                    : rightOffsetBodyPosition;

                float maxDrop = Mathf.Min(MaxBodyCrouchHeight, MaxBodyDropFromCharacter);
                BodyPositionOffset = Mathf.Clamp(BodyPositionOffset, -maxDrop, 0f);

                float force = UpAndDownForce + (GroundAngle / 20f);

                NewAnimationBodyPosition = anim.bodyPosition + transform.up * BodyPositionOffset;
                NewAnimationBodyPosition.y = Mathf.Lerp(
                    LastBodyPositionY,
                    NewAnimationBodyPosition.y,
                    force * Time.deltaTime
                );

                float dist = Mathf.Abs(Animation_Y_BodyPosition - LastBodyPositionY);

                if (!JustCalculateBodyPosition && dist < 1f)
                {
                    anim.bodyPosition = NewAnimationBodyPosition;
                }

                LastBodyPositionY = anim.bodyPosition.y;
            }
            else
            {
                if (!TheresGroundBelow || BlockBodyPositioning)
                    return;

                NewAnimationBodyPosition = anim.bodyPosition + transform.up * BodyPositionOffset;
                NewAnimationBodyPosition.y = Mathf.Lerp(
                    LastBodyPositionY,
                    Animation_Y_BodyPosition,
                    UpAndDownForce * Time.deltaTime
                );

                anim.bodyPosition = NewAnimationBodyPosition;
                LastBodyPositionY = anim.bodyPosition.y;
            }
        }

        void DisableBlock()
        {
            BlockBodyPositioning = false;
            LastBodyPositionY = Animation_Y_BodyPosition;
        }

        public Vector3 GetCalculatedAnimatorCenterOfMass()
        {
            return NewAnimationBodyPosition;
        }

        #endregion

        #region Public Helpers

        public void FootIKSmoothDisable(bool enabled)
        {
            SmoothIKTransition = enabled;
        }

        public void IKBodyPlacingDisable(bool enabled)
        {
            EnableDynamicBodyPlacing = enabled;
        }

        #endregion

        #region Gizmos

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (LeftFoot != null && RightFoot != null && EnableFootPlacement)
            {
                GUIStyle textStyle = new GUIStyle(EditorStyles.boldLabel);

                // Disco base del personaje
                Handles.color = new Color(1, 1, 1, 0.3f);
                Handles.DrawWireDisc(transform.position, transform.up, 0.6f);
                Handles.DrawDottedLine(transform.position - transform.forward * 0.6f, transform.position + transform.forward * 0.6f, 10);
                Handles.DrawDottedLine(transform.position - transform.right * 0.6f, transform.position + transform.right * 0.6f, 10);

                // Límite de step
                Handles.color = new Color(1, 0.2f, 0.2f, 0.5f);
                Handles.DrawWireDisc(transform.position + transform.up * MaxStepHeight, transform.up, 0.3f);
                Handles.DrawDottedLine(transform.position + transform.up * MaxStepHeight - transform.forward * 0.3f, transform.position + transform.up * MaxStepHeight + transform.forward * 0.3f, 2f);
                Handles.DrawDottedLine(transform.position + transform.up * MaxStepHeight - transform.right * 0.3f, transform.position + transform.up * MaxStepHeight + transform.right * 0.3f, 2f);

                textStyle.normal.textColor = new Color(1, 0.4f, 0.4f, 1);
                Handles.Label(transform.position + transform.up * (MaxStepHeight + 0.1f) + transform.right * 0.4f, "Step Limit", textStyle);

                if (UseDynamicFootPlacing)
                {
                    Vector3 leftfootposition = transform.position - transform.right * 0.6f;
                    Handles.color = Color.yellow;
                    Handles.DrawDottedLine(leftfootposition, leftfootposition + transform.up * AnimationLeftFootPositionY, 1);
                    textStyle.normal.textColor = Color.yellow;
                    Handles.Label(leftfootposition + transform.up * AnimationLeftFootPositionY, "LF_Y \n\r" + AnimationLeftFootPositionY.ToString("#0.000"), textStyle);

                    Vector3 rightfootposition = transform.position + transform.right * 0.6f;
                    Handles.color = new Color(0.2f, 0.4f, 1f);
                    Handles.DrawDottedLine(rightfootposition, rightfootposition + transform.up * AnimationRightFootPositionY, 1);
                    textStyle.normal.textColor = new Color(0.2f, 0.4f, 1f);
                    Handles.Label(rightfootposition + transform.up * AnimationRightFootPositionY, "RF_Y \n\r" + AnimationRightFootPositionY.ToString("#0.000"), textStyle);
                }

                if (EnableDynamicBodyPlacing && NewAnimationBodyPosition != Vector3.zero)
                {
                    Handles.color = Color.green;
                    textStyle.normal.textColor = Color.green;
                    Handles.Label(NewAnimationBodyPosition + transform.right * 0.4f + transform.up * 0.1f, "Body Position", textStyle);

                    Handles.DrawWireDisc(NewAnimationBodyPosition, transform.up, 0.2f);

                    if (LeftHitPlaceBase.collider != null)
                    {
                        Handles.color = Color.yellow;
                        Handles.DrawDottedLine(NewAnimationBodyPosition - transform.right * 0.2f, LeftHitPlaceBase.point, 1f);
                    }

                    if (RightHitPlaceBase.collider != null)
                    {
                        Handles.color = new Color(0.3f, 0.6f, 1f);
                        Handles.DrawDottedLine(NewAnimationBodyPosition + transform.right * 0.2f, RightHitPlaceBase.point, 1f);
                    }
                }

                if (LeftFootPlaceBase != null && RightFootPlaceBase != null && LeftHit && RightHit)
                {
                    Handles.color = Color.yellow;
                    Handles.ArrowHandleCap(0, LeftFootPlaceBase.position, Quaternion.FromToRotation(Vector3.forward, LeftHitPlaceBase.normal), 0.2f, EventType.Repaint);
                    Handles.DrawWireDisc(LeftFootPlaceBase.position, LeftFootPlaceBase.up, radius);

                    Handles.color = new Color(0.2f, 0.4f, 1f);
                    Handles.ArrowHandleCap(0, RightFootPlaceBase.position, Quaternion.FromToRotation(Vector3.forward, RightHitPlaceBase.normal), 0.2f, EventType.Repaint);
                    Handles.DrawWireDisc(RightFootPlaceBase.position, RightFootPlaceBase.up, radius);
                }

                if (!LeftHit)
                {
                    Gizmos.color = Color.yellow;
                    float distance = RaycastMaxDistance - RaycastHeight;
                    Gizmos.DrawLine(LeftFoot.position + transform.up * RaycastHeight, LeftFoot.position - transform.up * distance);
                }

                if (!RightHit)
                {
                    Gizmos.color = new Color(0.2f, 0.4f, 1f);
                    float distance = RaycastMaxDistance - RaycastHeight;
                    Gizmos.DrawLine(RightFoot.position + transform.up * RaycastHeight, RightFoot.position - transform.up * distance);
                }
            }
            else
            {
                anim = GetComponent<Animator>();
                if (anim != null)
                {
                    LeftFoot = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
                    RightFoot = anim.GetBoneTransform(HumanBodyBones.RightFoot);
                }
            }

            if (KeepCharacterOnGround)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position + transform.up * RaycastDistanceToGround, transform.position);

                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(transform.position + transform.up * RaycastDistanceToGround, 0.01f);
                Gizmos.DrawWireSphere(transform.position, 0.01f);

                if (HitGroundBodyPlacement.collider != null)
                {
                    Gizmos.DrawWireSphere(HitGroundBodyPlacement.point + transform.up * GroundCheckRadius, GroundCheckRadius);
                }
                else
                {
                    Gizmos.DrawWireSphere(transform.position + transform.up * GroundCheckRadius, GroundCheckRadius);
                }

                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position + transform.up * BodyHeightPosition, 0.01f);
                Handles.Label(transform.position + transform.up * BodyHeightPosition, "Body Position");
            }
        }
#endif

        #endregion
    }
}


#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyAttributeDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        bool previousState = GUI.enabled;
        GUI.enabled = false;
        EditorGUI.PropertyField(position, property, label, true);
        GUI.enabled = previousState;
    }
}

[CustomPropertyDrawer(typeof(EnableIfAttribute))]
public class EnableIfAttributeDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EnableIfAttribute enableIf = (EnableIfAttribute)attribute;
        bool enabled = ShouldEnable(property, enableIf);

        bool previousState = GUI.enabled;
        GUI.enabled = previousState && enabled;
        EditorGUI.PropertyField(position, property, label, true);
        GUI.enabled = previousState;
    }

    private static bool ShouldEnable(SerializedProperty property, EnableIfAttribute enableIf)
    {
        SerializedProperty conditionProperty = property.serializedObject.FindProperty(enableIf.ConditionFieldName);

        if (conditionProperty == null || conditionProperty.propertyType != SerializedPropertyType.Boolean)
            return true;

        return conditionProperty.boolValue == enableIf.ExpectedValue;
    }
}
#endif

// CUSTOM EDITOR
#if UNITY_EDITOR
namespace CustomEditors
{
    [CustomEditor(typeof(Nator.FootIK.NatFootPlacement), true)]
    [CanEditMultipleObjects]
    public class NatFootPlacementEditor : Editor
    {
        private static readonly string[] DontInclude = new string[] { "m_Script" };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawTitle("Nat Foot Placement");

            DrawPropertiesExcluding(serializedObject, DontInclude);

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawTitle(string title)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 30f);
            EditorGUI.DrawRect(rect, new Color(0.18f, 0.32f, 0.18f, 1f));

            GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13
            };
            style.normal.textColor = Color.white;

            EditorGUI.LabelField(rect, title, style);
            EditorGUILayout.Space(6f);
        }
    }
}
#endif