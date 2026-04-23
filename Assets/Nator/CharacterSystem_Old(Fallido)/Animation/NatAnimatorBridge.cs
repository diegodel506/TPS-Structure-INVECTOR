using UnityEngine;

namespace Nator.CharacterSystem.Animation
{
    [DisallowMultipleComponent]
    public class NatAnimatorBridge : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;

        [Header("Float Damping")]
        [SerializeField] private bool useDampedFloats = true;
        [SerializeField] private float locomotionDampTime = 0.10f;
        [SerializeField] private float physicsDampTime = 0.10f;

        [Header("Layer Names")]
        [SerializeField] private string baseLayerName = "Base Layer";
        [SerializeField] private string fullBodyLayerName = "FullBody";

        private int baseLayerIndex = -1;
        private int fullBodyLayerIndex = -1;

        private AnimatorStateInfo baseLayerState;
        private AnimatorStateInfo fullBodyLayerState;

        private NatAnimatorTagInfo activeTags;

        private Vector3 deltaPosition;
        private Quaternion deltaRotation = Quaternion.identity;

        public Animator Animator => animator;
        public NatAnimatorTagInfo ActiveTags => activeTags;

        public Vector3 DeltaPosition => deltaPosition;
        public Quaternion DeltaRotation => deltaRotation;

        public int BaseLayerIndex => baseLayerIndex;
        public int FullBodyLayerIndex => fullBodyLayerIndex;

        public float BaseLayerNormalizedTime => baseLayerState.normalizedTime;
        public float FullBodyLayerNormalizedTime => fullBodyLayerState.normalizedTime;

        public bool LockMovement => activeTags.LockMovement;
        public bool LockRotation => activeTags.LockRotation;
        public bool CustomAction => activeTags.CustomAction;
        public bool IsRolling => activeTags.IsRolling;
        public bool IsAirborne => activeTags.Airborne;
        public bool IsDeadTagActive => activeTags.Dead;

        public bool IsInitialized { get; private set; }

        public void Initialize()
        {
            if (!animator)
                animator = GetComponent<Animator>();

            if (!animator)
            {
                Debug.LogError("[NatAnimatorBridge] Animator not found.", this);
                enabled = false;
                return;
            }

            baseLayerIndex = animator.GetLayerIndex(baseLayerName);
            fullBodyLayerIndex = animator.GetLayerIndex(fullBodyLayerName);

            if (baseLayerIndex < 0)
                Debug.LogWarning($"[NatAnimatorBridge] Layer '{baseLayerName}' not found.", this);

            if (fullBodyLayerIndex < 0)
                Debug.LogWarning($"[NatAnimatorBridge] Layer '{fullBodyLayerName}' not found.", this);

            RefreshStateInfo();
            ClearRootMotionCache();

            IsInitialized = true;
        }

        public void ApplyFrameData(NatAnimatorFrameData data, float deltaTime)
        {
            if (!IsInitialized || !animator)
                return;

            SetFloat(NatAnimatorParameterIds.InputHorizontal, data.InputHorizontal, locomotionDampTime, deltaTime);
            SetFloat(NatAnimatorParameterIds.InputVertical, data.InputVertical, locomotionDampTime, deltaTime);
            SetFloat(NatAnimatorParameterIds.InputDirection, data.InputDirection, locomotionDampTime, deltaTime);
            SetFloat(NatAnimatorParameterIds.InputMagnitude, data.InputMagnitude, locomotionDampTime, deltaTime);
            SetFloat(NatAnimatorParameterIds.RotationMagnitude, data.RotationMagnitude, locomotionDampTime, deltaTime);

            animator.SetInteger(NatAnimatorParameterIds.ActionState, data.ActionState);

            animator.SetBool(NatAnimatorParameterIds.IsDead, data.IsDead);
            animator.SetBool(NatAnimatorParameterIds.IsGrounded, data.IsGrounded);
            animator.SetBool(NatAnimatorParameterIds.IsCrouching, data.IsCrouching);
            animator.SetBool(NatAnimatorParameterIds.IsStrafing, data.IsStrafing);
            animator.SetBool(NatAnimatorParameterIds.IsSprinting, data.IsSprinting);
            animator.SetBool(NatAnimatorParameterIds.IsSliding, data.IsSliding);

            SetFloat(NatAnimatorParameterIds.GroundDistance, data.GroundDistance, physicsDampTime, deltaTime);
            SetFloat(NatAnimatorParameterIds.GroundAngle, data.GroundAngle, physicsDampTime, deltaTime);
            SetFloat(NatAnimatorParameterIds.VerticalVelocity, data.VerticalVelocity, physicsDampTime, deltaTime);

            animator.SetFloat(NatAnimatorParameterIds.MoveSetId, data.MoveSetId);
            animator.SetInteger(NatAnimatorParameterIds.IdleRandom, data.IdleRandom);

            if (data.TriggerIdleRandom)
                animator.SetTrigger(NatAnimatorParameterIds.IdleRandomTrigger);

            if (data.TriggerResetState)
                animator.SetTrigger(NatAnimatorParameterIds.ResetState);
        }

        public void RefreshStateInfo()
        {
            if (!animator)
                return;

            if (baseLayerIndex >= 0)
                baseLayerState = animator.GetCurrentAnimatorStateInfo(baseLayerIndex);

            if (fullBodyLayerIndex >= 0)
                fullBodyLayerState = animator.GetCurrentAnimatorStateInfo(fullBodyLayerIndex);

            activeTags = ReadTags();
        }

        public void HandleAnimatorMove()
        {
            if (!IsInitialized || !animator)
                return;

            deltaPosition = animator.deltaPosition;
            deltaRotation = animator.deltaRotation;
        }

        public void ClearRootMotionCache()
        {
            deltaPosition = Vector3.zero;
            deltaRotation = Quaternion.identity;
        }

        public string GetCurrentBaseLayerStateName()
        {
            if (!animator || baseLayerIndex < 0)
                return string.Empty;

            return GetCurrentStateName(baseLayerIndex);
        }

        public string GetCurrentFullBodyStateName()
        {
            if (!animator || fullBodyLayerIndex < 0)
                return string.Empty;

            return GetCurrentStateName(fullBodyLayerIndex);
        }

        public bool IsInTransition(int layerIndex)
        {
            if (!animator || layerIndex < 0)
                return false;

            return animator.IsInTransition(layerIndex);
        }

        private string GetCurrentStateName(int layerIndex)
        {
            AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(layerIndex);
            if (clips != null && clips.Length > 0 && clips[0].clip != null)
                return clips[0].clip.name;

            return string.Empty;
        }

        private NatAnimatorTagInfo ReadTags()
        {
            NatAnimatorTagInfo info = default;

            if (baseLayerIndex >= 0)
            {
                info.IsRolling |= baseLayerState.IsTag("IsRolling");
                info.LockMovement |= baseLayerState.IsTag("LockMovement");
                info.LockRotation |= baseLayerState.IsTag("LockRotation");
                info.CustomAction |= baseLayerState.IsTag("CustomAction");
                info.Airborne |= baseLayerState.IsTag("Airborne");
                info.IgnoreIK |= baseLayerState.IsTag("IgnoreIK");
                info.IgnoreHeadtrack |= baseLayerState.IsTag("IgnoreHeadtrack");
                info.ClimbLadder |= baseLayerState.IsTag("ClimbLadder");
                info.LadderSlideDown |= baseLayerState.IsTag("LadderSlideDown");
            }

            if (fullBodyLayerIndex >= 0)
            {
                info.Dead |= fullBodyLayerState.IsTag("Dead");
                info.LockMovement |= fullBodyLayerState.IsTag("LockMovement");
                info.LockRotation |= fullBodyLayerState.IsTag("LockRotation");
                info.IgnoreIK |= fullBodyLayerState.IsTag("IgnoreIK");
            }

            return info;
        }

        private void SetFloat(int id, float value, float dampTime, float deltaTime)
        {
            if (!useDampedFloats)
            {
                animator.SetFloat(id, value);
                return;
            }

            animator.SetFloat(id, value, dampTime, deltaTime);
        }

        private void Reset()
        {
            animator = GetComponent<Animator>();
        }

        private void Awake()
        {
            Initialize();
        }
    }
}