using System;
using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace XReality.LocalInput.XReality.LocalInput.Scripts
{
    public class UIFollowPlayer : MonoBehaviour
    {
        [HideInInspector] public GameObject playerHeadCamera;
        
        [SerializeField] private bool useMainCamera;
        
        public bool UseMainCamera => useMainCamera;
        
        private bool _offsetStatus;

        // TODO: for late we can adjust the offset based on if the second panel is open or not!
        public bool OffsetStatus
        {
            get => _offsetStatus;
            set => _offsetStatus = value;
        }
        
        public float distance = 1.3f;
        public float yOffset = 0f;
        public float yOffsetToHeadTreshold = 0.3f;
        public float angleOffsetClamp = 45f;
        public Quaternion headForwardRotationOffset = default;

        public event Action Enabled;
        public event Action Disabled;

        private Coroutine yRepositionCoroutine = null;
        private Coroutine hRepositionCoroutine = null;
        private Vector3 hCenter = default;

        private void OnEnable()
        {
            if (useMainCamera)
            {
                if (Camera.main != null)
                {
                    playerHeadCamera = Camera.main.gameObject;
                }
                else
                {
                    Debug.LogError("No main camera found");
                }
            }
            
            SnapToPosition();
            Enabled?.Invoke();
        }

        private void OnDisable()
        {
            yRepositionCoroutine = null;
            hRepositionCoroutine = null;
            Disabled?.Invoke();
        }

        private void LateUpdate()
        {
            if (!playerHeadCamera) return;
            CheckHorizontalThreshold();
            CheckYThreshold();
            UpdatePosition();
        }

        private void SnapToPosition()
        {
            if (playerHeadCamera == null) return;

            var headF = Vector3.ProjectOnPlane(headForwardRotationOffset * playerHeadCamera.transform.forward, Vector3.up).normalized;
            var headR = Quaternion.LookRotation(headF, Vector3.up);
            hCenter = playerHeadCamera.transform.position;
            hCenter.y = 0;
            var pos = hCenter + headR * (Vector3.forward * distance);
            pos.y = transform.position.y;
            transform.position = pos;
            transform.rotation = headR;
        }

        private void UpdatePosition()
        {
            var headF = Vector3.ProjectOnPlane(headForwardRotationOffset * playerHeadCamera.transform.forward, Vector3.up).normalized;
            var headR = Quaternion.LookRotation(headF, Vector3.up);
            var clampedRot = Quaternion.RotateTowards(headR, transform.rotation, angleOffsetClamp);
            var pos = hCenter + clampedRot * (Vector3.forward * distance);
            pos.y = transform.position.y;
            transform.position = pos;
            transform.rotation = clampedRot;
        }

        private void CheckYThreshold()
        {
            if (yRepositionCoroutine != null) return;

            float desired = playerHeadCamera.transform.position.y + yOffset;
            float dist = Mathf.Abs(transform.position.y - desired);

            if (dist > yOffsetToHeadTreshold * 2f)
            {
                Vector3 newP = transform.position;
                newP.y = playerHeadCamera.transform.position.y + yOffset;
                transform.position = newP;
            }
            else if (dist > yOffsetToHeadTreshold)
            {
                yRepositionCoroutine = StartCoroutine(YRepositionIE());
            }
        }

        private void CheckHorizontalThreshold()
        {
            if (hRepositionCoroutine != null) return;

            var headP = playerHeadCamera.transform.position;
            headP.y = 0;
            float dist = Vector3.Distance(headP, hCenter);
            bool reposition = dist > distance * 0.5f;
            bool instant = dist > distance;

            if (reposition && instant)
            {
                hCenter = headP;
            }
            else if (reposition)
            {
                hRepositionCoroutine = StartCoroutine(HorizontalRepositionIE());
            }
        }

        private IEnumerator YRepositionIE()
        {
            var start = transform.position.y;
            float process = 0;
            while (true)
            {
                process += Time.deltaTime * 1.5f;
                float t = UnityEngine.Mathf.SmoothStep(0, 1, process);
                var end = playerHeadCamera.transform.position.y + yOffset;

                var tY = UnityEngine.Mathf.Lerp(start, end, t);
                Vector3 newP = transform.position;
                newP.y = tY;
                transform.position = newP;

                if (process > 1) break;
                yield return null;
            }
            yRepositionCoroutine = null;
        }

        private IEnumerator HorizontalRepositionIE()
        {
            var start = hCenter;
            float process = 0;
            while (true)
            {
                process += Time.deltaTime * 1.5f;

                float t = UnityEngine.Mathf.SmoothStep(0, 1, process);
                var end = playerHeadCamera.transform.position;
                end.y = 0;
                hCenter = Vector3.Lerp(start, end, t);
                if (process > 1) break;
                yield return null;
            }
            hRepositionCoroutine = null;
        }
    }
    
#if UNITY_EDITOR
    [CustomEditor(typeof(UIFollowPlayer))]
    public class UIFollowPlayerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            UIFollowPlayer script = (UIFollowPlayer)target;

            EditorGUI.BeginChangeCheck(); // Start checking for changes

            DrawDefaultInspector();

            if (!script.UseMainCamera)
            {
                script.playerHeadCamera = (GameObject)EditorGUILayout.ObjectField("Player Head Camera",
                    script.playerHeadCamera, typeof(GameObject), true);
            }

            if (EditorGUI.EndChangeCheck()) // Check if any changes occurred
            {
                EditorUtility.SetDirty(script); // Mark the object as dirty if changes occurred
            }
        }
    }
#endif
}
