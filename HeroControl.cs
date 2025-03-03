using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent (typeof(BoxCollider2D))]
[RequireComponent(typeof(TrailRenderer))]
public class HeroControl : MonoBehaviour
{
    public enum HeroStatus 
    {
        Default,
        OnWall,
        InAir,
        Siting,
        CutScene,
        Jumping
    }
    [SerializeField] Vector2 rbVelocity;
    [Header("Force")]
    public Vector2 forceVector;
    [SerializeField] private float timerForce;

    [Header("Global")]
    [Tooltip("������, �������� �������������")] public HeroStatus hStatus = HeroStatus.Default;

    [Header("Input")]
    private Vector2 inputVector;
    [SerializeField] private Vector2 FadeVector;

    [Header("Move")]
    public float speed = 1.0f;
    [SerializeField] private float moveStrenght = 1f;
    public float Hspeed = 0f;
    private float previosPosition;

    [Header("Derection")]
    public bool IstErRight = true;

    [Header("Jump Global")]
    private float _jumpTime = 0f;
    private Vector2 ItogJump;
    public float grabBlockTimer = 0.2f;
    private float grabBlockTime = 0.00f;

    [Header("Jump from ground")]
    [SerializeField] float jumpForce = 5.0f;
    public float jumpTimer = 0.2f;

    [Header("Jump from wall")]
    [SerializeField] float jumpVeticalForce = 5.0f;
    [SerializeField] Vector2 jumpAngle = new Vector2(3f, 3f);
    [SerializeField] private float jumpAngleTimer = 0.4f;

    [Header("Dash")]
    private int dashCount = 1;
    public int DashCountMax = 0;
    public float dashSpeed = 10.0f;
    [SerializeField] private float dashDuration = 0.1f;
    public float dashCoolDown = 0.1f;
    [Range(0f, 1f)] public float timeChange = 1f;
    private bool isDashing = false;
    private bool canDash = true;
    private TrailRenderer trail;
    [SerializeField] ParticleSystem dashParticles_right;
    [SerializeField] ParticleSystem dashParticles_left;
    private Coroutine dashCoroutine;

    [Header("Move in air")]
    [SerializeField] private float airSpeedMultyplier = 1.1f;
    [SerializeField] private float airMoveStrenght = 1f;

    [Header("Grab")]
    [SerializeField] private bool IsGrabing = false;

    [Header("Move on walls")]
    [SerializeField] private float WallMoveUpDownSpeed = 1.0f;

    [Header("Gravity")]
    [SerializeField] private float baseGravity = 1.0f;
    [SerializeField] private float maxFallSpeed = 10.0f;
    [SerializeField] private float fallSpeedMultyplier;

    [Header("Components")]
    [SerializeField] private Animator anim;
    private Rigidbody2D rb;
    

    [Header("Ground_cheak")]
    [SerializeField] private Transform groundCheakPos;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.5f, 0.05f);
    [SerializeField] private LayerMask groundLayer;

    [Header("Wall_cheak")]
    [SerializeField] private float x_right;
    [SerializeField] private float x_left;
    [SerializeField] private Transform wallCheakPos_up;
    [SerializeField] private Transform wallCheakPos_down;
    [SerializeField] private Transform wallCheakDanger;
    [SerializeField] private Vector2 wallCheckSize = new Vector2(0.5f, 0.05f);
    [SerializeField] private Vector2 wallCheckUpSize = new Vector2(0.5f, 0.05f);
    [SerializeField] private Vector2 wallCheckDangerSize = new Vector2(0.5f, 0.05f);
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private LayerMask dangerLayer;

    [Header("Steps")]
    [SerializeField] private ParticleSystem stepParticle;
    public float StepAudioTimer = 0.2f;
    private float StepAudioTime = 0.0f;
    public void SetStatus(HeroStatus status)
    {
        hStatus = status;
    }
    public void MoveInput(InputAction.CallbackContext context)
    {
        inputVector.x = Mathf.Round(context.ReadValue<Vector2>().x);
        inputVector.y = Mathf.Round(context.ReadValue<Vector2>().y);
    }
    public Vector2 CheckJump() 
    {       
        if (hStatus == HeroStatus.OnWall && inputVector.x == (IstErRight ? -1 : 1))
            ItogJump = new Vector2(IstErRight ? -jumpAngle.x : jumpAngle.x, jumpAngle.y); // angle jump from wall
        else if (hStatus == HeroStatus.OnWall && (inputVector.x == (IstErRight ? 1 : -1) || inputVector.x == 0) )
            ItogJump = new Vector2(0, jumpVeticalForce); //vertical jump from wall
        else
            ItogJump = new Vector2(0, jumpForce); // simple jump
        return ItogJump;
    }
    public void Jump(InputAction.CallbackContext context) 
    {
        if (context.performed) 
        {
            if (hStatus == HeroStatus.Default || hStatus == HeroStatus.OnWall)
            {
                CheckJump();
                CamManager.CameraShake?.Invoke(0.5f, 0.05f, 0.05f);
                rb.gravityScale = baseGravity;
                hStatus = HeroStatus.Jumping;
                if(ItogJump.x == 0)
                    _jumpTime = jumpTimer;
                else
                    _jumpTime = jumpAngleTimer;
                grabBlockTime = grabBlockTimer;
            }
        } 
        else
        {
            ItogJump.y = ItogJump.y / 2;
        }
    }
    public void TimerOfJump()
    {
        if (_jumpTime > 0)
        {
            _jumpTime -= Time.deltaTime;
            if (_jumpTime <= 0)
            {
                hStatus = HeroStatus.Default;
                if(ItogJump.x != 0)
                    FadeVector.x = ItogJump.x;
                ItogJump = Vector2.zero;
            }
        }      
    }
    public void Grab(InputAction.CallbackContext context)
    {
            IsGrabing = context.performed;
    }
    public void GrabTimer()
    {
        if (grabBlockTime > 0)
        {
            grabBlockTime -= Time.deltaTime;
        }
        
    }
    private bool CanGrab() 
    {
        if (grabBlockTime > 0)
            return false;
        else
            return IsGrabing;
    }
    public void Dash(InputAction.CallbackContext context)
    {
        if (context.performed && canDash)
        {
            if(DashCountMax == 0 || (DashCountMax != 0 && dashCount > 0))
            {
                if ((hStatus == HeroStatus.Default || hStatus == HeroStatus.InAir || hStatus == HeroStatus.Jumping))
                {
                    SoundManager.Play("Dash");
                    StartCoroutine(DashCoroutine(IstErRight ? 1f : -1f));
                }
                else if (hStatus == HeroStatus.OnWall)
                {
                    if((CheckJump().x > 0 && !IstErRight) || (CheckJump().x < 0 && IstErRight))
                    {
                        SoundManager.Play("Dash");
                        StartCoroutine(DashCoroutine(CheckJump().x >= 0 ? 1f : -1f));
                    }
                }
            }
            if (DashCountMax != 0)
                dashCount--;       
        }
    }
    public void StopDash()
    {
        StopCoroutine(DashCoroutine(IstErRight? 1 : -1));
    }
    private IEnumerator DashCoroutine(float dashdirection)
    {
        Time.timeScale = timeChange;
        anim.SetBool("Dashing",true);
        
        if (dashdirection > 0)
            dashParticles_right.Play();
        else 
            dashParticles_left.Play();
        canDash = false;
        isDashing = true;
        grabBlockTime = 0.01f;
        trail.emitting = true;
        rb.velocity = new Vector2(dashdirection * dashSpeed, rb.velocity.y);
        yield return new WaitForSeconds(dashDuration);
        rb.velocity = new Vector2(0, 0);
        isDashing = false;
        trail.emitting = false;
        Time.timeScale = 1f;
        anim.SetBool("Dashing", false);
        yield return new WaitForSeconds(dashCoolDown);
        canDash = true;
        
        SetForce(IstErRight ? 4f : -4f, 1f);
        
    } 
    private void Move() 
    {
        switch (hStatus) 
        {
            case HeroStatus.Default:
                if (inputVector.x != FadeVector.x)
                {
                    FadeVector.x = Mathf.MoveTowards(FadeVector.x, inputVector.x, Time.deltaTime * moveStrenght);
                }
                if (!isDashing)
                    rb.velocity = new Vector2(FadeVector.x * speed + forceVector.x, rb.velocity.y);
                if (DashCountMax != 0)
                    dashCount = DashCountMax;
                break;

            case HeroStatus.InAir:
                if (inputVector.x != FadeVector.x)
                    FadeVector.x = Mathf.MoveTowards(FadeVector.x, inputVector.x, Time.deltaTime * airMoveStrenght);
                if (!isDashing)
                    rb.velocity = new Vector2(FadeVector.x * speed * airSpeedMultyplier + forceVector.x, rb.velocity.y);
                break;  
            case HeroStatus.Siting:
                FadeVector = Vector2.zero;
                rb.velocity = Vector2.zero;
                break;
            case HeroStatus.OnWall:
                if(DragUpDanger() && DragUpWall())
                {
                    if (inputVector.y > 0)
                    {
                        rb.velocity = new Vector2(0, 0);
                    }
                    else
                    {
                        rb.velocity = new Vector2(rb.velocity.x, inputVector.y * WallMoveUpDownSpeed);
                    }
                }
                else if (!DragUpDanger() && DragDownWall() && !DragUpWall()) //climb up
                {
                    
                    if (inputVector.y > 0) 
                    {
                        rb.velocity = new Vector2( IstErRight? speed : -speed, inputVector.y * WallMoveUpDownSpeed);
                    }
                    else
                    {
                        rbVelocity = new Vector2(0, 0);
                    }
                }
                else if (DragUpWall()) //move or stop
                {
                    if (inputVector.y == 0)
                        rb.velocity = new Vector2(0, 0);
                    else
                        rb.velocity = new Vector2(rb.velocity.x, inputVector.y * WallMoveUpDownSpeed);
                }


                break;
            case HeroStatus.Jumping:
                if(ItogJump.x == 0)
                {
                    if (inputVector.x != FadeVector.x)
                    {
                        FadeVector.x = Mathf.MoveTowards(FadeVector.x, inputVector.x, Time.deltaTime * airMoveStrenght);
                    }
                    if (!isDashing)
                        rb.velocity = new Vector2(FadeVector.x * speed * airSpeedMultyplier + forceVector.x, ItogJump.y);
                }
                else
                    if(!isDashing)
                        rb.velocity = new Vector2((ItogJump.x * speed * airSpeedMultyplier) + FadeVector.x + forceVector.x, ItogJump.y);
                break;
        }
    }
    public void SetForce(float force, float time) 
    {
        timerForce = time;
        forceVector.x = force;
    }
    public void ForceTime() 
    {
        if (Hspeed == 0)
        {
            timerForce = 0;
            forceVector.x = 0;
        }
        else if (hStatus == HeroStatus.Default || hStatus == HeroStatus.OnWall)
        {
            timerForce = 0;
            forceVector.x = 0;
        }
        else if (hStatus == HeroStatus.InAir && inputVector.x != 0) 
        {
            timerForce = 0;
        }
        if (timerForce > 0) 
        {
            timerForce -= Time.deltaTime;
        }
        else if (forceVector.x != 0) 
        {
            forceVector.x = Mathf.MoveTowards(forceVector.x, 0, 1);
        }
        
    }
    private void Gravity()
    {
        if (hStatus != HeroStatus.OnWall)
        {
            if (rb.velocity.y < 0)
            {
                rb.gravityScale = baseGravity * fallSpeedMultyplier;
                rb.velocity = new Vector2(rb.velocity.x, Mathf.Max(rb.velocity.y, -maxFallSpeed));
            }
            else
            {
                rb.gravityScale = baseGravity;
            }
        }
        else
            rb.gravityScale = 0;
    }
    void UpdateDirection()
    {
        if (hStatus != HeroStatus.OnWall && hStatus != HeroStatus.CutScene)
        {
            if (inputVector.x != 0 && inputVector.x > 0.00f)
            {
                IstErRight = true;
                wallCheakPos_up.transform.localPosition = new Vector3(x_right, wallCheakPos_up.localPosition.y, 0f);
                wallCheakPos_down.transform.localPosition = new Vector3(x_right, wallCheakPos_down.localPosition.y, 0f);
                wallCheakDanger.transform.localPosition = new Vector3(x_right, wallCheakDanger.localPosition.y, 0f);
            }
            else if (inputVector.x != 0 && inputVector.x < 0.00f)
            {
                IstErRight = false;
                wallCheakPos_up.transform.localPosition = new Vector3(x_left, wallCheakPos_up.localPosition.y, 0f);
                wallCheakPos_down.transform.localPosition = new Vector3(x_left, wallCheakPos_down.localPosition.y, 0f);
                wallCheakDanger.transform.localPosition = new Vector3(x_left, wallCheakDanger.localPosition.y, 0f);
            }
        }
    }
    void SetDirection(bool right)
    {
        if (right)
        {
            IstErRight = true;
            wallCheakPos_up.transform.localPosition = new Vector3(x_right, wallCheakPos_up.localPosition.y, 0f);
            wallCheakPos_down.transform.localPosition = new Vector3(x_right, wallCheakPos_down.localPosition.y, 0f);
        }
        else
        {
            IstErRight = false;
            wallCheakPos_up.transform.localPosition = new Vector3(x_left, wallCheakPos_up.localPosition.y, 0f);
            wallCheakPos_down.transform.localPosition = new Vector3(x_left, wallCheakPos_down.localPosition.y, 0f);
        } 
    }
    private HeroStatus GetStatus() 
    {
        if(hStatus == HeroStatus.CutScene)
            return HeroStatus.CutScene;
        else if (IsGrounded() && inputVector.y == -1 && hStatus != HeroStatus.OnWall)
            return HeroStatus.Siting;
        else if (CanGrab() && (DragUpWall() || DragDownWall()))
            return HeroStatus.OnWall;
        else if (hStatus == HeroStatus.Jumping)
            return HeroStatus.Jumping;
        else if (!IsGrounded())
            return HeroStatus.InAir;
        else
            return HeroStatus.Default;
    } 
    private bool IsGrounded()
    {
        return Physics2D.OverlapBox(groundCheakPos.position, groundCheckSize, 0, groundLayer);
    }
    private bool DragUpWall()
    {
        return (Physics2D.OverlapBox(wallCheakPos_up.position, wallCheckSize, 0, wallLayer));
    }
    private bool DragDownWall()
    {
        return (Physics2D.OverlapBox(wallCheakPos_down.position, wallCheckSize, 0, wallLayer));
    }
    private bool DragUpDanger()
    {
        return (Physics2D.OverlapBox(wallCheakDanger.position, wallCheckDangerSize, 0, dangerLayer)) && !(Physics2D.OverlapBox(wallCheakDanger.position, wallCheckDangerSize, 0, groundLayer));
    }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        trail = GetComponent<TrailRenderer>();
    }
    void Update()
    {
        TimerOfJump();
        hStatus = GetStatus();
        Move();
        UpdateDirection();
        Gravity();
        ForceTime();
        GrabTimer();
       
    }   
    private void FixedUpdate()
    {
        rbVelocity = rb.velocity;
        Hspeed = Mathf.Abs((transform.position.x - previosPosition) / Time.deltaTime);
        previosPosition = transform.position.x;
        if (Hspeed > 0.1f && hStatus != HeroStatus.InAir)
        {
            if (StepAudioTime < 0.0f)
            {
                stepParticle.Play();
                try
                {
                    SoundManager.Play("S_forest");
                }
                catch
                {
                    Debug.LogError("��� ������ �� ��������� ������");
                }

                StepAudioTime = StepAudioTimer;
            }
            else
                StepAudioTime -= Time.deltaTime;
            
        }
        anim.SetBool("Walking", Hspeed > 0.1f);
        anim.SetBool("IstErRight", IstErRight);
        anim.SetBool("OnGround", IsGrounded());
        anim.SetBool("Sitting", hStatus == HeroStatus.Siting);
        anim.SetBool("OnWallGrabing", hStatus == HeroStatus.OnWall);
        anim.SetBool("LookingFromWall", hStatus == HeroStatus.OnWall && ((IstErRight && inputVector.x == -1) || (!IstErRight && inputVector.x == 1)));
        anim.SetFloat("Vertical", inputVector.y);

    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.white;
        //ground
        Gizmos.DrawCube(groundCheakPos.position, groundCheckSize);

        Gizmos.color = Color.yellow;
        //right
        Gizmos.DrawCube(transform.TransformPoint(new Vector3(x_right, wallCheakPos_up.localPosition.y, 0)), wallCheckUpSize);
        Gizmos.DrawCube(transform.TransformPoint(new Vector3(x_right, wallCheakPos_down.localPosition.y, 0)), wallCheckSize);
        
        //left
        Gizmos.DrawCube(transform.TransformPoint(new Vector3(x_left, wallCheakPos_up.localPosition.y, 0)), wallCheckUpSize);
        Gizmos.DrawCube(transform.TransformPoint(new Vector3 (x_left, wallCheakPos_down.localPosition.y, 0)), wallCheckSize);
        

        Gizmos.color = Color.red;
        Gizmos.DrawCube(transform.TransformPoint(new Vector3(x_left, wallCheakDanger.localPosition.y, 0)), wallCheckDangerSize);
        Gizmos.DrawCube(transform.TransformPoint(new Vector3(x_right, wallCheakDanger.localPosition.y, 0)), wallCheckDangerSize);
    }
}
