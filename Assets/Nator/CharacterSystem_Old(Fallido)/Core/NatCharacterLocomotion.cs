using UnityEngine;
using Nator.CharacterSystem.Input;
using Nator.CharacterSystem.Animation;
using Nator.CharacterSystem.Motor;

namespace Nator.CharacterSystem.Core
{
    [DisallowMultipleComponent]
    public class NatCharacterLocomotion : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private NatCharacterInput characterInput;
        [SerializeField] private NatLocomotionStateMachine stateMachine;
        [SerializeField] private NatAnimatorBridge animatorBridge;
        [SerializeField] private NatCharacterMotor characterMotor;
        [SerializeField] private Transform cameraReference;

        [Header("Animator Settings")]
        [SerializeField] private float moveSetId = 0f;
        [SerializeField] private bool autoFindMainCamera = true;

        [Header("Runtime Debug")]
        [SerializeField] private Vector3 desiredMoveDirection;
        [SerializeField] private Vector3 desiredFacingDirection;
        [SerializeField] private float inputMagnitude;
        [SerializeField] private float inputDirection;
        [SerializeField] private float rotationMagnitude;
        [SerializeField] private int currentActionState = 0;
        [SerializeField] private bool jumpBuffered;

        [Header("Idle Random")]
        [SerializeField] private bool enableRandomIdle = true;
        [SerializeField] private float randomIdleDelay = 8f;
        [SerializeField] private int randomIdleMin = 1;
        [SerializeField] private int randomIdleMaxExclusive = 4;

        private float idleTimer;
        private bool triggerIdleRandom;
        private int idleRandomValue;

        private NatAnimatorFrameData frameData;
        private Vector3 lastForward;

        public Vector3 DesiredMoveDirection => desiredMoveDirection;
        public Vector3 DesiredFacingDirection => desiredFacingDirection;
        public float InputMagnitude => inputMagnitude;
        public float RotationMagnitude => rotationMagnitude;

        private void Reset()
        {
            if (!characterInput)
                characterInput = GetComponent<NatCharacterInput>();

            if (!stateMachine)
                stateMachine = GetComponent<NatLocomotionStateMachine>();

            if (!animatorBridge)
                animatorBridge = GetComponent<NatAnimatorBridge>();

            if (!characterMotor)
                characterMotor = GetComponent<NatCharacterMotor>();
        }

        private void Awake()
        {
            if (!characterInput)
                characterInput = GetComponent<NatCharacterInput>();

            if (!stateMachine)
                stateMachine = GetComponent<NatLocomotionStateMachine>();

            if (!animatorBridge)
                animatorBridge = GetComponent<NatAnimatorBridge>();

            if (!characterMotor)
                characterMotor = GetComponent<NatCharacterMotor>();

            if (!cameraReference && autoFindMainCamera && Camera.main)
                cameraReference = Camera.main.transform;

            if (animatorBridge != null && !animatorBridge.IsInitialized)
                animatorBridge.Initialize();

            lastForward = transform.forward;
        }

        private void Update()
        {
            if (characterInput == null || stateMachine == null || animatorBridge == null || characterMotor == null)
                return;

            if (!cameraReference && autoFindMainCamera && Camera.main)
                cameraReference = Camera.main.transform;

            characterInput.ReadInput();

            if (characterInput.JumpPressed)
                jumpBuffered = true;

            UpdateDesiredDirections();
            UpdateInputMetrics();

            stateMachine.SetInputs(
                characterInput.StrafeActive,
                characterInput.CrouchActive,
                characterInput.SprintHeld,
                characterInput.RollPressed,
                jumpBuffered);

            stateMachine.SetPhysicalState(
                characterMotor.IsGrounded,
                characterMotor.IsSliding,
                false);

            stateMachine.Evaluate(animatorBridge);

            UpdateIdleRandom();
            BuildAnimatorFrameData(Time.deltaTime);

            animatorBridge.ApplyFrameData(frameData, Time.deltaTime);
            animatorBridge.RefreshStateInfo();

            characterInput.ClearOneFrameInputs();
        }

        private void FixedUpdate()
        {
            if (characterMotor == null || stateMachine == null || animatorBridge == null || characterInput == null)
                return;

            characterMotor.SetDesiredMotion(desiredMoveDirection, desiredFacingDirection);
            characterMotor.SetIgnoreGroundSnap(stateMachine.IgnoreGroundSnap);

            if (stateMachine.UseRootMotion)
            {
                characterMotor.SetRootMotionDelta(
                    animatorBridge.DeltaPosition,
                    animatorBridge.DeltaRotation);
            }

            if (jumpBuffered)
            {
                characterMotor.RequestJump();
                jumpBuffered = false;
            }

            characterMotor.TickMotor(
                stateMachine.CanMove,
                stateMachine.CanRotate,
                characterInput.SprintHeld,
                characterInput.CrouchActive,
                characterInput.StrafeActive,
                stateMachine.UseRootMotion,
                Time.fixedDeltaTime);

            animatorBridge.RefreshStateInfo();
            animatorBridge.ClearRootMotionCache();
        }

        private void OnAnimatorMove()
        {
            if (animatorBridge == null)
                return;

            animatorBridge.HandleAnimatorMove();
        }

        private void UpdateDesiredDirections()
        {
            Vector2 moveInput = characterInput.Move;

            Vector3 forward = transform.forward;
            Vector3 right = transform.right;

            if (cameraReference != null)
            {
                forward = cameraReference.forward;
                right = cameraReference.right;
            }

            forward.y = 0f;
            right.y = 0f;

            if (forward.sqrMagnitude > 0.0001f)
                forward.Normalize();

            if (right.sqrMagnitude > 0.0001f)
                right.Normalize();

            desiredMoveDirection = (forward * moveInput.y + right * moveInput.x);

            if (desiredMoveDirection.sqrMagnitude > 1f)
                desiredMoveDirection.Normalize();

            if (stateMachine.LocomotionMode == NatLocomotionMode.Strafe)
            {
                desiredFacingDirection = forward.sqrMagnitude > 0.0001f
                    ? forward
                    : transform.forward;
            }
            else
            {
                desiredFacingDirection = desiredMoveDirection.sqrMagnitude > 0.0001f
                    ? desiredMoveDirection
                    : transform.forward;
            }
        }

        private void UpdateInputMetrics()
        {
            Vector2 moveInput = characterInput.Move;
            inputMagnitude = Mathf.Clamp01(moveInput.magnitude);

            Vector3 localMove = transform.InverseTransformDirection(desiredMoveDirection);
            float horizontal = Mathf.Clamp(localMove.x, -1f, 1f);
            float vertical = Mathf.Clamp(localMove.z, -1f, 1f);

            inputDirection = Mathf.Atan2(horizontal, Mathf.Abs(vertical) + 0.0001f) * Mathf.Rad2Deg / 180f;

            Vector3 currentForward = transform.forward;
            currentForward.y = 0f;

            Vector3 previousForward = lastForward;
            previousForward.y = 0f;

            if (currentForward.sqrMagnitude > 0.0001f)
                currentForward.Normalize();

            if (previousForward.sqrMagnitude > 0.0001f)
                previousForward.Normalize();

            if (currentForward.sqrMagnitude < 0.0001f || previousForward.sqrMagnitude < 0.0001f)
            {
                rotationMagnitude = 0f;
            }
            else
            {
                float signedAngle = Vector3.SignedAngle(previousForward, currentForward, Vector3.up);
                rotationMagnitude = Mathf.Clamp(signedAngle / 180f, -1f, 1f);
            }

            lastForward = transform.forward;
        }

        private void UpdateIdleRandom()
        {
            triggerIdleRandom = false;

            if (!enableRandomIdle)
                return;

            bool isIdleEnough =
                inputMagnitude < 0.05f &&
                characterMotor.IsGrounded &&
                !stateMachine.IsSliding &&
                !stateMachine.IsRolling &&
                !stateMachine.IsDead &&
                !animatorBridge.CustomAction;

            if (!isIdleEnough)
            {
                idleTimer = 0f;
                return;
            }

            idleTimer += Time.deltaTime;

            if (idleTimer >= randomIdleDelay)
            {
                idleTimer = 0f;
                idleRandomValue = Random.Range(randomIdleMin, randomIdleMaxExclusive);
                triggerIdleRandom = true;
            }
        }

        private void BuildAnimatorFrameData(float deltaTime)
        {
            Vector3 localMove = transform.InverseTransformDirection(desiredMoveDirection);

            float inputHorizontal = Mathf.Clamp(localMove.x, -1f, 1f);
            float inputVertical = Mathf.Clamp(localMove.z, -1f, 1f);

            float animatorMagnitude = CalculateAnimatorInputMagnitude();

            frameData = new NatAnimatorFrameData
            {
                InputHorizontal = inputHorizontal,
                InputVertical = inputVertical,
                InputDirection = inputDirection,
                InputMagnitude = animatorMagnitude,
                RotationMagnitude = rotationMagnitude,

                ActionState = currentActionState,

                IsDead = stateMachine.IsDead,
                IsGrounded = characterMotor.IsGrounded,
                IsCrouching = stateMachine.Stance == NatStance.Crouching,
                IsStrafing = stateMachine.LocomotionMode == NatLocomotionMode.Strafe,
                IsSprinting = stateMachine.IsSprinting,
                IsSliding = characterMotor.IsSliding,

                GroundDistance = characterMotor.GroundDistance,
                GroundAngle = characterMotor.GroundAngle,
                VerticalVelocity = characterMotor.VerticalVelocity,

                MoveSetId = moveSetId,
                IdleRandom = idleRandomValue,

                TriggerIdleRandom = triggerIdleRandom,
                TriggerResetState = false
            };
        }

        private float CalculateAnimatorInputMagnitude()
        {
            if (inputMagnitude < 0.05f)
                return 0f;

            bool isCrouching = stateMachine.Stance == NatStance.Crouching;
            bool isSprinting = stateMachine.IsSprinting;

            // Para respetar el Animator de Invector:
            // 0.0 = idle
            // 0.5 = walk
            // 1.0 = run
            // 1.5 = sprint

            if (isCrouching)
            {
                return Mathf.Lerp(0f, 0.5f, inputMagnitude);
            }

            if (isSprinting)
            {
                return Mathf.Lerp(0f, 1.5f, inputMagnitude);
            }

            // Por ahora dejamos locomoción normal en "walk"
            // Si luego agregamos toggle walk/run real, aquí decidimos entre 0.5 y 1.0
            return Mathf.Lerp(0f, 0.5f, inputMagnitude);
        }
    }
}