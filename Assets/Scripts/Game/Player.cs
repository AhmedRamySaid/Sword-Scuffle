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
        
        public bool isPlayer = false;

        // Animator hashes
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

        public void Initialize()
        {
            lastSentData = new PlayerData();
            currentData = new PlayerData();
            mAnimator = GetComponent<Animator>();
            mBody2d = GetComponent<Rigidbody2D>();
            mGroundSensor = transform.Find("GroundSensor").GetComponent<Sensor_HeroKnight>();
            
            leftSwordHitbox = transform.Find("SwordHitboxLeft").GetComponent<Collider2D>();
            rightSwordHitbox = transform.Find("SwordHitboxRight").GetComponent<Collider2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            
            hitPlayers = new List<Player>();
        }

        private void Update()
        {
            HandleTimers();
            HandleGrounded();
            HandleInput();
            ApplyVariableGravity();
            UpdateAnimator();
            UpdateAttacking();
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

            // Flip sprite
            if (inputX > 0)
            {
                FlipX(true);
            }
            else if (inputX < 0)
            {
                FlipX(false);
            }

            if (inputY < 0 && mBody2d.velocity.y > 0)
                mBody2d.velocity = new Vector2(mBody2d.velocity.x, 0);

            // Movement (disabled while rolling)
            if (!currentData.mRolling)
                mBody2d.velocity = new Vector2(inputX * mSpeed, mBody2d.velocity.y);

            // Roll
            if (Input.GetKeyDown(KeyCode.LeftShift) && !currentData.mRolling && !mIsWallSliding)
                StartRoll();

            // Jump
            else if (Input.GetKeyDown(KeyCode.Space) && mGrounded && !currentData.mRolling)
                Jump();

            // Attack
            else if (Input.GetMouseButtonDown(0) && currentData.mTimeSinceAttack > 0.429f && !currentData.mRolling)
                Attack();

            // Block
            else if (Input.GetMouseButtonDown(1) && !currentData.mRolling)
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

            // Hurt / Death
            else if (Input.GetKeyDown(KeyCode.Q) && !currentData.mRolling)
                mAnimator.SetTrigger(HashHurt);
            else if (Input.GetKeyDown(KeyCode.E) && !currentData.mRolling)
                mAnimator.SetTrigger(HashDeath);

            // Run / Idle
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

            // Use cached hashes
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

        private void ApplyVariableGravity()
        {
            if (!mGrounded)
            {
                if (mBody2d.velocity.y < 0)
                    mBody2d.velocity += Vector2.up * Physics2D.gravity.y * (mFallGravityMultiplier - 1) * Time.deltaTime;
                else if (mBody2d.velocity.y > 0 && !Input.GetKey(KeyCode.Space))
                    mBody2d.velocity += Vector2.up * Physics2D.gravity.y * 0.2f * Time.deltaTime; // short hop
            }
        }

        private void UpdateAttacking()
        {
            AnimatorStateInfo state = mAnimator.GetCurrentAnimatorStateInfo(0);
            bool attack = state.IsName("Attack1") || 
                          state.IsName("Attack2") || 
                          state.IsName("Attack3");

            if (!attack)
            {
                leftSwordHitbox.enabled = false;
                rightSwordHitbox.enabled = false;
                currentData.isAttacking = false;
                hitPlayers.Clear();
                return;
            }

            if (currentData.mFacingDirection == 1)
            {
                rightSwordHitbox.enabled = true;
                return;
            }
            leftSwordHitbox.enabled = true;
        }

        private void UpdateAnimator()
        {
            mAnimator.SetBool(HashWallSlide, mIsWallSliding);
            mAnimator.SetFloat(HashAirSpeedY, mBody2d.velocity.y);
        }

        private void FlipX(bool facingRight)
        {
            int dir = facingRight ? 1 : -1;
            spriteRenderer.flipX = !facingRight;
            currentData.mFacingDirection = dir;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Check if the collider is a sword
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

        public void ApplyDeltaData(PlayerData deltaData)
        {
            // Facing direction
            if (deltaData.mFacingDirection != 0 && deltaData.mFacingDirection != currentData.mFacingDirection)
            {
                FlipX(deltaData.mFacingDirection > 0);
                currentData.mFacingDirection = deltaData.mFacingDirection;
            }

            // Rolling
            if (deltaData.rollingChanged)
            {
                if (deltaData.mRolling && !currentData.mRolling)
                    mAnimator.SetTrigger(HashRoll);
                currentData.mRolling = deltaData.mRolling;
            }

            // Attacking
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
                currentData.mCurrentAttack += deltaData.mCurrentAttack; // accumulate attacks if needed
            }

            // Blocking
            if (deltaData.blockingChanged)
            {
                if (deltaData.isBlocking && !currentData.isBlocking)
                    mAnimator.SetTrigger(HashBlock);
                currentData.isBlocking = deltaData.isBlocking;
            }

            // Position delta (smooth movement)
            if (deltaData.xPos != 0 || deltaData.yPos != 0)
            {
                Vector2 targetPos = new Vector2(currentData.xPos + deltaData.xPos,
                    currentData.yPos + deltaData.yPos);
                mBody2d.position = Vector2.Lerp(mBody2d.position, targetPos, 0.5f);

                currentData.xPos += deltaData.xPos;
                currentData.yPos += deltaData.yPos;
            }
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
            // Facing direction
            if (data.mFacingDirection != 0 && data.mFacingDirection != currentData.mFacingDirection)
            {
                FlipX(currentData.mFacingDirection > 0);
            }

            // Rolling
            if (data.mRolling && !currentData.mRolling)
                mAnimator.SetTrigger(HashRoll);
            currentData.mRolling = data.mRolling;

            // Attacking
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

            // Blocking
            if (data.isBlocking && !currentData.isBlocking)
                mAnimator.SetTrigger(HashBlock);
            currentData.isBlocking = data.isBlocking;

            // Smooth remote movement
            mBody2d.position = Vector2.Lerp(mBody2d.position, new Vector2(data.xPos, data.yPos), 0.5f);
        }
    }
}