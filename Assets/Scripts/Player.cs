using UnityEngine;

public class Player : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float mSpeed = 10.0f;
    [SerializeField] private float mJumpForce = 10.0f;
    [SerializeField] private float mRollForce = 25.0f;

    private Animator mAnimator;
    private Rigidbody2D mBody2d;
    private Sensor_HeroKnight mGroundSensor;

    private bool mIsWallSliding;
    private bool mGrounded;
    private bool mRolling;
    private int mFacingDirection = 1;
    private int mCurrentAttack;
    private float mTimeSinceAttack;
    private float mDelayToIdle;
    private float mRollCurrentTime;
    private readonly float mRollDuration = 8f / 14f; // ~0.57s

    private void Start()
    {
        mAnimator = GetComponent<Animator>();
        mBody2d = GetComponent<Rigidbody2D>();
        mGroundSensor = transform.Find("GroundSensor").GetComponent<Sensor_HeroKnight>();
    }

    private void Update()
    {
        HandleTimers();
        HandleGrounded();
        HandleInput();
        UpdateAnimator();
    }

    private void HandleTimers()
    {
        mTimeSinceAttack += Time.deltaTime;

        if (mRolling)
        {
            mRollCurrentTime += Time.deltaTime;
            if (mRollCurrentTime >= mRollDuration)
            {
                mRolling = false;
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
        float inputX = Input.GetAxisRaw("Horizontal");

        // Flip sprite
        if (inputX > 0)
        {
            GetComponent<SpriteRenderer>().flipX = false;
            mFacingDirection = 1;
        }
        else if (inputX < 0)
        {
            GetComponent<SpriteRenderer>().flipX = true;
            mFacingDirection = -1;
        }

        // Movement (disabled while rolling)
        if (!mRolling)
            mBody2d.velocity = new Vector2(inputX * mSpeed, mBody2d.velocity.y);

        // Roll
        if (Input.GetKeyDown(KeyCode.LeftShift) && !mRolling && !mIsWallSliding)
        {
            StartRoll();
        }

        // Jump
        else if (Input.GetKeyDown(KeyCode.Space) && mGrounded && !mRolling)
        {
            Jump();
        }

        // Attack
        else if (Input.GetMouseButtonDown(0) && mTimeSinceAttack > 0.25f && !mRolling)
        {
            Attack();
        }

        // Block
        else if (Input.GetMouseButtonDown(1) && !mRolling)
        {
            mAnimator.SetTrigger("Block");
            mAnimator.SetBool("IdleBlock", true);
        }
        else if (Input.GetMouseButtonUp(1))
        {
            mAnimator.SetBool("IdleBlock", false);
        }

        // Hurt / Death
        else if (Input.GetKeyDown(KeyCode.Q) && !mRolling)
            mAnimator.SetTrigger("Hurt");
        else if (Input.GetKeyDown(KeyCode.E) && !mRolling)
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
        mRolling = true;
        mRollCurrentTime = 0f;
        mAnimator.SetTrigger("Roll");
        mBody2d.velocity = new Vector2(mFacingDirection * mRollForce, mBody2d.velocity.y);
    }

    private void Jump()
    {
        mAnimator.SetTrigger("Jump");
        mGrounded = false;
        mAnimator.SetBool("Grounded", false);
        mBody2d.velocity = new Vector2(mBody2d.velocity.x, mJumpForce);
        mGroundSensor.Disable(0.2f);
    }

    private void Attack()
    {
        mCurrentAttack++;
        if (mCurrentAttack > 3) mCurrentAttack = 1;
        if (mTimeSinceAttack > 1.0f) mCurrentAttack = 1;

        mAnimator.SetTrigger("Attack" + mCurrentAttack);
        mTimeSinceAttack = 0.0f;
    }

    private void UpdateAnimator()
    {
        mAnimator.SetBool("WallSlide", mIsWallSliding);
        mAnimator.SetFloat("AirSpeedY", mBody2d.velocity.y);
    }
}
