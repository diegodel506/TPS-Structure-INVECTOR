using UnityEngine;

namespace Nator.CharacterSystem.Motor
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class NatCharacterMotor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Rigidbody rb;
        [SerializeField] private CapsuleCollider capsule;

        [Header("Ground Detection")]
        [SerializeField] private LayerMask groundLayers = ~0;
        [SerializeField] private float groundProbeOffset = 0.05f;
        [SerializeField] private float groundCheckDistance = 0.35f;
        [SerializeField] private float groundedDistance = 0.12f;
        [SerializeField] private float sphereCastRadiusScale = 0.95f;
        [SerializeField] private float maxGroundAngle = 60f;

        [Header("Snap To Ground")]
        [SerializeField] private bool useGroundSnap = true;
        [SerializeField] private float snapForce = 18f;
        [SerializeField] private float maxSnapUpSpeed = 4f;
        [SerializeField] private float maxSnapDownSpeed = 10f;

        [Header("Movement")]
        [SerializeField] private float walkSpeed = 2f;
        [SerializeField] private float runSpeed = 4f;
        [SerializeField] private float sprintSpeed = 6f;
        [SerializeField] private float crouchSpeed = 2f;
        [SerializeField] private float groundAcceleration = 16f;
        [SerializeField] private float airAcceleration = 5f;
        [SerializeField] private float rotationSpeed = 14f;
        [SerializeField] private bool projectMovementOnGround = true;

        [Header("Air")]
        [SerializeField] private float airControl = 0.65f;
        [SerializeField] private float jumpHeight = 5f;
        [SerializeField] private float extraGravity = 22f;
        [SerializeField] private float maxFallSpeed = 35f;

        [Header("Root Motion")]
        [SerializeField] private bool allowRootMotion = true;
        [SerializeField] private bool applyRootMotionRotation = true;
        [SerializeField] private float rootMotionVelocityMultiplier = 1f;

        [Header("Runtime Debug")]
        [SerializeField] private bool isGrounded;
        [SerializeField] private bool wasGroundedLastFrame;
        [SerializeField] private bool isSliding;
        [SerializeField] private float groundDistance;
        [SerializeField] private float groundAngle;
        [SerializeField] private float verticalVelocity;
        [SerializeField] private Vector3 desiredMoveDirection;
        [SerializeField] private Vector3 desiredFacingDirection;
        [SerializeField] private Vector3 desiredVelocity;
        [SerializeField] private Vector3 currentVelocity;
        [SerializeField] private Vector3 projectedGroundVelocity;
        [SerializeField] private Vector3 lastGroundNormal = Vector3.up;

        private RaycastHit groundHit;
        private bool jumpRequested;
        private bool ignoreGroundSnapThisFrame;
        private bool rootMotionRequestedThisFrame;
        private Vector3 rootMotionDeltaPosition;
        private Quaternion rootMotionDeltaRotation = Quaternion.identity;

        public bool IsGrounded => isGrounded;
        public bool WasGroundedLastFrame => wasGroundedLastFrame;
        public bool IsSliding => isSliding;
        public float GroundDistance => groundDistance;
        public float GroundAngle => groundAngle;
        public float VerticalVelocity => verticalVelocity;
        public Vector3 CurrentVelocity => currentVelocity;
        public Vector3 GroundNormal => lastGroundNormal;
        public RaycastHit GroundHit => groundHit;

        private void Reset()
        {
            rb = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();
        }

        private void Awake()
        {
            if (!rb)
                rb = GetComponent<Rigidbody>();

            if (!capsule)
                capsule = GetComponent<CapsuleCollider>();

            ConfigureRigidbody();
        }

        private void ConfigureRigidbody()
        {
            if (!rb)
                return;

            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        public void SetDesiredMotion(Vector3 moveDirection, Vector3 facingDirection)
        {
            desiredMoveDirection = moveDirection;
            desiredFacingDirection = facingDirection;
        }

        public void RequestJump()
        {
            jumpRequested = true;
        }

        public void SetIgnoreGroundSnap(bool value)
        {
            ignoreGroundSnapThisFrame = value;
        }

        public void SetRootMotionDelta(Vector3 deltaPosition, Quaternion deltaRotation)
        {
            rootMotionDeltaPosition = deltaPosition;
            rootMotionDeltaRotation = deltaRotation;
            rootMotionRequestedThisFrame = true;
        }

        public void TickMotor(
            bool canMove,
            bool canRotate,
            bool wantsSprint,
            bool wantsCrouch,
            bool isStrafing,
            bool useRootMotion,
            float deltaTime)
        {
            if (!rb || !capsule)
                return;

            wasGroundedLastFrame = isGrounded;

            CheckGround();
            UpdateVerticalVelocity();

            if (canRotate)
                ApplyRotation(deltaTime, useRootMotion);

            if (canMove)
                ApplyMovement(wantsSprint, wantsCrouch, isStrafing, useRootMotion, deltaTime);
            else
                DampHorizontalMovement(deltaTime);

            HandleJump();
            ApplyExtraGravity(deltaTime);
            ApplyGroundSnap(deltaTime);

            currentVelocity = rb.linearVelocity;

            rootMotionRequestedThisFrame = false;
            rootMotionDeltaPosition = Vector3.zero;
            rootMotionDeltaRotation = Quaternion.identity;
            ignoreGroundSnapThisFrame = false;
        }

        private void CheckGround()
        {
            Vector3 origin = GetGroundCheckOrigin();
            float radius = Mathf.Max(0.01f, capsule.radius * sphereCastRadiusScale);
            float castDistance = groundCheckDistance + groundedDistance;

            bool hitSomething = Physics.SphereCast(
                origin,
                radius,
                Vector3.down,
                out groundHit,
                castDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore);

            if (!hitSomething)
            {
                isGrounded = false;
                isSliding = false;
                groundDistance = castDistance;
                groundAngle = 0f;
                lastGroundNormal = Vector3.up;
                groundHit = default;
                return;
            }

            groundDistance = Mathf.Max(0f, groundHit.distance - groundProbeOffset);
            groundAngle = Vector3.Angle(groundHit.normal, Vector3.up);
            lastGroundNormal = groundHit.normal;

            isSliding = groundAngle > maxGroundAngle;
            //isGrounded = groundDistance <= groundedDistance && !isSliding;
            isGrounded = groundDistance <= groundedDistance;
        }

        private Vector3 GetGroundCheckOrigin()
        {
            Vector3 center = transform.TransformPoint(capsule.center);
            float halfHeight = Mathf.Max(capsule.radius, (capsule.height * 0.5f) - capsule.radius);

            Vector3 bottomSphereCenter = center - Vector3.up * halfHeight;
            bottomSphereCenter += Vector3.up * (capsule.radius + groundProbeOffset);

            return bottomSphereCenter;
        }

        private void UpdateVerticalVelocity()
        {
            verticalVelocity = rb.linearVelocity.y;

            if (isGrounded && verticalVelocity < 0f)
                verticalVelocity = 0f;
        }

        private void ApplyRotation(float deltaTime, bool useRootMotion)
        {
            if (useRootMotion && allowRootMotion && rootMotionRequestedThisFrame && applyRootMotionRotation)
            {
                Quaternion targetRotation = rb.rotation * rootMotionDeltaRotation;
                Quaternion scopeNewRotation = Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * deltaTime);
                rb.MoveRotation(scopeNewRotation);
                return;
            }

            Vector3 faceDir = desiredFacingDirection;
            faceDir.y = 0f;

            if (faceDir.sqrMagnitude < 0.0001f)
                return;

            Quaternion target = Quaternion.LookRotation(faceDir.normalized, Vector3.up);
            Quaternion newRotation = Quaternion.Slerp(rb.rotation, target, rotationSpeed * deltaTime);
            rb.MoveRotation(newRotation);
        }

        private void ApplyMovement(
            bool wantsSprint,
            bool wantsCrouch,
            bool isStrafing,
            bool useRootMotion,
            float deltaTime)
        {
            Vector3 velocity = rb.linearVelocity;
            Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);

            if (useRootMotion && allowRootMotion && rootMotionRequestedThisFrame)
            {
                Vector3 rmVelocity = rootMotionDeltaPosition / Mathf.Max(deltaTime, 0.0001f);
                rmVelocity *= rootMotionVelocityMultiplier;
                rmVelocity.y = 0f;

                Vector3 targetHorizontal = rmVelocity;
                if (isGrounded && projectMovementOnGround)
                    targetHorizontal = ProjectVelocityOnGround(targetHorizontal);

                Vector3 blended = Vector3.Lerp(horizontalVelocity, targetHorizontal, groundAcceleration * deltaTime);
                rb.linearVelocity = new Vector3(blended.x, velocity.y, blended.z);
                desiredVelocity = targetHorizontal;
                projectedGroundVelocity = targetHorizontal;
                return;
            }

            Vector3 moveDir = desiredMoveDirection;
            moveDir.y = 0f;

            if (moveDir.sqrMagnitude > 1f)
                moveDir.Normalize();

            float targetSpeed = ResolveTargetSpeed(wantsSprint, wantsCrouch, isStrafing);
            Vector3 targetVelocity = moveDir * targetSpeed;

            if (isGrounded && projectMovementOnGround)
            {
                targetVelocity = ProjectVelocityOnGround(targetVelocity);
                projectedGroundVelocity = targetVelocity;
            }
            else
            {
                projectedGroundVelocity = Vector3.zero;
            }

            desiredVelocity = targetVelocity;

            float accel = isGrounded ? groundAcceleration : airAcceleration;
            float control = isGrounded ? 1f : airControl;

            Vector3 appliedTarget = Vector3.Lerp(horizontalVelocity, targetVelocity, accel * control * deltaTime);

            rb.linearVelocity = new Vector3(appliedTarget.x, velocity.y, appliedTarget.z);
        }

        private Vector3 ProjectVelocityOnGround(Vector3 velocity)
        {
            if (lastGroundNormal.sqrMagnitude < 0.001f)
                return velocity;

            Vector3 projected = Vector3.ProjectOnPlane(velocity, lastGroundNormal);
            return projected;
        }

        private float ResolveTargetSpeed(bool wantsSprint, bool wantsCrouch, bool isStrafing)
        {
            if (wantsCrouch)
                return crouchSpeed;

            if (wantsSprint && !isStrafing)
                return sprintSpeed;

            return runSpeed;
        }

        private void DampHorizontalMovement(float deltaTime)
        {
            Vector3 velocity = rb.linearVelocity;
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            Vector3 damped = Vector3.Lerp(horizontal, Vector3.zero, groundAcceleration * deltaTime);
            rb.linearVelocity = new Vector3(damped.x, velocity.y, damped.z);
        }

        private void HandleJump()
        {
            if (!jumpRequested)
                return;

            jumpRequested = false;

            if (!isGrounded || isSliding)
                return;

            Vector3 velocity = rb.linearVelocity;
            velocity.y = jumpHeight;
            rb.linearVelocity = velocity;

            isGrounded = false;
        }

        private void ApplyExtraGravity(float deltaTime)
        {
            Vector3 velocity = rb.linearVelocity;

            if (isGrounded && velocity.y <= 0f)
                return;

            velocity.y -= extraGravity * deltaTime;
            velocity.y = Mathf.Max(velocity.y, -maxFallSpeed);

            rb.linearVelocity = velocity;
        }

        private void ApplyGroundSnap(float deltaTime)
        {
            if (!useGroundSnap)
                return;

            if (ignoreGroundSnapThisFrame)
                return;

            if (!isGrounded)
                return;

            if (jumpRequested)
                return;

            if (lastGroundNormal.sqrMagnitude < 0.001f)
                return;

            if (rb.linearVelocity.y < -2f)
                return;

            Vector3 velocity = rb.linearVelocity;
            if (velocity.y > maxSnapUpSpeed)
                return;

            float distanceError = groundDistance;
            if (distanceError <= 0.001f)
                return;

            float snapY = -(distanceError * snapForce);
            snapY = Mathf.Clamp(snapY, -maxSnapDownSpeed, maxSnapUpSpeed);

            velocity.y = snapY;
            rb.linearVelocity = velocity;
        }

        private void OnDrawGizmosSelected()
        {
            if (!capsule)
                capsule = GetComponent<CapsuleCollider>();

            if (!capsule)
                return;

            Vector3 origin = GetGroundCheckOrigin();
            float radius = Mathf.Max(0.01f, capsule.radius * sphereCastRadiusScale);
            float castDistance = groundCheckDistance + groundedDistance;

            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(origin, radius);
            Gizmos.DrawWireSphere(origin + Vector3.down * castDistance, radius);

            if (groundHit.collider != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(origin, groundHit.point);
                Gizmos.DrawSphere(groundHit.point, 0.03f);

                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(groundHit.point, groundHit.normal * 0.5f);
            }

            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position + Vector3.up * 0.1f, desiredMoveDirection);

            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(transform.position + Vector3.up * 0.2f, desiredFacingDirection);
        }
    }
}