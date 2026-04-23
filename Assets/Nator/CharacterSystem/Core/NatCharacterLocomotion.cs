using UnityEngine;
using Nator.CharacterSystem.Input;
using Nator.CharacterSystem.Animation;

namespace Nator.CharacterSystem.Core
{
    [DisallowMultipleComponent]
    public class NatCharacterLocomotion : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private NatCharacterInput characterInput;
        [SerializeField] private NatLocomotionStateMachine stateMachine;
        [SerializeField] private NatAnimatorBridge animatorBridge;
        [SerializeField] private Transform cameraReference;

        [Header("Animator Settings")]
        [SerializeField] private float moveSetId = 0f;
        [SerializeField] private bool autoFindMainCamera = true;

        [Header("Debug / Runtime")]
        [SerializeField] private Vector3 desiredMoveDirection;
        [SerializeField] private Vector3 desiredFacingDirection;
        [SerializeField] private float inputMagnitude;
        [SerializeField] private float inputDirection;
        [SerializeField] private float rotationMagnitude;
        [SerializeField] private bool isGrounded = true;
        [SerializeField] private bool isSliding = false;
        [SerializeField] private bool isDead = false;
        [SerializeField] private float groundDistance = 0f;
        [SerializeField] private float groundAngle = 0f;
        [SerializeField] private float verticalVelocity = 0f;
        [SerializeField] private int currentActionState = 0;

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

        public bool IsGrounded => isGrounded;
        public bool IsSliding => isSliding;
        public bool IsDead => isDead;

        public void SetPhysicalDebugState(
            bool grounded,
            bool sliding,
            bool dead,
            float groundDistanceValue,
            float groundAngleValue,
            float verticalVelocityValue)
        {
            isGrounded = grounded;
            isSliding = sliding;
            isDead = dead;
            groundDistance = groundDistanceValue;
            groundAngle = groundAngleValue;
            verticalVelocity = verticalVelocityValue;
        }

        public void SetActionState(int value)
        {
            currentActionState = value;
        }

        public void SetMoveSetId(float value)
        {
            moveSetId = value;
        }

        private void Reset()
        {
            if (!characterInput)
                characterInput = GetComponent<NatCharacterInput>();

            if (!stateMachine)
                stateMachine = GetComponent<NatLocomotionStateMachine>();

            if (!animatorBridge)
                animatorBridge = GetComponent<NatAnimatorBridge>();
        }

        private void Awake()
        {
            if (!characterInput)
                characterInput = GetComponent<NatCharacterInput>();

            if (!stateMachine)
                stateMachine = GetComponent<NatLocomotionStateMachine>();

            if (!animatorBridge)
                animatorBridge = GetComponent<NatAnimatorBridge>();

            if (!cameraReference && autoFindMainCamera && Camera.main)
                cameraReference = Camera.main.transform;

            if (animatorBridge != null && !animatorBridge.IsInitialized)
                animatorBridge.Initialize();

            lastForward = transform.forward;
        }

        private void Update()
        {
            if (characterInput == null || stateMachine == null || animatorBridge == null)
                return;

            if (!cameraReference && autoFindMainCamera && Camera.main)
                cameraReference = Camera.main.transform;

            characterInput.ReadInput();

            UpdateDesiredDirections();
            UpdateInputMetrics();
            UpdateIdleRandom();

            stateMachine.SetInputs(
                characterInput.StrafeActive,
                characterInput.CrouchActive,
                characterInput.SprintHeld,
                characterInput.RollPressed,
                characterInput.JumpPressed);

            stateMachine.SetPhysicalState(
                isGrounded,
                isSliding,
                isDead);

            stateMachine.Evaluate(animatorBridge);

            BuildAnimatorFrameData(Time.deltaTime);
            animatorBridge.ApplyFrameData(frameData, Time.deltaTime);
            animatorBridge.RefreshStateInfo();

            characterInput.ClearOneFrameInputs();
        }

        private void FixedUpdate()
        {
            if (animatorBridge == null || stateMachine == null)
                return;

            animatorBridge.RefreshStateInfo();
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

            forward.Normalize();
            right.Normalize();

            desiredMoveDirection = (forward * moveInput.y + right * moveInput.x);
            if (desiredMoveDirection.sqrMagnitude > 1f)
                desiredMoveDirection.Normalize();

            if (stateMachine.LocomotionMode == NatLocomotionMode.Strafe)
            {
                desiredFacingDirection = forward.sqrMagnitude > 0f ? forward : transform.forward;
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
            currentForward.Normalize();

            Vector3 previousForward = lastForward;
            previousForward.y = 0f;
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
                isGrounded &&
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

            frameData = new NatAnimatorFrameData
            {
                InputHorizontal = inputHorizontal,
                InputVertical = inputVertical,
                InputDirection = inputDirection,
                InputMagnitude = inputMagnitude,
                RotationMagnitude = rotationMagnitude,

                ActionState = currentActionState,

                IsDead = stateMachine.IsDead,
                IsGrounded = isGrounded,
                IsCrouching = stateMachine.Stance == NatStance.Crouching,
                IsStrafing = stateMachine.LocomotionMode == NatLocomotionMode.Strafe,
                IsSprinting = stateMachine.IsSprinting,
                IsSliding = isSliding,

                GroundDistance = groundDistance,
                GroundAngle = groundAngle,
                VerticalVelocity = verticalVelocity,

                MoveSetId = moveSetId,
                IdleRandom = idleRandomValue,

                TriggerIdleRandom = triggerIdleRandom,
                TriggerResetState = false
            };
        }
    }
}