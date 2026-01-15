using UnityEngine;

public class ParticleDisplay : MonoBehaviour
{
    public Mesh mesh;
    public Shader shader;
    public float scale;
    public Gradient gradient;
    public int gradientResolution;
    public float velocityDisplayMax;
    Material material;
    ComputeBuffer argsBuffer;
    Bounds bounds;
    Texture2D gradientTexture;
    bool onUpdate;

    //Used to set the material(Shader applied) and the initial buffers set
    public void Init(Simulation2D sim)
    {
        material = new Material(shader);
        material.SetBuffer("Positions2D", sim.positionBuffer);
        material.SetBuffer("Velocities", sim.velocityBuffer);
        material.SetBuffer("DensityData", sim.densityBuffer);

        argsBuffer = ComputeHelper.CreateArgsBuffer(mesh, sim.positionBuffer.count);
        bounds = new Bounds(Vector3.zero, Vector3.one * 100);
    }

    //When there is a changed loaded in the inspector
    void setSettings()
    {
        //Updating Variables if any change by the inspector
        if (onUpdate)
        {
            onUpdate = false;

            applyGradientOnTexture(ref gradientTexture, gradientResolution, gradient);
            material.SetTexture("ColourMap", gradientTexture);

            material.SetFloat("scale", scale);
            material.SetFloat("velocityMax", velocityDisplayMax);
        }
    }

    //Is called once a frame
    //Is called after all Update calls are done
    void LateUpdate()
    {
        if (shader != null && material != null)
        {
            setSettings();
            Graphics.DrawMeshInstancedIndirect(mesh, 0, material, bounds, argsBuffer);
        }
    }
 
    //Apply a Gradient on the Texture
    //Needs a Texture, width for the texture and a gradient to change color to
    public static void applyGradientOnTexture(ref Texture2D graidentTexture, int width, Gradient gradient)
    {
        if (graidentTexture == null)
        {
            graidentTexture = new Texture2D(width, 1);
        }
        else if (graidentTexture.width != width)
        {
            graidentTexture.Reinitialize(width, 1);
        }
        if (gradient == null)
        {
            gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.black, 0.0f), new GradientColorKey(Color.black, 1.0f) },
                new GradientAlphaKey[] {new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 1.0f)}
            );
        }

        graidentTexture.filterMode = FilterMode.Bilinear;
        graidentTexture.wrapMode = TextureWrapMode.Clamp;

        Color[] cols = new Color[width];
        for (int i = 0; i < cols.Length; i++)
        {
            float t = (cols.Length <= 1) ? 0f : i / (cols.Length - 1f);
			cols[i] = gradient.Evaluate(t);
        }

        graidentTexture.SetPixels(cols);
        graidentTexture.Apply();
    }

    //Function is called when a changed is loaded via the inspector
    void OnValidate()
    {
        onUpdate = true;
    }

    //Called when a Object is about to be destroyed 
    //When a scene is unloaded it destroys all objects
    void OnDestroy()
    {
        ComputeHelper.Release(argsBuffer);
    }
}
