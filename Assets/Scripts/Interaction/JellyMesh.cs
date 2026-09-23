using UnityEngine;

public class JellyMesh : MonoBehaviour
{
    public float Intensity = 1f;
    public float Mass = 1f;
    public float stiffness = 1f;
    public float damping = 0.75f;
    [Min(0f)] public float MaxMorphDistance = 0.25f;
    private Mesh OriginalMesh, MeshClone;
    private MeshRenderer meshRenderer;
    private JellyVertex[] jv;
    private Vector3[] vertexArray;
    public bool Sleep = false;

    public void Start()
    {
        OriginalMesh = GetComponent<MeshFilter>().sharedMesh;
        MeshClone = Instantiate(OriginalMesh);
        GetComponent<MeshFilter>().sharedMesh = MeshClone;
        meshRenderer = GetComponent<MeshRenderer>();
        jv = new JellyVertex[MeshClone.vertices.Length];

        for (int i = 0; i < MeshClone.vertices.Length; i++)
            jv[i] = new JellyVertex(i, transform.TransformPoint(MeshClone.vertices[i]));
    }

    private void FixedUpdate()
    {
        vertexArray = OriginalMesh.vertices;

        if (!Sleep)
        {
            for (int i = 0; i < jv.Length; i++)
            {
                Vector3 target = transform.TransformPoint(vertexArray[jv[i].ID]);
                float intensity = (1 - (meshRenderer.bounds.max.y - target.y) / meshRenderer.bounds.size.y) * Intensity;
                jv[i].Shake(target, Mass, stiffness, damping);
                target = transform.InverseTransformPoint(jv[i].Position);

                Vector3 originalVertex = vertexArray[jv[i].ID];
                Vector3 morphedVertex = Vector3.Lerp(originalVertex, target, intensity);
                Vector3 morphOffset = Vector3.ClampMagnitude(
                    morphedVertex - originalVertex,
                    MaxMorphDistance);
                vertexArray[jv[i].ID] = originalVertex + morphOffset;
            }
        }

        MeshClone.vertices = vertexArray;
    }

    public class JellyVertex
    {
        public int ID;
        public Vector3 Position;
        public Vector3 velocity, Force;

        public JellyVertex(int _id, Vector3 _pos)
        {
            ID = _id;
            Position = _pos;
        }

        public void Shake(Vector3 target, float m, float s, float d)
        {
            Force = (target - Position) * s;
            velocity = (velocity + Force / m) * d;
            Position += velocity;

            if ((velocity + Force + Force / m).magnitude < 0.001f)
                Position = target;
        }
    }
}