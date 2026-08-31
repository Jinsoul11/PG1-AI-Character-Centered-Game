using UnityEngine;

/// <summary>
/// 玩家移动控制器，使用 CharacterController 实现 WASD 移动 / 鼠标旋转。
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("移动参数")]
    public CharacterController characterController; // 若未赋值，自动获取 CharacterController 组件
    public float moveSpeed = 5f;
    public float rotateSpeed = 2f; // 鼠标旋转灵敏度

    private bool _isMoveLocked = false;
    private bool _isLookLocked = false;

    private void Awake()
    {
        SceneTravelService.RegisterPlayerTransform(transform);
    }

    private void Start()
    {
        // 若未手动指定 CharacterController，在 Start 时自动查找
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }
    }

    private void Update()
    {
        if (!_isMoveLocked)
            HandleMovement();

        if (!_isLookLocked)
            HandleRotation();
    }

    /// <summary>
    /// 处理 WASD 移动，通过 CharacterController.Move 驱动。
    /// </summary>
    private void HandleMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 move = transform.right * horizontal + transform.forward * vertical;
        if (move.sqrMagnitude > 1f)
            move.Normalize();

        move *= moveSpeed;
        move.y = Physics.gravity.y;
        characterController.Move(move * Time.deltaTime);
    }

    /// <summary>
    /// 处理鼠标水平旋转。
    /// </summary>
    private void HandleRotation()
    {
        float mouseX = Input.GetAxis("Mouse X") * rotateSpeed;
        transform.Rotate(0, mouseX, 0);
    }

    // ===================== 移动 / 视角锁定（对话、手机等） =====================
    /// <summary>
    /// 锁定/解锁玩家移动。
    /// </summary>
    /// <param name="lockMovement">true=禁止移动，false=恢复移动</param>
    public void SetMoveLock(bool lockMovement)
    {
        _isMoveLocked = lockMovement;

        if (lockMovement && characterController != null)
            characterController.Move(Vector3.zero);
    }

    /// <summary>
    /// 锁定/解锁水平视角（鼠标 X）；与 <see cref="SetMoveLock"/> 不同，对话时通常只限制位移不隐藏角色。
    /// </summary>
    public void SetLookLock(bool lockLook)
    {
        _isLookLocked = lockLook;
    }

    /// <summary>
    /// 当前是否禁止移动（供 UI 等查询）。
    /// </summary>
    public bool IsMoveLocked => _isMoveLocked;

    public bool IsLookLocked => _isLookLocked;
}
