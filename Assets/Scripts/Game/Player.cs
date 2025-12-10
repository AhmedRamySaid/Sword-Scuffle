using Game;
using UnityEngine;

public class Player : MonoBehaviour
{
    [Header("Movement Settings")] [SerializeField]
    private float mSpeed = 10.0f;

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
    private Vector3 lastSendPos;
    
    public bool isPlayer = false;    
    public void Initialize()
    {
        lastSentData = new PlayerData();
        currentData = new PlayerData();
        mAnimator = GetComponent<Animator>();
        mBody2d = GetComponent<Rigidbody2D>();
        mGroundSensor = transform.Find("GroundSensor").GetComponent<Sensor_HeroKnight>();
    }

    private void Update()
    {
        HandleTimers();
        HandleGrounded();
        HandleInput();
        ApplyVariableGravity();
        UpdateAnimator();
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
            mAnimator.SetBool("Grounded", true);
        }
        else if (mGrounded && !sensorState)
        {
            mGrounded = false;
            mAnimator.SetBool("Grounded", false);
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
            GetComponent<SpriteRenderer>().flipX = false;
            currentData.mFacingDirection = 1;
        }
        else if (inputX < 0)
        {
            GetComponent<SpriteRenderer>().flipX = true;
            currentData.mFacingDirection = -1;
        }

        if (inputY < 0 && mBody2d.velocity.y > 0)
        {
            mBody2d.velocity = new Vector2(mBody2d.velocity.x, 0);
        }

        // Movement (disabled while rolling)
        if (!currentData.mRolling)
            mBody2d.velocity = new Vector2(inputX * mSpeed, mBody2d.velocity.y);

        // Roll
        if (Input.GetKeyDown(KeyCode.LeftShift) && !currentData.mRolling && !mIsWallSliding)
        {
            StartRoll();
        }

        // Jump
        else if (Input.GetKeyDown(KeyCode.Space) && mGrounded && !currentData.mRolling)
        {
            Jump();
        }

        // Attack
        else if (Input.GetMouseButtonDown(0) && currentData.mTimeSinceAttack > 0.25f && !currentData.mRolling)
        {
            Attack();
        }

        // Block
        else if (Input.GetMouseButtonDown(1) && !currentData.mRolling)
        {
            mAnimator.SetTrigger("Block");
            mAnimator.SetBool("IdleBlock", true);
        }
        else if (Input.GetMouseButtonUp(1))
        {
            mAnimator.SetBool("IdleBlock", false);
        }

        // Hurt / Death
        else if (Input.GetKeyDown(KeyCode.Q) && !currentData.mRolling)
            mAnimator.SetTrigger("Hurt");
        else if (Input.GetKeyDown(KeyCode.E) && !currentData.mRolling)
        {
            mAnimator.SetTrigger("Death");
        }

        // Run / Idle states
        if (Mathf.Abs(inputX) > Mathf.Epsilon)
        {
            mDelayToIdle = 0.05f;
            mAnimator.SetInteger("AnimState", 1);
        }
        else
        {
            mDelayToIdle -= Time.deltaTime;
            if (mDelayToIdle < 0)
                mAnimator.SetInteger("AnimState", 0);
        }
    }

    private void StartRoll()
    {
        currentData.mRolling = true;
        mRollCurrentTime = 0f;
        mAnimator.SetTrigger("Roll");
        mBody2d.velocity = new Vector2(currentData.mFacingDirection * mRollForce, mBody2d.velocity.y);
    }

    private void Jump()
    {
        mAnimator.SetTrigger("Jump");
        mGrounded = false;
        mAnimator.SetBool("Grounded", false);

        // Zero out vertical velocity to make jumps consistent
        mBody2d.velocity = new Vector2(mBody2d.velocity.x, 0f);

        // Apply upward impulse force instead of setting velocity
        mBody2d.AddForce(Vector2.up * mJumpForce, ForceMode2D.Impulse);

        mGroundSensor.Disable(0.2f);
    }

    private void Attack()
    {
        currentData.mCurrentAttack++;
        if (currentData.mCurrentAttack > 3) currentData.mCurrentAttack = 1;
        if (currentData.mTimeSinceAttack > 1.0f) currentData.mCurrentAttack = 1;

        mAnimator.SetTrigger("Attack" + currentData.mCurrentAttack);
        currentData.mTimeSinceAttack = 0.0f;
    }

    private void ApplyVariableGravity()
    {
        if (!mGrounded)
        {
            if (mBody2d.velocity.y < 0)
            {
                // Apply stronger gravity when falling
                mBody2d.velocity += Vector2.up * Physics2D.gravity.y * (mFallGravityMultiplier - 1) * Time.deltaTime;
            }
            else if (mBody2d.velocity.y > 0 && !Input.GetKey(KeyCode.Space))
            {
                // If player released jump early, apply slightly more gravity for a short hop
                mBody2d.velocity += Vector2.up * Physics2D.gravity.y * (1.2f - 1) * Time.deltaTime;
            }
        }
    }

    private void UpdateAnimator()
    {
        mAnimator.SetBool("WallSlide", mIsWallSliding);
        mAnimator.SetFloat("AirSpeedY", mBody2d.velocity.y);
    }

    public PlayerData GetDeltaData()
    {
        currentData.xPos = mBody2d.position.x;
        currentData.yPos = mBody2d.position.y;
        
        PlayerData deltaData = PlayerData.SubtractData(currentData, lastSentData);
        lastSentData.CopyData(currentData);
        return deltaData;
    }

    public void ApplyRealData(PlayerData data)
    {
        // Update facing direction
        if (data.mFacingDirection != 0 && data.mFacingDirection != currentData.mFacingDirection)
        {
            currentData.mFacingDirection = data.mFacingDirection;
            GetComponent<SpriteRenderer>().flipX = currentData.mFacingDirection < 0;
        }

        // Handle rolling
        if (data.mRolling && !currentData.mRolling)
        {
            // Start rolling animation for remote player
            mAnimator.SetTrigger("Roll");
        }
        currentData.mRolling = data.mRolling;

        // Update attack
        if (data.isAttacking && !currentData.isAttacking)
        {
            int attackNumber = data.mCurrentAttack;
            mAnimator.SetTrigger("Attack" + attackNumber);
        }
        currentData.isAttacking = data.isAttacking;
        currentData.mCurrentAttack = data.mCurrentAttack;

        // Update position (you can also LERP for smooth movement)
        mBody2d.position = new Vector2(data.xPos, data.yPos);
    }
}