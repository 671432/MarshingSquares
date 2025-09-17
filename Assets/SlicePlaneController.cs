using UnityEngine;
using UnityEngine.UIElements;

public class SlicePlaneController : MonoBehaviour
{
    public Transform sphereTransform; // Assign your sphere
    private float sphereRadius;
    private Slider _slider3;

    void Start()
    {
        var uiDocument = GameObject.Find("MyUIDocument")?.GetComponent<UIDocument>();
        if (uiDocument == null)
        {
            Debug.LogError("UIDocument not found!");
            return;
        }

        _slider3 = uiDocument.rootVisualElement.Q<Slider>("slider3");
        if (_slider3 == null)
        {
            Debug.LogError("Slider 'slider3' not found!");
            return;
        }

        if (sphereTransform == null)
        {
            GameObject sphereObject = GameObject.Find("Sphere");
            if (sphereObject != null)
            {
                sphereTransform = sphereObject.transform;
            }
            else
            {
                Debug.LogError("Sphere not found!");
                return;
            }
        }

        sphereRadius = sphereTransform.localScale.x / 2f;

        _slider3.RegisterValueChangedCallback(evt => UpdateSlicePlane(evt.newValue));
    }

    void UpdateSlicePlane(float sliceZ)
    {
        // Move the quad to match the slice position
        transform.position = new Vector3(sphereTransform.position.x, sphereTransform.position.y + sliceZ, sphereTransform.position.z);
    }
}
