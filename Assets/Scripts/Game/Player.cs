using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public class Player : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float mSpeed = 10.0f;
        [SerializeField] private float mJumpForce = 10.0f;
        [SerializeField] private float mRollForce = 25.0f;
        [SerializeField] private float mFallGravityMultiplier = 1.5f;

        private Animator mAnimator;
        private Rigidbody2D mBody2d;
        private Sensor_HeroKnight mGroundSensor;

        private bool mIsWallSliding;
        private bool mGrounded;

        private float mDelayToIdle;
        private float mRollCurrentTime;
        private readonly float mRollDuration = 8f / 14f; // ~0.57s

        private PlayerData lastSentData;
        private PlayerData currentData;

        private Collider2D leftSwordHitbox;
        private Collider2D rightSwordHitbox;
        private SpriteRenderer spriteRenderer;
        private List<Player> hitPlayers;

        private Vector2 targetPosition;
        private readonly float smoothSpeed = 8f;

        public bool isPlayer = false;

        private static readonly int HashRoll = Animator.StringToHash("Roll");
        private static readonly int HashAttack1 = Animator.StringToHash("Attack1");
        private static readonly int HashAttack2 = Animator.StringToHash("Attack2");
        private static readonly int HashAttack3 = Animator.StringToHash("Attack3");
        private static readonly int HashBlock = Animator.StringToHash("Block");
        private static readonly int HashJump = Animator.StringToHash("Jump");
        private static readonly int HashHurt = Animator.StringToHash("Hurt");
        private static readonly int HashDeath = Animator.StringToHash("Death");
        private static readonly int HashWallSlide = Animator.StringToHash("WallSlide");
        private static readonly int HashAirSpeedY = Animator.StringToHash("AirSpeedY");
        private static readonly int HashGrounded = Animator.StringToHash("Grounded");
        private static readonly int HashAnimState = Animator.StringToHash("AnimState");
        private static readonly int HashIdleBlock = Animator.StringToHash("IdleBlock");

        private bool wasInAttack = false;

        public void Initialize()
        {
            lastSentData = new PlayerData();
            currentData = new PlayerData();

            currentData.xPos = transform.position.x;
            currentData.yPos = transform.position.y;
            lastSentData.CopyData(currentData);

            mAnimator = GetComponent<Animator>();
            mBody2d = GetComponent<Rigidbody2D>();
            mGroundSensor = transform.Find("GroundSensor").GetComponent<Sensor_HeroKnight>();

            leftSwordHitbox = transform.Find("SwordHitboxLeft").GetComponent<Collider2D>();
            rightSwordHitbox = transform.Find("SwordHitboxRight").GetComponent<Collider2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();

            if (!isPlayer)
            {
                Rigidbody2D rb = GetComponent<Rigidbody2D>();
                rb.gravityScale = 0.001f;
            }

            targetPosition = new Vector2(currentData.xPos, currentData.yPos);
            hitPlayers = new List<Player>();
        }

        private void Update()
        {
            UpdateAttackingStateMachine();
            HandleTimers();
            HandleGrounded();
            HandleInput();
            ApplyVariableGravity();
            UpdateAnimator();
        }

        public void SetToPlayer()
        {
            isPlayer = true;
            GetComponent<Rigidbody2D>().gravityScale = 1f;
        }

        private void HandleTimers()
        {
            currentData.mTimeSinceAttack += Time.deltaTime;

            if (currentData.mRolling)
            {
                mRollCurrentTime += Time.deltaTime;
                if (mRollCurrentTime >= mRollDuration)
                {
                    currentData.mRolling = false;
                    mRollCurrentTime = 0f;
                }
            }
        }

        private void HandleGrounded()
        {
            bool sensorState = mGroundSensor.State();
            if (!mGrounded && sensorState)
            {
                mGrounded = true;
                mAnimator.SetBool(HashGrounded, true);
            }
            else if (mGrounded && !sensorState)
            {
                mGrounded = false;
                mAnimator.SetBool(HashGrounded, false);
            }
        }

        private void HandleInput()
        {
            if (!isPlayer) return;

            float inputX = Input.GetAxisRaw("Horizontal");
            float inputY = Input.GetAxisRaw("Vertical");

            if (inputX > 0) FlipX(true);
            else if (inputX < 0) FlipX(false);

            if (inputY < 0 && mBody2d.velocity.y > 0)
                mBody2d.velocity = new Vector2(mBody2d.velocity.x, 0);

            if (!currentData.mRolling)
                mBody2d.velocity = new Vector2(inputX * mSpeed, mBody2d.velocity.y);

            if (Input.GetKeyDown(KeyCode.LeftShift) && !currentData.mRolling)
                StartRoll();

            else if (Input.GetKeyDown(KeyCode.Space) && mGrounded)
                Jump();

            else if (Input.GetMouseButtonDown(0) && currentData.mTimeSinceAttack > 0.429f)
                Attack();

            else if (Input.GetMouseButtonDown(1))
            {
                currentData.isBlocking = true;
                mAnimator.SetTrigger(HashBlock);
                mAnimator.SetBool(HashIdleBlock, true);
            }
            else if (Input.GetMouseButtonUp(1))
            {
                currentData.isBlocking = false;
                mAnimator.SetBool(HashIdleBlock, false);
            }

            if (Mathf.Abs(inputX) > Mathf.Epsilon)
            {
                mDelayToIdle = 0.05f;
                mAnimator.SetInteger(HashAnimState, 1);
            }
            else
            {
                mDelayToIdle -= Time.deltaTime;
                if (mDelayToIdle < 0)
                    mAnimator.SetInteger(HashAnimState, 0);
            }
        }

        private void StartRoll()
        {
            currentData.mRolling = true;
            mRollCurrentTime = 0f;
            mAnimator.SetTrigger(HashRoll);
            mBody2d.velocity = new Vector2(currentData.mFacingDirection * mRollForce, mBody2d.velocity.y);
        }

        private void Jump()
        {
            mAnimator.SetTrigger(HashJump);
            mGrounded = false;
            mAnimator.SetBool(HashGrounded, false);
            mBody2d.velocity = new Vector2(mBody2d.velocity.x, 0f);
            mBody2d.AddForce(Vector2.up * mJumpForce, ForceMode2D.Impulse);
            mGroundSensor.Disable(0.2f);
        }

        private void Attack()
        {
            if (currentData.isAttacking) return;

            currentData.mCurrentAttack++;
            if (currentData.mCurrentAttack > 2) currentData.mCurrentAttack = 1;
            if (currentData.mTimeSinceAttack > 1.0f) currentData.mCurrentAttack = 1;

            int attackHash = currentData.mCurrentAttack switch
            {
                1 => HashAttack1,
                2 => HashAttack2,
                _ => HashAttack1
            };

            mAnimator.SetTrigger(attackHash);
            currentData.isAttacking = true;
            currentData.mTimeSinceAttack = 0.0f;
        }

        private void UpdateAttackingStateMachine()
        {
            AnimatorStateInfo state = mAnimator.GetCurrentAnimatorStateInfo(0);
            bool inAttack =
                state.IsName("Attack1") ||
                state.IsName("Attack2") ||
                state.IsName("Attack3");

            if (inAttack)
            {
                if (currentData.mFacingDirection == 1)
                {
                    rightSwordHitbox.enabled = true;
                    leftSwordHitbox.enabled = false;
                }
                else
                {
                    leftSwordHitbox.enabled = true;
                    rightSwordHitbox.enabled = false;
                }

                wasInAttack = true;
                return;
            }

            leftSwordHitbox.enabled = false;
            rightSwordHitbox.enabled = false;

            if (wasInAttack && !inAttack)
            {
                wasInAttack = false;
                currentData.isAttacking = false;
                hitPlayers.Clear();
            }
        }

        private void ApplyVariableGravity()
        {
            if (!isPlayer) return;

            if (!mGrounded)
            {
                if (mBody2d.velocity.y < 0)
                    mBody2d.velocity += Vector2.up * Physics2D.gravity.y * (mFallGravityMultiplier - 1) * Time.deltaTime;
                else if (mBody2d.velocity.y > 0 && !Input.GetKey(KeyCode.Space))
                    mBody2d.velocity += Vector2.up * Physics2D.gravity.y * 0.2f * Time.deltaTime;
            }
        }

        private void UpdateAnimator()
        {
            mAnimator.SetBool(HashWallSlide, mIsWallSliding);
            mAnimator.SetFloat(HashAirSpeedY, mBody2d.velocity.y);
        }

        private void FlipX(bool facingRight)
        {
            spriteRenderer.flipX = !facingRight;
            currentData.mFacingDirection = facingRight ? 1 : -1;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Sword"))
            {
                bool gotHit = other.GetComponentInParent<Player>().AttackPlayer(this);

                if (gotHit)
                {
                    Debug.Log("Hit another player");
                    TakeDamage();
                }
            }
        }

        private bool AttackPlayer(Player player)
        {
            if (hitPlayers.Contains(player)) return false;

            hitPlayers.Add(player);
            return true;
        }

        private void TakeDamage()
        {
            mAnimator.SetTrigger(HashHurt);
        }

        public PlayerData GetDeltaData()
        {
            currentData.xPos = mBody2d.position.x;
            currentData.yPos = mBody2d.position.y;
            PlayerData deltaData = PlayerData.SubtractData(currentData, lastSentData);
            lastSentData.CopyData(currentData);
            return deltaData;
        }

        private void FixedUpdate()
        {
            if (isPlayer) return;
            mBody2d.position = Vector2.Lerp(mBody2d.position, targetPosition, Time.fixedDeltaTime * smoothSpeed);
        }

        public void ApplyDeltaData(PlayerData deltaData)
        {
            if (deltaData.mFacingDirection != 0 && deltaData.mFacingDirection != currentData.mFacingDirection)
                FlipX(deltaData.mFacingDirection > 0);

            if (deltaData.rollingChanged)
            {
                if (deltaData.mRolling && !currentData.mRolling)
                    mAnimator.SetTrigger(HashRoll);

                currentData.mRolling = deltaData.mRolling;
            }

            if (deltaData.attackingChanged)
            {
                if (deltaData.isAttacking && !currentData.isAttacking)
                {
                    int attackHash = deltaData.mCurrentAttack switch
                    {
                        1 => HashAttack1,
                        2 => HashAttack2,
                        3 => HashAttack3,
                        _ => HashAttack1
                    };
                    mAnimator.SetTrigger(attackHash);
                }
                currentData.isAttacking = deltaData.isAttacking;
                currentData.mCurrentAttack += deltaData.mCurrentAttack;
            }

            if (deltaData.blockingChanged)
            {
                if (deltaData.isBlocking && !currentData.isBlocking)
                    mAnimator.SetTrigger(HashBlock);

                currentData.isBlocking = deltaData.isBlocking;
                mAnimator.SetBool(HashIdleBlock, deltaData.isBlocking);
            }

            targetPosition = new Vector2(currentData.xPos + deltaData.xPos, currentData.yPos + deltaData.yPos);
            currentData.xPos += deltaData.xPos;
            currentData.yPos += deltaData.yPos;
        }

        public PlayerData GetRealData()
        {
            currentData.xPos = mBody2d.position.x;
            currentData.yPos = mBody2d.position.y;
            lastSentData.CopyData(currentData);

            PlayerData realData = new PlayerData();
            realData.CopyData(lastSentData);
            return realData;
        }

        public void ApplyRealData(PlayerData data)
        {
            if (data.mFacingDirection != 0 && data.mFacingDirection != currentData.mFacingDirection)
                FlipX(data.mFacingDirection > 0);

            if (data.mRolling && !currentData.mRolling)
                mAnimator.SetTrigger(HashRoll);

            currentData.mRolling = data.mRolling;

            if (data.isAttacking && !currentData.isAttacking)
            {
                int attackHash = data.mCurrentAttack switch
                {
                    1 => HashAttack1,
                    2 => HashAttack2,
                    3 => HashAttack3,
                    _ => HashAttack1
                };
                mAnimator.SetTrigger(attackHash);
            }

            currentData.isAttacking = data.isAttacking;
            currentData.mCurrentAttack = data.mCurrentAttack;

            if (data.isBlocking != currentData.isBlocking)
            {
                if (data.isBlocking)
                    mAnimator.SetTrigger(HashBlock);

                mAnimator.SetBool(HashIdleBlock, data.isBlocking);
            }

            currentData.isBlocking = data.isBlocking;

            targetPosition = new Vector2(data.xPos, data.yPos);
            currentData.xPos = data.xPos;
            currentData.yPos = data.yPos;
        }
    }
}