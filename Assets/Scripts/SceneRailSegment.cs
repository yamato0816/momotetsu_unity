using UnityEngine;

namespace RailTrip3D
{
    // A complete, editable track object. Move its endpoints in the Scene and
    // use Rail Trip > Refresh Selected Tracks to align its authored meshes.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class SceneRailSegment : MonoBehaviour
    {
        public SceneRailNode from;
        public SceneRailNode to;
        public GameObject routeHighlight;

#if UNITY_EDITOR
        private void Update()
        {
            if(Application.isPlaying || from==null || to==null) return;
            Vector3 a=from.transform.position, b=to.transform.position;
            Vector3 target=(a+b)*.5f;
            float length=Vector3.Distance(a,b)/2.2f;
            if(Vector3.SqrMagnitude(transform.position-target)>.000001f ||
                Mathf.Abs(transform.localScale.z-length)>.001f)
                RefreshTransform();
        }
#endif
        public void RefreshTransform()
        {
            if (from == null || to == null) return;
            Vector3 a = from.transform.position, b = to.transform.position;
            Vector3 difference = b-a;
            transform.position = (a+b)*.5f;
            transform.rotation = Quaternion.LookRotation(
                Mathf.Abs(difference.z) >= Mathf.Abs(difference.x) ? Vector3.forward : Vector3.right,
                Vector3.up);
            // Mesh children use local X width / local Z length. The track section has
            // a scaled parent so rail length changes when its endpoints are moved.
            transform.localScale = new Vector3(1f,1f,Mathf.Max(.2f,difference.magnitude/2.2f));
        }
    }
}
