#pragma warning disable 0649

using UnityEngine;
using UnityEngine.Reflect;

[RequireComponent(typeof(Camera))]
public class Teleport : MonoBehaviour
{
    [Tooltip("Offset from the contact point back towards the start position")]
    public float m_ArrivalOffsetRelative = 1f;
    [Tooltip("Offset along the surface normal of the selected object")]
    public float m_ArrivalOffsetNormal = 1f;
    [Tooltip("Fixed offset in world space")]
    public Vector3 m_ArrivalOffsetFixed = Vector3.zero;
    [Tooltip("Indicator offset in world space")]
    public Vector3 m_IndicatorOffsetFixed = Vector3.zero;
    [Tooltip("Fixed time for the teleport animation")]
    public float m_LerpTime = 0.2f;

    [SerializeField]
    GameObject m_IndicatorPrefab;

    [SerializeField] 
    AnimationCurve m_DistanceOverTime;
    [SerializeField] 
    AnimationCurve m_IndicatorSizeOverTime;
    
    Camera m_Camera;
    Transform m_CameraTransform;
    bool m_IsTeleporting;
    Vector3 m_Source;
    Vector3 m_Destination;
    float m_Timer;
    GameObject m_IndicatorInstance;
    Vector3 m_IndicatorScale = new Vector3(1f, 0f, 1f);
    
    void Start()
    {
        m_Camera = GetComponent<Camera>();
        m_CameraTransform = m_Camera.transform;
        SyncObjectBinding.OnCreated += AddCollider;
        // don't need to subscribe to SyncObjectBinding.OnDestroyed since collider will be destroyed automatically
    }

    void Update()
    {
        // if currently in movement
        if (!m_IsTeleporting) 
            return;
        
        m_Timer += Time.deltaTime;
        
        // lerp toward destination
        var ratio = m_Timer / m_LerpTime;
        m_CameraTransform.position = Vector3.Lerp(m_Source, m_Destination, m_DistanceOverTime.Evaluate(ratio));
        
        // animate the indicator
        m_IndicatorScale.y = m_IndicatorSizeOverTime.Evaluate(ratio);
        m_IndicatorInstance.transform.localScale = m_IndicatorScale;
        
        if (m_Timer < m_LerpTime) 
            return;
        
        // reset when timer ends
        m_Timer = 0f;
        m_IsTeleporting = false;
        Destroy(m_IndicatorInstance);
    }

    static void AddCollider(GameObject obj)
    {
        // ensure there's a mesh on the object
        var meshFilter = obj.GetComponentInChildren<MeshFilter>(true);
        if (meshFilter == null)
            return;
        
        // add mesh collider if there isn't one already
        var meshCollider = obj.GetComponentInChildren<MeshCollider>(true);
        if (meshCollider == null)
            meshCollider = obj.AddComponent<MeshCollider>();
        
        meshCollider.sharedMesh = meshFilter.sharedMesh;
    }

    // called in UI event
    public void TriggerTeleport(Vector2 position)
    {
        if (m_IsTeleporting || !Physics.Raycast(m_Camera.ScreenPointToRay(position), out var hitInfo)) 
            return;

        var point = hitInfo.point;
        var normal = hitInfo.normal;
        m_Source = m_CameraTransform.position;
        m_Destination = point + 
                        m_ArrivalOffsetFixed + 
                        m_ArrivalOffsetNormal * normal + 
                        m_ArrivalOffsetRelative * (m_Source - point).normalized;
        m_IsTeleporting = true;
        
        m_IndicatorInstance = Instantiate(m_IndicatorPrefab);
        m_IndicatorInstance.transform.position = m_Destination + m_IndicatorOffsetFixed;
        
        // billboard effect
        m_IndicatorInstance.transform.LookAt(m_Source);
        
        // avoid the full size indicator popping up before the animation
        m_IndicatorScale.y = 0f;
        m_IndicatorInstance.transform.localScale = m_IndicatorScale;
    }
}
