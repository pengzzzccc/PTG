using UnityEngine;
using UnityEngine.InputSystem;

namespace TerrainDemo
{
    public class FlyCamera : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 30f;

        [SerializeField] private float lookSpeed = 0.25f;

        private float _yaw;

        private float _pitch;

        private void Start()
        {
            Vector3 euler = transform.eulerAngles;
            _yaw = euler.y;
            _pitch = euler.x;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;

            Vector3 move = Vector3.zero;
            if (!TerrainDemoUI.IsTyping)
            {
                if (keyboard.wKey.isPressed) move += transform.forward;
                if (keyboard.sKey.isPressed) move -= transform.forward;
                if (keyboard.dKey.isPressed) move += transform.right;
                if (keyboard.aKey.isPressed) move -= transform.right;
                if (keyboard.eKey.isPressed) move += Vector3.up;
                if (keyboard.qKey.isPressed) move -= Vector3.up;
            }
            if (move.sqrMagnitude > 0f)
            {
                transform.position += move.normalized * (moveSpeed * Time.deltaTime);
            }

            if (mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue() * lookSpeed;
                _yaw += delta.x;
                _pitch = Mathf.Clamp(_pitch - delta.y, -89f, 89f);
                transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            }
        }
    }
}
