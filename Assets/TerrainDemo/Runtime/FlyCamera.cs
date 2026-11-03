using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerrainDemo
{
    public enum CameraMode
    {
        Orbit,
        Free,
    }

    public class FlyCamera : MonoBehaviour
    {
        [SerializeField] private CameraMode mode = CameraMode.Orbit;

        [SerializeField] private float moveSpeed = 30f;

        [SerializeField] private float lookSpeed = 0.25f;

        [SerializeField] private float orbitKeySpeed = 60f;

        [SerializeField] private float zoomStep = 0.1f;

        [SerializeField] private TerrainGenerator target;

        public CameraMode Mode => mode;

        public event Action ModeChanged;

        private float _yaw;
        private float _pitch;

        private float _orbitYaw = 45f;
        private float _orbitPitch = 35f;
        private float _orbitDistance = 250f;
        private Vector3 _orbitCenter;
        private float _orbitSize;

        private void Start()
        {
            Vector3 euler = transform.eulerAngles;
            _yaw = euler.y;
            _pitch = NormalizePitch(euler.x);

            if (target == null) target = FindFirstObjectByType<TerrainGenerator>();
            if (target != null) target.Generated += OnTerrainGenerated;
            Retarget(true);
            if (mode == CameraMode.Orbit) ApplyOrbit();
        }

        private void OnDestroy()
        {
            if (target != null) target.Generated -= OnTerrainGenerated;
        }

        public void ToggleMode()
        {
            SetMode(mode == CameraMode.Orbit ? CameraMode.Free : CameraMode.Orbit);
        }

        public void SetMode(CameraMode next)
        {
            if (next == mode) return;
            mode = next;
            if (mode == CameraMode.Orbit)
            {
                Vector3 offset = transform.position - _orbitCenter;
                if (offset.sqrMagnitude > 1e-4f)
                {
                    _orbitDistance = ClampDistance(offset.magnitude);
                    Quaternion look = Quaternion.LookRotation(-offset.normalized, Vector3.up);
                    _orbitYaw = look.eulerAngles.y;
                    _orbitPitch = Mathf.Clamp(NormalizePitch(look.eulerAngles.x), -89f, 89f);
                }
                ApplyOrbit();
            }
            else
            {
                Vector3 euler = transform.eulerAngles;
                _yaw = euler.y;
                _pitch = NormalizePitch(euler.x);
            }
            ModeChanged?.Invoke();
        }

        private void OnTerrainGenerated() => Retarget(false);

        private void Retarget(bool force)
        {
            if (target == null) return;
            var renderer = target.GetComponent<Renderer>();
            if (renderer == null) return;
            Bounds bounds = renderer.bounds;
            if (bounds.size == Vector3.zero) return;

            float size = bounds.extents.magnitude;
            bool resized = _orbitSize <= 0f || Mathf.Abs(size - _orbitSize) > _orbitSize * 0.25f;
            _orbitCenter = bounds.center;
            if (force || resized)
            {
                _orbitDistance = size * 2.2f;
                _orbitSize = size;
            }
            if (mode == CameraMode.Orbit) ApplyOrbit();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;

            if (mode == CameraMode.Orbit) UpdateOrbit(keyboard, mouse);
            else UpdateFree(keyboard, mouse);
        }

        private void UpdateOrbit(Keyboard keyboard, Mouse mouse)
        {
            if (!TerrainDemoUI.IsTyping)
            {
                float step = orbitKeySpeed * Time.deltaTime;
                if (keyboard.aKey.isPressed) _orbitYaw += step;
                if (keyboard.dKey.isPressed) _orbitYaw -= step;
                if (keyboard.eKey.isPressed) _orbitPitch += step;
                if (keyboard.qKey.isPressed) _orbitPitch -= step;
                if (keyboard.wKey.isPressed) _orbitDistance *= 1f - Time.deltaTime;
                if (keyboard.sKey.isPressed) _orbitDistance *= 1f + Time.deltaTime;
            }

            if (mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue() * lookSpeed;
                _orbitYaw += delta.x;
                _orbitPitch -= delta.y;
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0f && !TerrainDemoUI.PointerOverUI)
            {
                _orbitDistance *= 1f - Mathf.Sign(scroll) * zoomStep;
            }

            _orbitPitch = Mathf.Clamp(_orbitPitch, -89f, 89f);
            _orbitDistance = ClampDistance(_orbitDistance);
            ApplyOrbit();
        }

        private void UpdateFree(Keyboard keyboard, Mouse mouse)
        {
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

        private void ApplyOrbit()
        {
            Quaternion rotation = Quaternion.Euler(_orbitPitch, _orbitYaw, 0f);
            transform.position = _orbitCenter + rotation * new Vector3(0f, 0f, -_orbitDistance);
            transform.rotation = rotation;
        }

        private float ClampDistance(float distance)
        {
            float size = _orbitSize > 0f ? _orbitSize : 100f;
            return Mathf.Clamp(distance, size * 0.6f, size * 8f);
        }

        private static float NormalizePitch(float pitch) => pitch > 180f ? pitch - 360f : pitch;
    }
}
