using UnityEngine;
using UnityEngine.InputSystem;

public class Camera_Controller : MonoBehaviour
{
    // ============================================================
    // SERIALIZED FIELDS
    // ============================================================

    [Header("Focus")]
    [Tooltip("Point the camera orbits around. Leave at (0,0,0) to orbit " +
             "around world origin, or set to your board center.")]
    [SerializeField] private Vector3 Focus_Point = Vector3.zero;
    [SerializeField] private Transform Board_Transform;

    [Header("Distance")]
    [Tooltip("Closest the camera can get to the focus point.")]
    [SerializeField] private float Min_Distance = 6f;

    [Tooltip("Farthest the camera can get from the focus point.")]
    [SerializeField] private float Max_Distance = 18f;

    [Tooltip("How far one scroll notch moves the camera. Larger = snappier.")]
    [SerializeField] private float Zoom_Speed = 8f;

    [Header("Rotation")]
    [Tooltip("Degrees of yaw/pitch per pixel of mouse drag.")]
    [SerializeField] private float Rotate_Speed = 0.25f;

    [Tooltip("Lowest pitch angle above the horizon. 0 = horizontal, 90 = top-down.")]
    [Range(5f, 89f)]
    [SerializeField] private float Min_Pitch = 25f;

    [Tooltip("Highest pitch angle above the horizon.")]
    [Range(5f, 89f)]
    [SerializeField] private float Max_Pitch = 80f;

    [Tooltip("If true, yaw is unbounded. If false, yaw is clamped to the " +
             "authored yaw plus/minus Yaw_Range_Degrees.")]
    [SerializeField] private bool Allow_Full_Yaw = true;

    [Tooltip("Only used when Allow_Full_Yaw is false.")]
    [SerializeField] private float Yaw_Range_Degrees = 90f;

    [Header("Reset")]
    [SerializeField] private KeyCode Reset_Key = KeyCode.Home;

    [Header("Input")]
    [SerializeField] private InputActionAsset InputActions;
    [SerializeField] private string Action_Map_Name = "Camera";

    // ============================================================
    // STATE
    // ============================================================

    private InputAction Zoom_Action;
    private InputAction Modifier_Action;
    private InputAction Mouse_Delta_Action;
    private InputAction Rotate_Button_Action;

    private Vector3 Home_Position;
    private Quaternion Home_Rotation;
    private float Home_Distance;
    private float Home_Yaw;
    private float Home_Pitch;

    private float Current_Yaw;
    private float Current_Pitch;
    private float Current_Distance;

    private bool Is_Rotating;

    public bool Is_Manipulating => Is_Rotating;

    // ============================================================
    // LIFECYCLE
    // ============================================================

    private void Awake()
    {
        Setup_Input();
    }

    private void Start()
    {
        if (Board_Transform != null)
            Focus_Point = Board_Transform.position;

        Home_Position = transform.position;
        Home_Rotation = transform.rotation;

        Vector3 Offset = transform.position - Focus_Point;
        Home_Distance = Offset.magnitude;
        Current_Distance = Mathf.Clamp(Home_Distance, Min_Distance, Max_Distance);

        Vector3 Flat = Vector3.ProjectOnPlane(Offset, Vector3.up);
        Current_Yaw = Mathf.Atan2(Flat.x, Flat.z) * Mathf.Rad2Deg;

        float Horizontal = Flat.magnitude;
        Current_Pitch = Mathf.Atan2(Offset.y, Horizontal) * Mathf.Rad2Deg;

        Home_Yaw = Current_Yaw;
        Home_Pitch = Current_Pitch;

        Apply_Orbit();
    }

    private void OnEnable()
    {
        if (InputActions != null)
            InputActions.FindActionMap(Action_Map_Name)?.Enable();
    }

    private void OnDisable()
    {
        if (InputActions != null)
            InputActions.FindActionMap(Action_Map_Name)?.Disable();
    }

    private void Update()
    {
        if (Input.GetKeyDown(Reset_Key))
            Reset_To_Home();

        Handle_Zoom();
        Handle_Rotate();
    }

    // ============================================================
    // INPUT SETUP
    // ============================================================

    private void Setup_Input()
    {
        if (InputActions == null)
        {
            Debug.LogError("Camera_Controller: InputActions asset not assigned.");
            return;
        }

        InputActionMap Map = InputActions.FindActionMap(Action_Map_Name);
        if (Map == null)
        {
            Debug.LogError($"Camera_Controller: action map '{Action_Map_Name}' not found.");
            return;
        }

        Zoom_Action = Map.FindAction("Zoom");
        Modifier_Action = Map.FindAction("Modifier");
        Mouse_Delta_Action = Map.FindAction("Mouse_Delta");
        Rotate_Button_Action = Map.FindAction("Rotate_Button");

        if (Zoom_Action == null) Debug.LogError("Camera: 'Zoom' action not found.");
        if (Modifier_Action == null) Debug.LogError("Camera: 'Modifier' action not found.");
        if (Mouse_Delta_Action == null) Debug.LogError("Camera: 'Mouse_Delta' action not found.");
        if (Rotate_Button_Action == null) Debug.LogError("Camera: 'Rotate_Button' action not found.");
    }

    // ============================================================
    // ZOOM
    // ============================================================

    private void Handle_Zoom()
    {
        if (Zoom_Action == null) return;

        float Scroll = Zoom_Action.ReadValue<Vector2>().y;
        if (Mathf.Approximately(Scroll, 0f)) return;

        // Scroll up (positive) zooms in, meaning distance decreases.
        // If your wheel feels inverted, flip the sign here.
        float Delta = -Scroll * Zoom_Speed * Time.unscaledDeltaTime;

        Current_Distance = Mathf.Clamp(Current_Distance + Delta, Min_Distance, Max_Distance);
        Apply_Orbit();
    }

    // ============================================================
    // ROTATE
    // ============================================================

    private void Handle_Rotate()
    {
        if (Mouse_Delta_Action == null || Modifier_Action == null || Rotate_Button_Action == null)
            return;

        // Set state based purely on input, before any delta check. This makes
        // Is_Manipulating true on the same frame the click happens, so other
        // components that check it in Update can't act on the same click.
        bool Should_Rotate = Modifier_Action.IsPressed() && Rotate_Button_Action.IsPressed();

        if (Should_Rotate && !Is_Rotating) Is_Rotating = true;
        else if (!Should_Rotate && Is_Rotating) Is_Rotating = false;

        if (!Is_Rotating) return;

        Vector2 Delta = Mouse_Delta_Action.ReadValue<Vector2>();
        if (Delta.sqrMagnitude < 0.0001f) return;

        Current_Yaw += Delta.x * Rotate_Speed;
        Current_Pitch -= Delta.y * Rotate_Speed;

        if (!Allow_Full_Yaw)
        {
            float Min_Yaw = Home_Yaw - Yaw_Range_Degrees;
            float Max_Yaw = Home_Yaw + Yaw_Range_Degrees;
            Current_Yaw = Mathf.Clamp(Current_Yaw, Min_Yaw, Max_Yaw);
        }

        Current_Pitch = Mathf.Clamp(Current_Pitch, Min_Pitch, Max_Pitch);

        Apply_Orbit();
    }

    // ============================================================
    // APPLY
    // ============================================================

    private void Apply_Orbit()
    {
        float Yaw_Rad = Current_Yaw * Mathf.Deg2Rad;
        float Pitch_Rad = Current_Pitch * Mathf.Deg2Rad;

        float Horizontal = Mathf.Cos(Pitch_Rad) * Current_Distance;
        float Vertical = Mathf.Sin(Pitch_Rad) * Current_Distance;

        Vector3 Offset = new Vector3(
            Mathf.Sin(Yaw_Rad) * Horizontal,
            Vertical,
            Mathf.Cos(Yaw_Rad) * Horizontal);

        transform.position = Focus_Point + Offset;
        transform.LookAt(Focus_Point, Vector3.up);
    }

    private void Reset_To_Home()
    {
        Current_Yaw = Home_Yaw;
        Current_Pitch = Home_Pitch;
        Current_Distance = Mathf.Clamp(Home_Distance, Min_Distance, Max_Distance);
        Apply_Orbit();
    }
}