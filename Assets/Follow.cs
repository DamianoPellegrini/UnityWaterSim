using UnityEngine;

[ExecuteInEditMode]
public class Follow : MonoBehaviour
{

    public Transform ToFollow;
    public Vector3 Offset;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (ToFollow == null) return;

        this.transform.position = ToFollow.position + Offset;
    }
}
