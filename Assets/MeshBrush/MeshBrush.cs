using UnityEngine;
using System.Collections.Generic;

namespace MeshBrush
{
    [ExecuteInEditMode]
    public class MeshBrush : MonoBehaviour
    {
        // Actual editor script is inside the Editor folder...
        // Here I just define some public variables that I need for the inspector gui. ツ

        public bool isActive = true;        // Activates or deactivates this MeshBrush instance.

        public bool globalPaintingMode = false;

        public GameObject brush;            // The brush object. Never delete this!
        public GameObject paintBufferRoot;  // The paint buffer root object (hidden).
        public Transform holderObj;         // The holder object's transform.
        public Transform brushTransform;    // The brush object's transform (used for multiple mesh painting).

        public string groupName = "<group name>";

        // This is the global favourites list for templates. 
        // All MeshBrush instances read from and write to the same global favourites list.
        public static List<string> favourites = new List<string>();

        // This is the list of meshes to paint.
        public List<GameObject> setOfMeshesToPaint = new List<GameObject>(1);

        // Global painting layer mask.
        public bool[] globalPaintingLayers = null;

        // This here is the paint buffer used to further improve 
        // the editor's performance when painting high numbers of meshes per stroke.
        public List<GameObject> paintBuffer = new List<GameObject>();

        // This is the object deletion buffer. This is needed because deleting all objects inside the brush area at once 
        // can cause the editor to stall for a few seconds, but deleting them smoothly (in groups of maximum 20 or 30 at once) 
        // improves the editor's performance and stability a lot... it's a bit like the ABS you find in cars ;)
        public List<GameObject> deletionBuffer = new List<GameObject>();

        // KeyCode variables for the customizable shortcuts:
        public KeyCode paintKey = KeyCode.P;
        public KeyCode deleteKey = KeyCode.L;
        public KeyCode combineAreaKey = KeyCode.K;
        public KeyCode increaseRadiusKey = KeyCode.O;
        public KeyCode decreaseRadiusKey = KeyCode.I;

        public float hRadius = 0.3f;                // The radius of the helper handle. 

        public Color hColor = Color.white;          // Sets the helper handle color.

        public int meshCount = 1;                   // Number of meshes to paint.

        public bool useRandomMeshCount = false;     // Should we pick a random number for the mesh count?
        public int minNrOfMeshes = 1, maxNrOfMeshes = 1;

        public float delay = 0.25f;                 // Delay between paint strokes if you hold down your paint button.
        public float meshOffset = 0.0f;             // A float variable for the vertical offset of the mesh we are going to paint. You probably won't ever need this if you place the pivot of your meshes nicely, but you never know.
        public float slopeInfluence = 100.0f;       // Float value for how much the painted meshes are kept upright or not when painted on top of surfaces.

        public bool activeSlopeFilter = false;      // Activate/deactivate the slope filter.

        public float maxSlopeFilterAngle = 30f;     // Float value for the slope filter (use this to avoid having meshes painted on slopes or hills).
        public bool inverseSlopeFilter = false;     // Invert the slope filter functionality with ease.
        public bool manualRefVecSampling = false;   // Manually sample the reference slope vector.
        public bool showRefVecInSceneGUI = true;    // Show/hide the reference gui vector in the scene view.


        public Vector3 slopeRefVec = Vector3.up;    // The sampled reference slope vector.
        public Vector3 slopeRefVec_HandleLocation = Vector3.zero; // The point in space where we sampled our reference slope vector..


        public bool yAxisIsTangent = false;         // Determines if the local Y-Axis of painted meshes should be tangent to its underlying surface or not (if it's not, regular global Vector3.up is used and the meshes will be kept upright).
        public bool invertY = false;                // Inverts the Y-axis of the painted meshes (useful for upside-down painting without slope influence).

        public float scattering = 60f;              // Percentage of scattering.

        public bool autoStatic = true;

        public bool useOverlapFilter = false;
        public bool useRandomAbsMinDist = false;
        public float absoluteMinDist = 0.5f;        // Absolute distance (in meters) to maintain between painted meshes.
        public Vector2 randomAbsMinDist = new Vector2(.5f, 1f);    // Random range within which to choose the absolute min dist value.

        public bool uniformScale = true;
        public bool constUniformScale = true;
        public bool rWithinRange = false;           // Within range toggle bool.

        public bool b_Help = false;                 // Boolean for the help foldout.
        public bool b_Help_Templates = false;       // (Various foldouts inside the help section)
        public bool b_Help_GeneralUsage = false;
        public bool b_Help_Optimization = false;

        public bool b_SetOfMeshesToPaint = true;    // Boolean for the set of meshes to paint foldout.
        public bool b_Templates = true;             // Boolean for the templates menu foldout.
        public bool b_CustomKeys = false;           // Boolean for the customize keyboard shortcuts foldout.
        public bool b_BrushSettings = true;         // Boolean for the brush setting foldout.
        public bool b_Slopes = true;                // Boolean for the slopes foldout.
        public bool b_Randomizers = true;           // Boolean value for the randomize foldout menu in the inspector.
        public bool b_OverlapFilter = true;     // Boolean for the overlap flter foldout.
        public bool b_AdditiveScale = true;         // Boolean for the 'Apply additive scale' foldout menu.
        public bool b_Opt = true;                   // Boolean for the 'Optimization' foldout menu.

        public float rScaleW = 0.0f;                // Random and constant scale multipliers.
        public float rScaleH = 0.0f;
        public float rScale = 0.0f;
        public Vector2 rUniformRange = Vector2.zero; // Variables for our customized random ranges.....
        public Vector4 rNonUniformRange = Vector4.zero;

        public float cScale = 0.0f;                 // Float variable for the uniform additive scale.
        public Vector3 cScaleXYZ = Vector3.zero;    // Vector3 variable for the non-uniform additive scale.

        public float rRot = 0.0f;                   // Random rotation float value.

        public bool autoSelectOnCombine = true;

        public void ResetSlopeSettings()
        {
            slopeInfluence = 100f;
            maxSlopeFilterAngle = 30f;
            activeSlopeFilter = false;
            inverseSlopeFilter = false;
            manualRefVecSampling = false;
            showRefVecInSceneGUI = true;
        }


        public void ResetRandomizers()
        {
            rScale = 0f;
            rScaleW = 0f;
            rScaleH = 0f;
            rRot = 0f;
            rUniformRange = Vector2.zero;
            rNonUniformRange = Vector4.zero;
        }

        public void ResetOverlapFilterSettings()
        {
            useOverlapFilter = false;
            useRandomAbsMinDist = false;
            absoluteMinDist = 0.5f;
            randomAbsMinDist = new Vector2(0.5f, 1.0f);
        }

        // Clear the MeshBrush component's paint and deletion buffer on removal.
        void OnDestroy()
        {
            if (deletionBuffer.Count > 0)
            {
                for (int i = 0; i < deletionBuffer.Count; i++)
                {
                    if (deletionBuffer[i] != null)
                        DestroyImmediate(deletionBuffer[i]);
                }
                deletionBuffer.Clear();
            }

            if (paintBuffer.Count > 0)
            {
                for (int i = 0; i < paintBuffer.Count; i++)
                {
                    if (paintBuffer[i] != null)
                        DestroyImmediate(paintBuffer[i]);
                }
                paintBuffer.Clear();
            }
        }
    }
}

// Copyright (C) 2016, Raphael Beck