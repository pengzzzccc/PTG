// ─────────────────────────────────────────────────────────────────────────────
// File: Runtime/FlyCamera.cs
// Module: Procedural terrain generation · Unity runtime
// Status: Fully implemented (built on the Input System package)
// ─────────────────────────────────────────────────────────────────────────────

using UnityEngine;
using UnityEngine.InputSystem;

namespace TerrainDemo
{
    /// <summary>
    /// Free-flying observer camera: WASD to move, QE for elevation, hold the right mouse
    /// button and drag to look around — for inspecting the generated mountain.
    /// </summary>
    public class FlyCamera : MonoBehaviour
    {
        /// <summary>Movement speed in meters per second.</summary>
        [SerializeField] private float moveSpeed = 30f;

        /// <summary>Look sensitivity in degrees per pixel, applied while the right button is held.</summary>
        [SerializeField] private float lookSpeed = 0.25f;

        /// <summary>Yaw around the world Y axis, accumulated from mouse X deltas.</summary>
        private float _yaw;

        /// <summary>Pitch around the local X axis, clamped to ±89° to avoid flipping.</summary>
        private float _pitch;

        private void Start()
        {
            // Initialize from the current orientation so the editor-placed camera
            // position carries over seamlessly.
            Vector3 euler = transform.eulerAngles;
            _yaw = euler.y;
            _pitch = euler.x;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;

            // Movement: WASD along the camera's own axes, QE along the world vertical;
            // multiplied by deltaTime so speed is frame-rate independent.
            Vector3 move = Vector3.zero;
            if (keyboard.wKey.isPressed) move += transform.forward;
            if (keyboard.sKey.isPressed) move -= transform.forward;
            if (keyboard.dKey.isPressed) move += transform.right;
            if (keyboard.aKey.isPressed) move -= transform.right;
            if (keyboard.eKey.isPressed) move += Vector3.up;
            if (keyboard.qKey.isPressed) move -= Vector3.up;
            if (move.sqrMagnitude > 0f)
            {
                transform.position += move.normalized * (moveSpeed * Time.deltaTime);
            }

            // Looking: while the right button is held, mouse deltas convert to yaw/pitch;
            // pitch is clamped to avoid gimbal flipping.
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

// ── Implementation notes ─────────────────────────────────────────────────────
//
// Purpose. Evaluating the terrain honestly requires flying around it: structural
// fidelity means checking that ridge lines connect, detail fidelity means zooming into
// surface transitions. This component does exactly one thing and is fully decoupled
// from the generation pipeline — drop it on any camera and it works.
//
// Principle. Movement reads keyboard input each frame into a local-space direction
// vector (forward/back along transform.forward, left/right along transform.right, QE
// along the world Y), scaled by speed and deltaTime so speed is frame-rate independent.
// Rotation uses the common yaw/pitch scheme: with the right button held, mouse deltas
// convert to yaw around the world Y and pitch around the local X, clamped to ±89°;
// angles accumulate in private fields rather than being read back from Euler angles,
// avoiding solver jitter. Input goes through the Input System package (bundled with the
// Unity 6 template and the recommended backend; the project enables both backends):
// Keyboard.current / Mouse.current read device state directly, with no asset setup.
//
// Approach. Stateless and pipeline-independent by design — attach and use; movement and
// rotation are verified independently. Speed and sensitivity are serialized fields to
// be tuned against the 200-meter scene scale once the mountain exists.
// ─────────────────────────────────────────────────────────────────────────────
