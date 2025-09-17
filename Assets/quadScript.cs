using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;
using System;
using System.Collections.Generic;
using System.IO;

public class quadScript : MonoBehaviour
{

    // Dicom har et "levende" dictionary som leses fra xml ved initDicom
    // slices må sorteres, og det basert på en tag, men at pixeldata lesing er en separat operasjon, derfor har vi nullpeker til pixeldata
    // dicomfile lagres slik at fil ikke må leses enda en gang når pixeldata hentes

    // member variables of quadScript, accessible from any function
    Slice[] _slices;
    int _numSlices;
    int _minIntensity;
    int _maxIntensity;


    private Button _button1;
    private Button _button2;
    private Button _button3;
    private Toggle _toggle;
    private Slider _slider1;
    private Slider _slider2;
    private Slider _slider3;
    //int _iso;

    public int textureSize = 512;
    public int radius = 256;

    private int circleType;

    // Use this for initialization
    void Start()
    {
        var uiDocument = GameObject.Find("MyUIDocument").GetComponent<UIDocument>();
        _button1 = uiDocument.rootVisualElement.Q("button1") as Button;
        _button2 = uiDocument.rootVisualElement.Q("button2") as Button;
        _button3 = uiDocument.rootVisualElement.Q("button3") as Button;
        _toggle = uiDocument.rootVisualElement.Q("toggle") as Toggle;
        _slider1 = uiDocument.rootVisualElement.Q("slider1") as Slider;
        _slider2 = uiDocument.rootVisualElement.Q("slider2") as Slider;
        _slider3 = uiDocument.rootVisualElement.Q("slider3") as Slider;

        // Register callbacks
        _button1.RegisterCallback<ClickEvent>(button1Pushed);
        _button2.RegisterCallback<ClickEvent>(button2Pushed);
        _button3.RegisterCallback<ClickEvent>(button3Pushed);
        _slider1.RegisterValueChangedCallback(slice1SliderChange);
        _slider2.RegisterValueChangedCallback(slice2SliderChange);
        _slider3.RegisterValueChangedCallback(slice3SliderChange);
        _toggle.RegisterValueChangedCallback(evt =>
        {
            Debug.Log($"Toggle pressed. New value: {evt.newValue}");
        });

        Slice.initDicom();

        string dicomfilepath = Application.dataPath + @"\..\dicomdata\";
        _slices = processSlices(dicomfilepath);
        setTexture(_slices[0]);

        // Initialize mesh object
        meshScript mscript = GameObject.Find("GameObjectMesh").GetComponent<meshScript>();
        List<Vector3> vertices = new List<Vector3>();
        List<int> indices = new List<int>();
        mscript.createMeshGeometry(vertices, indices);
    }


    /* MARSHING SQUARES */

    Vector2 InterpolateEdge(float x1, float y1, float x2, float y2, float v1, float v2, float isoLevel)
    {
        if (Mathf.Approximately(v1, v2))
            return new Vector2((x1 + x2) / 2, (y1 + y2) / 2);

        if (Mathf.Approximately(v2 - v1, 0f))
            return new Vector2(x1, y1);

        float t = (isoLevel - v1) / (v2 - v1);
        t = Mathf.Clamp01(t); // Ensure t is between 0 and 1
        return new Vector2(
            Mathf.Lerp(x1, x2, t),
            Mathf.Lerp(y1, y2, t)
        );
    }

    void GenerateMarchingSquares(float isoValue)
    {
        float adjustedIsoValue = isoValue; // Remove the Lerp to use direct iso value from slider
        Debug.Log($"IsoValue: {adjustedIsoValue}");

        List<Vector3> vertices = new List<Vector3>();
        List<int> indices = new List<int>();

        Texture2D texture = GetComponent<Renderer>().material.mainTexture as Texture2D;
        if (texture == null)
        {
            Debug.LogError("No texture found!");
            return;
        }

        int width = texture.width;
        int height = texture.height;

        // Process each cell in the grid
        for (int y = 0; y < height - 1; y++)
        {
            for (int x = 0; x < width - 1; x++)
            {
                // Get pixel intensities (grayscale values 0-1)
                float v1 = texture.GetPixel(x, y).r;           // Top-left pixel
                float v2 = texture.GetPixel(x + 1, y).r;       // Top-right pixel
                float v3 = texture.GetPixel(x, y + 1).r;       // Bottom-left pixel
                float v4 = texture.GetPixel(x + 1, y + 1).r;   // Bottom-right pixel

                // Create unique square index based on intensity threshold

                int squareIndex = 0;
                if (v1 >= adjustedIsoValue) squareIndex |= 1;
                if (v2 >= adjustedIsoValue) squareIndex |= 2;
                if (v3 >= adjustedIsoValue) squareIndex |= 4;
                if (v4 >= adjustedIsoValue) squareIndex |= 8;

                if (squareIndex == 0 || squareIndex == 15) continue;

                Vector2 p1, p2;

                switch (squareIndex)
                {
                    case 1:
                    case 14:
                        p1 = InterpolateEdge(x, y, x, y + 1, v1, v3, adjustedIsoValue);
                        p2 = InterpolateEdge(x, y, x + 1, y, v1, v2, adjustedIsoValue);
                        AddLine(vertices, indices, p1.x, p1.y, p2.x, p2.y);
                        break;
                    case 2:
                    case 13:
                        p1 = InterpolateEdge(x, y, x + 1, y, v1, v2, adjustedIsoValue);
                        p2 = InterpolateEdge(x + 1, y, x + 1, y + 1, v2, v4, adjustedIsoValue);
                        AddLine(vertices, indices, p1.x, p1.y, p2.x, p2.y);
                        break;
                    case 3:
                    case 12:
                        p1 = InterpolateEdge(x, y + 1, x, y, v3, v1, adjustedIsoValue);
                        p2 = InterpolateEdge(x + 1, y + 1, x + 1, y, v4, v2, adjustedIsoValue);
                        AddLine(vertices, indices, p1.x, p1.y, p2.x, p2.y);
                        break;
                    case 4:
                    case 11:
                        p1 = InterpolateEdge(x, y + 1, x + 1, y + 1, v3, v4, adjustedIsoValue);
                        p2 = InterpolateEdge(x, y, x, y + 1, v1, v3, adjustedIsoValue);
                        AddLine(vertices, indices, p1.x, p1.y, p2.x, p2.y);
                        break;
                    case 6:
                    case 9:
                        p1 = InterpolateEdge(x, y, x + 1, y, v1, v2, adjustedIsoValue);
                        p2 = InterpolateEdge(x, y + 1, x + 1, y + 1, v3, v4, adjustedIsoValue);
                        AddLine(vertices, indices, p1.x, p1.y, p2.x, p2.y);
                        break;
                    case 7:
                    case 8:
                        p1 = InterpolateEdge(x, y + 1, x, y, v3, v1, adjustedIsoValue);
                        p2 = InterpolateEdge(x + 1, y + 1, x + 1, y, v4, v2, adjustedIsoValue);
                        AddLine(vertices, indices, p1.x, p1.y, p2.x, p2.y);
                        break;
                    case 5:
                    case 10:
                        float centerValue = (v1 + v2 + v3 + v4) / 4f;
                        if (centerValue >= adjustedIsoValue)
                        {
                            p1 = InterpolateEdge(x, y, x, y + 1, v1, v3, adjustedIsoValue);
                            p2 = InterpolateEdge(x + 1, y, x + 1, y + 1, v2, v4, adjustedIsoValue);
                        }
                        else
                        {
                            p1 = InterpolateEdge(x, y, x + 1, y, v1, v2, adjustedIsoValue);
                            p2 = InterpolateEdge(x, y + 1, x + 1, y + 1, v3, v4, adjustedIsoValue);
                        }
                        AddLine(vertices, indices, p1.x, p1.y, p2.x, p2.y);
                        break;
                }
            }
        }

        // Use meshScript to generate the mesh
        meshScript mscript = GameObject.Find("GameObjectMesh").GetComponent<meshScript>();
        mscript.createMeshGeometry(vertices, indices);
    }

    void AddLine(List<Vector3> vertices, List<int> indices, float x1, float y1, float x2, float y2)
    {
        //Debug.Log($"Vertices count: {vertices.Count}");
        // converting texturecoordinates to -0.5 to 0.5
        float worldX1 = (x1 / textureSize) - 0.5f;
        float worldY1 = (y1 / textureSize) - 0.5f;
        float worldX2 = (x2 / textureSize) - 0.5f;
        float worldY2 = (y2 / textureSize) - 0.5f;

        int indexStart = vertices.Count;
        vertices.Add(new Vector3(worldX1, worldY1, 0));
        vertices.Add(new Vector3(worldX2, worldY2, 0));
        indices.Add(indexStart);
        indices.Add(indexStart + 1);

    }

    Vector2 vec2(float x, float y)
    {
        return new Vector2(x, y);
    }








    /* DRAW CIRCLES */

    void drawCircle1()
    {

        Renderer renderer = GetComponent<Renderer>();

        Texture2D texture = renderer.material.mainTexture as Texture2D;
        if (texture == null)
        {
            Debug.LogError("No texture found on the material.");
            return;
        }

        // Create a new texture based on the existing one
        Texture2D newTexture = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
        newTexture.SetPixels(texture.GetPixels());

        // Calculate center of the texture
        int centerX = texture.width / 2;
        int centerY = texture.height / 2;

        for (int y = 0; y <= texture.height; y++)
        {
            for (int x = 0; x <= texture.width; x++)
            {
                float distance = Mathf.Sqrt((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY));
                float normalizedDistance = Mathf.Clamp01(distance / radius); // Normalise to [0,1]

                Color grayscale = new Color(normalizedDistance, normalizedDistance, normalizedDistance);
                newTexture.SetPixel(x, y, grayscale);
            }
        }

        newTexture.Apply();
        GetComponent<Renderer>().material.mainTexture = newTexture;
    }

    void drawCircle2()
    {

        Renderer renderer = GetComponent<Renderer>();

        Texture2D texture = renderer.material.mainTexture as Texture2D;
        if (texture == null)
        {
            Debug.LogError("No texture found on the material.");
            return;
        }

        // Create a new texture based on the existing one
        Texture2D newTexture = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
        newTexture.SetPixels(texture.GetPixels());

        // Calculate center of the texture
        int centerX = texture.width / 2;
        int centerY = texture.height / 2;

        // Loop through each pixel in the texture
        for (int y = 0; y <= texture.height; y++)
        {
            for (int x = 0; x <= texture.width; x++)
            {
                // Calculate the distance from the current pixel to the center
                float distance = Mathf.Sqrt(Mathf.Pow(x - centerX, 2) + Mathf.Pow(y - centerY, 2));
                //float distance = Mathf.Sqrt((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY));

                // Check if the pixel is within the circle's radius
                if (distance <= radius)
                {
                    // Fill the pixel with white if it's inside the circle
                    newTexture.SetPixel(x, y, Color.white);
                }
                else
                {
                    newTexture.SetPixel(x, y, Color.black);
                }
            }
        }

        // Apply the changes to the texture
        newTexture.Apply();
        GetComponent<Renderer>().material.mainTexture = newTexture;
    }

    void drawGrayscaleCircle()
    {
        Renderer renderer = GetComponent<Renderer>();
        Texture2D texture = renderer.material.mainTexture as Texture2D;
        if (texture == null)
        {
            Debug.LogError("No texture found on the material.");
            return;
        }

        // Create a new texture based on the existing one
        Texture2D newTexture = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);

        // Calculate center of texture
        Vector2 center = new Vector2(texture.width / 2, texture.height / 2);
        float maxDistance = radius; // Use the existing radius variable

        // Fill the texture with grayscale values based on distance
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float normalizedDistance = Mathf.Clamp01(distance / maxDistance);
                Color pixelColor = new Color(normalizedDistance, normalizedDistance, normalizedDistance);
                newTexture.SetPixel(x, y, pixelColor);
            }
        }

        newTexture.Apply();
        renderer.material.mainTexture = newTexture;
    }







    /* SOMETHING ELSE */

    // Update is called once per frame
    void Update()
    {


    }

    ushort pixelval(Vector2 p, int xdim, ushort[] pixels)
    {
        return pixels[(int)p.x + (int)p.y * xdim];
    }

    void setTexture(Slice slice)
    {
        int xdim = slice.sliceInfo.Rows;
        int ydim = slice.sliceInfo.Columns;

        var texture = new Texture2D(xdim, ydim, TextureFormat.RGB24, false);     // garbage collector will tackle that it is new'ed 

        ushort[] pixels = slice.getPixels();

        for (int y = 0; y < ydim; y++)
            for (int x = 0; x < xdim; x++)
            {
                float val = pixelval(new Vector2(x, y), xdim, pixels);
                //float v = (val - _minIntensity) / _maxIntensity; // maps [_minIntensity,_maxIntensity] to [0,1] , i.e.  _minIntensity to black and _maxIntensity to white
                float v = Mathf.Clamp01((val - _minIntensity) / Mathf.Max(_maxIntensity - _minIntensity, 1));
                texture.SetPixel(x, y, new UnityEngine.Color(v, v, v));
            }

        texture.filterMode = FilterMode.Point;  // nearest neigbor interpolation is used.  (alternative is FilterMode.Bilinear)
        texture.Apply();  // Apply all SetPixel calls
        GetComponent<Renderer>().material.mainTexture = texture;
    }

    Slice[] processSlices(string dicomfilepath)
    {
        string[] dicomfilenames = Directory.GetFiles(dicomfilepath, "*.IMA");
        _numSlices = dicomfilenames.Length;

        Slice[] slices = new Slice[_numSlices];

        float max = -1;
        float min = 99999;
        for (int i = 0; i < _numSlices; i++)
        {
            string filename = dicomfilenames[i];
            slices[i] = new Slice(filename);
            SliceInfo info = slices[i].sliceInfo;
            if (info.LargestImagePixelValue > max) max = info.LargestImagePixelValue;
            if (info.SmallestImagePixelValue < min) min = info.SmallestImagePixelValue;
            // divide data by max before inserting into textre
            // alternatively divide 2^dicombitdepth, but that would become 4096 in this instance

        }
        print("Number of slices read:" + _numSlices);
        print("Max intensity in all slices:" + max);
        print("Min intensity in all slices:" + min);

        _minIntensity = (int)min;
        _maxIntensity = (int)max;
        //_iso = 0;

        Array.Sort(slices);

        return slices;
    }

    void drawSphereSlice(float zCoord)
    {
        Debug.Log($"Imagine that the sphere was just sliced at: {zCoord}");

    }







    /* BUTTONS */

    // Slider callbacks
    public void slice1SliderChange(ChangeEvent<float> evt)
    {
        // Update iso value and regenerate marching squares if they're visible
        GenerateMarchingSquares(evt.newValue);
    }

    public void slice2SliderChange(ChangeEvent<float> evt)
    {
        // Update radius
        radius = Mathf.RoundToInt(evt.newValue);

        // Redraw current visualization based on active circle type
        if (circleType == 1)
            drawCircle1();
        else if (circleType == 2)
            drawCircle2();
        else if (circleType == 3)
            draw3DSphereSlice(_slider3.value);
    }

    public void slice3SliderChange(ChangeEvent<float> evt)
    {
        // Activate 3D mode when slider3 changes
        circleType = 3;
        draw3DSphereSlice(evt.newValue);
        GenerateMarchingSquares(_slider1.value);
    }

    // Button callbacks
    public void button1Pushed(ClickEvent evt)
    {
        // Generate marching squares lines
        GenerateMarchingSquares(_slider1.value);
    }

    public void button2Pushed(ClickEvent evt)
    {
        circleType = 1;
        drawCircle1();
    }

    public void button3Pushed(ClickEvent evt)
    {
        circleType = 2;
        drawCircle2();
    }

    public void toggle1Pushed()
    {
        print("button2Pushed");
    }

    void draw3DSphereSlice(float zCoord)
    {
        Renderer renderer = GetComponent<Renderer>();
        Texture2D texture = renderer.material.mainTexture as Texture2D;
        if (texture == null)
        {
            Debug.LogError("No texture found on the material.");
            return;
        }

        // Create a new texture based on the existing one
        Texture2D newTexture = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);

        // Calculate center of texture
        Vector2 center = new Vector2(texture.width / 2, texture.height / 2);
        float maxRadius = radius; // Maximum radius from slider2

        // Normalize z-coordinate from [-2.5, 2.5] to [-1, 1] for sphere calculations
        float normalizedZ = zCoord / 2.5f;

        // Calculate the radius of the slice at this z position
        float sliceRadius = maxRadius * Mathf.Sqrt(Mathf.Max(0, 1 - normalizedZ * normalizedZ));

        // Fill the texture with the slice visualization
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                // Calculate distance in pixel space
                float dx = (x - center.x);
                float dy = (y - center.y);
                float pixelDistance = Mathf.Sqrt(dx * dx + dy * dy);

                // Determine pixel color - solid white for the slice intersection
                Color pixelColor;
                if (pixelDistance <= sliceRadius && Mathf.Abs(normalizedZ) <= 1.0f)
                {
                    pixelColor = Color.white;
                }
                else
                {
                    pixelColor = Color.black;
                }

                newTexture.SetPixel(x, y, pixelColor);
            }
        }

        newTexture.Apply();
        renderer.material.mainTexture = newTexture;

        // Update the mesh for visualization
        meshScript mscript = GameObject.Find("GameObjectMesh").GetComponent<meshScript>();
        List<Vector3> vertices = new List<Vector3>();
        List<int> indices = new List<int>();

        // Scale quad to match slice radius and convert z to [-0.5, 0.5] range for visualization
        float quadScale = sliceRadius / (float)maxRadius;
        float visualZ = normalizedZ * 0.5f; // Scale to [-0.5, 0.5] for visualization

        // Only show quad when it intersects the sphere
        if (Mathf.Abs(normalizedZ) <= 1.0f)
        {
            vertices.Add(new Vector3(-0.5f * quadScale, -0.5f * quadScale, visualZ));
            vertices.Add(new Vector3(0.5f * quadScale, -0.5f * quadScale, visualZ));
            vertices.Add(new Vector3(0.5f * quadScale, 0.5f * quadScale, visualZ));
            vertices.Add(new Vector3(-0.5f * quadScale, 0.5f * quadScale, visualZ));

            // Define quad edges
            indices.Add(0); indices.Add(1);
            indices.Add(1); indices.Add(2);
            indices.Add(2); indices.Add(3);
            indices.Add(3); indices.Add(0);
        }

        mscript.createMeshGeometry(vertices, indices);
    }

}