using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
    // This player class is just a simple player controller script and everything here is not necessary for online play, replace this with your own controller script.
    // THAT BEING SAID, MAKE SURE THAT ANY OBJECTS WITH ANIMATIONS OR THAT CHANGE POSITIONS HAVE THE "Photon View" COMPONENT AND THE CORRESPONDING "Photon ____ View" COMPONENTS DEPENDING ON WHAT NEEDS TO GO ACROSS NETWORKS.
    // LOOK AT EACH COMPONENT AND SEE IF IT NEEDS TO BE ALTERED.
    // MAKE SURE THE PLAYER THAT NEEDS TO BE INSTANTIATED GOES IN THE "Resources" FOLDER SO THE NETWORK CAN PROPERLY TRACK IT
    public float moveSpeed;
    public PlayerControls controls;
    public Animator anim;

    public Renderer rend;

    private InputAction colorChange;
    private InputAction move;

    void OnEnable()
    {
        colorChange = controls.Player.ColorChange;
        move = controls.Player.Move;

        colorChange.Enable();
        move.Enable();
    }
    void OnDisable()
    {
        colorChange.Disable();
        move.Disable();
    }

    void Awake()
    {
        controls = new PlayerControls();
        rend = gameObject.GetComponent<Renderer>();
    }

    void Update()
    {
        anim.SetBool("Color Change", colorChange.ReadValue<float>() > 0);

            Vector3 movement = new Vector3(move.ReadValue<Vector2>().x, 0, move.ReadValue<Vector2>().y);
        transform.Translate(movement * Time.deltaTime * moveSpeed);
    }
}
