using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(AudioSource))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Range(1f, 20f)] private float maxSpeed = 8f;
    [SerializeField, Range(5f, 200f)] private float acceleration = 70f;
    [SerializeField, Range(5f, 200f)] private float deceleration = 90f;

    [Header("Ground")]
    [SerializeField] private Transform groundCheck;
    [SerializeField, Range(0.05f, 0.5f)] private float groundCheckRadius = 0.15f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Jump")]
    [SerializeField, Range(0.5f, 8f)] private float jumpHeight = 3f;
    [SerializeField, Range(0.5f, 8f)] private float baseGravity = 3f;

    [Header("Tuning")]
    [SerializeField, Range(0f, 0.4f)] private float coyoteTime = 0.12f;
    [SerializeField, Range(0f, 0.4f)] private float jumpBufferTime = 0.15f;
    [SerializeField, Range(1f, 6f)] private float fallMultiplier = 2.2f;
    [SerializeField, Range(1f, 8f)] private float lowJumpMultiplier = 3.5f;

    [Header("Visuals")]
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Juice")]
    [SerializeField] private CameraShake cameraShake;
    [SerializeField, Range(2f, 30f)] private float hardLandingSpeed = 12f;
    [SerializeField, Range(0.02f, 0.5f)] private float landingShakeDuration = 0.1f;
    [SerializeField, Range(0.02f, 0.5f)] private float landingShakeMagnitude = 0.1f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] stepClips;
    [SerializeField, Range(0.1f, 1f)] private float stepInterval = 0.32f;
    [SerializeField] private AudioClip jumpClip;
    [SerializeField, Range(0f, 0.3f)] private float pitchVariance = 0.08f;
    [SerializeField, Range(0f, 1f)] private float audioVolume = 0.35f;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    private Rigidbody2D body;
    private PlayerControls controls;
    private float moveInput;

    private bool isGrounded;
    private float coyoteCounter;
    private float jumpBufferCounter;
    private float stepTimer;

    private AudioClip generatedStepClip;
    private AudioClip generatedJumpClip;

    private float JumpVelocity =>
        Mathf.Sqrt(2f * Mathf.Abs(Physics2D.gravity.y) * baseGravity * jumpHeight);

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        controls = new PlayerControls();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.volume = audioVolume;

        generatedStepClip = CreateStepClip();
        generatedJumpClip = CreateJumpClip();
    }

    private void OnEnable() => controls.Player.Enable();
    private void OnDisable() => controls.Player.Disable();
    private void OnDestroy() => controls.Dispose();

    private void Update()
    {
        float previousVerticalVelocity = body.linearVelocity.y;
        bool wasGrounded = isGrounded;

        isGrounded = CheckGround();

        if (!wasGrounded && isGrounded && previousVerticalVelocity <= -hardLandingSpeed)
        {
            if (cameraShake != null)
                cameraShake.Shake(landingShakeDuration, landingShakeMagnitude);
        }

        bool canRefill = isGrounded && body.linearVelocity.y <= 0.01f;
        coyoteCounter = canRefill ? coyoteTime : coyoteCounter - Time.deltaTime;

        jumpBufferCounter = controls.Player.Jump.WasPressedThisFrame()
            ? jumpBufferTime
            : jumpBufferCounter - Time.deltaTime;

        if (jumpBufferCounter > 0f && coyoteCounter > 0f)
        {
            Jump();
            jumpBufferCounter = 0f;
            coyoteCounter = 0f;
        }

        moveInput = controls.Player.Move.ReadValue<Vector2>().x;
        HandleFootsteps();
        UpdateVisuals();
    }

    private void FixedUpdate()
    {
        float target = moveInput * maxSpeed;
        float rate = Mathf.Abs(target) > 0.01f ? acceleration : deceleration;

        float x = Mathf.MoveTowards(body.linearVelocity.x, target, rate * Time.fixedDeltaTime);

        if (body.linearVelocity.y < 0f)
            body.gravityScale = baseGravity * fallMultiplier;
        else if (body.linearVelocity.y > 0f && !controls.Player.Jump.IsPressed())
            body.gravityScale = baseGravity * lowJumpMultiplier;
        else
            body.gravityScale = baseGravity;

        body.linearVelocity = new Vector2(x, body.linearVelocity.y);
    }

    private void Jump()
    {
        body.gravityScale = baseGravity;
        body.linearVelocity = new Vector2(body.linearVelocity.x, JumpVelocity);

        AudioClip clipToPlay = jumpClip != null ? jumpClip : generatedJumpClip;
        PlayClip(clipToPlay);
    }

    private void HandleFootsteps()
    {
        bool isMovingOnGround = isGrounded && Mathf.Abs(body.linearVelocity.x) > 0.2f;

        if (!isMovingOnGround)
        {
            stepTimer = 0f;
            return;
        }

        stepTimer -= Time.deltaTime;
        if (stepTimer <= 0f)
        {
            stepTimer = stepInterval;
            AudioClip clipToPlay = GetStepClip();
            PlayClip(clipToPlay);
        }
    }

    private AudioClip GetStepClip()
    {
        if (stepClips != null && stepClips.Length > 0)
        {
            AudioClip selected = stepClips[Random.Range(0, stepClips.Length)];
            if (selected != null)
                return selected;
        }

        return generatedStepClip;
    }

    private void PlayClip(AudioClip clip)
    {
        if (audioSource == null || clip == null) return;
        audioSource.pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
        audioSource.PlayOneShot(clip);
    }

    private AudioClip CreateStepClip()
    {
        int sampleRate = 44100;
        float duration = 0.055f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            float envelope = Mathf.Pow(1f - t, 2.5f);
            float freq = Mathf.Lerp(175f, 65f, t);
            float tone = Mathf.Sin(2f * Mathf.PI * freq * (i / (float)sampleRate));
            float noise = Random.Range(-0.35f, 0.35f);
            samples[i] = (tone * 0.7f + noise * 0.3f) * envelope;
        }

        AudioClip clip = AudioClip.Create("GeneratedStep", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateJumpClip()
    {
        int sampleRate = 44100;
        float duration = 0.13f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            float envelope = 1f - t;
            float freq = Mathf.Lerp(210f, 520f, t);
            float squareWave = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * freq * (i / (float)sampleRate)));
            samples[i] = squareWave * 0.25f * envelope;
        }

        AudioClip clip = AudioClip.Create("GeneratedJump", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private bool CheckGround() =>
        Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

    private void UpdateVisuals()
    {
        if (animator != null)
            animator.SetFloat(SpeedHash, Mathf.Abs(body.linearVelocity.x));

        if (spriteRenderer != null && Mathf.Abs(moveInput) > 0.01f)
            spriteRenderer.flipX = moveInput < 0f;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Application.isPlaying && isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}