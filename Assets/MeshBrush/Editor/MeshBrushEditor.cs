using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace MeshBrush
{
    [ExecuteInEditMode]
    [CustomEditor(typeof(MeshBrush))] // Check out the docs for extending the editor in Unity. You can do lots of awesome stuff with this!
    public class MeshBrushEditor : Editor
    {
        // Let's declare the variables we are going to need in this script...

        // This is the super private MeshPaint instance. 
        // It's the original script object we are editing(overriding) with this custom inspector script here.
        MeshBrush _mp;

        bool canPaint = true;   // This boolean is used alongside the delay variable whenever the user keeps the paint button pressed.

        int selGridInt;

        private delegate void ActiveBrushMode(); // MeshBrush currently has 3 brush modes: Meshpaint mode, PP mode (precision placement) and Vector sampling mode. I use a delegate to swiftly switch between these modes and keep the code clean at the same time (and of course keeping a door open for eventual future additional modes... who knows? ;)
        private ActiveBrushMode activeBrushMode;

        private enum BrushMode // We still need an enumeration for the above mentioned modes though...
        {
            MeshPaint,
            PrecisionPlacement,
            Sample
        }

        // Default brush mode is set to paint.
        private BrushMode brushMode = BrushMode.MeshPaint;

        Collider thisCollider;          // This gameobject's collider.
        Transform thisTransform;        // This gameobject's transform.
        Transform paintedMeshTransform; // The painted mesh's transform.

        GameObject paintedMesh;         // Variable for the instantiated meshes.
        GameObject globalPaintingObj;   // The MeshBrush placeholder object used when we are in global painting mode.

        Vector2 templatesScroll = Vector2.zero; // 2D Vectors used for scrollbars.
        Vector2 layersScroll = Vector2.zero;
        Vector2 setScroll = Vector2.zero;

        double Time2Die;                // Insider joke, no one can understand except for you bro, if you ever read this ;) This variable is used for painting meshes with the key held down.
        double _t;                      // Time storage variable. 
        double nextBufferRefill = 0d;   // The buffer gets refilled smoothly.
        double nextDeletion = 0d;       // Used for deleting objects smoothly. Read more on that kind of editor performance improvement in MeshBrush.cs @ line 36.

        KeyCode _paintKey; // Three variables for the customizable keyboard shortcuts.
        KeyCode _incRadius;
        KeyCode _decRadius;

        // The tooltips that show up in the inspector when we hover our mouse over certain GUI elements.
        GUIContent toolTipColor, toolTipRadius, toolTipFreq, toolTipOffset, toolTipSlopeInfluence, toolTipSlopeFilter, toolTipInvSlope, toolTipManualRefVecS, toolTipRefVecSample, toolTipTangentY, toolTipInvertY, toolTipInset, toolTipNR, toolTipUniformly, toolTipUniformlyRange, //Tooltips for the various gui elements.
            toolTipWithinRange, toolTipOverlapFilter, toolTipRot, toolTipV4, toolTipReset, toolTipAddScale, toolTipFlagS, toolTipCombine, toolTipDelete, toolTipPrecisionPlacementMode;

        // And here comes the raycasting part...
        Ray scRay; // This is the screen ray that shoots out of the scene view's camera when we press the paint button...
        RaycastHit scHit; // And this is its raycasthit.
        RaycastHit brHit; // This is the brush hit; it's used for multiple mesh painting.

        int globalPaintLayerMask = 1;

        float insetThreshold; // Threshold value used to calculate a random position inside the brush's area for each painted mesh (based on the amount of scattering defined by the user).

        float slopeAngle = 0f; // The slope angle variable that gets recalculated for each time we paint. This is used by the slope filter.

        #region OnLoadScript(OnEnable)
        void OnEnable()
        {
            // Reference to the script we are overriding.
            _mp = (MeshBrush)target;

            // Initialize the brushmode delegate. This is EXTREMELY important, since the call of a null delegate function could break everything spitting out horrible errors, if not even crash the program.
            activeBrushMode = BrushMode_MeshPaint;

            thisTransform = _mp.transform;

            if (thisCollider == null && !_mp.globalPaintingMode)
            {
                thisCollider = _mp.GetComponent<Collider>();
                if (thisCollider == null)
                    Debug.LogWarning("MeshBrush: This GameObject has no collider attached to it... MeshBrush needs a collider in order to work properly though; fix please! (see inspector for further details)");
            }

            // Initialize the global painting layers array.
            if (_mp.globalPaintingLayers == null || _mp.globalPaintingLayers.Length != 32)
            {
                _mp.globalPaintingLayers = null;
                _mp.globalPaintingLayers = new bool[32];

                for (int i = 0; i < 32; i++)
                    _mp.globalPaintingLayers[i] = true;
            }

            // Update the layer mask.
            UpdatePaintLayerMask();

            // Load up the favourite templates list.
            MeshBrushTemplate.LoadFavourites();

            // This sets up a holder object for our painted meshes 
            // (in case we don't already have one we create one).
            MeshBrushParent[] holders = _mp.GetComponentsInChildren<MeshBrushParent>();
            if (holders.Length > 0)
            {
                _mp.holderObj = null;
                foreach (MeshBrushParent holder in holders)
                {
                    if (holder)
                    {
                        if (string.CompareOrdinal(holder.name, _mp.groupName) == 0)
                        {
                            _mp.holderObj = holder.transform;
                        }
                    }
                    else continue;
                }

                if (_mp.holderObj == null)
                    CreateHolder();
            }
            else CreateHolder();

            // Create a brush object if we don't have one already. This is needed for multiple mesh painting.
            if (_mp.holderObj.FindChild("Brush") != null)
            {
                _mp.brush = _mp.holderObj.FindChild("Brush").gameObject;
                _mp.brushTransform = _mp.brush.transform;
            }
            else
            {
                _mp.brush = new GameObject("Brush");
                _mp.brushTransform = _mp.brush.transform; // Initialize the brush's transform variable.
                _mp.brushTransform.position = thisTransform.position;
                _mp.brushTransform.parent = _mp.holderObj;
            }

            // The GUI elements of the custom inspector, with their corresponding tooltips:

            toolTipColor = new GUIContent("Color:", "Color of the circle brush.");

            toolTipRadius = new GUIContent("Radius [m]:", "Radius of the circle brush.");

            toolTipFreq = new GUIContent("Delay [s]:", "If you press and hold down the paint button, this value will define the delay (in seconds) between paint strokes; thus, the higher you set this value, the slower you'll be painting meshes.");

            toolTipOffset = new GUIContent("Offset amount [cm]:", "Offsets all the painted meshes away from their underlying surface.\n\nThis is useful if your meshes are stuck inside your GameObject's geometry, or floating above it.\nGenerally, if you place your pivot points carefully, you won't need this.");

            toolTipSlopeInfluence = new GUIContent("Slope influence [%]:", "Defines how much influence slopes have on the rotation of the painted meshes.\n\nA value of 100% for example would adapt the painted meshes to be perfectly perpendicular to the surface beneath them, whereas a value of 0% would keep them upright at all times.");

            toolTipSlopeFilter = new GUIContent("Slope filter max. angle [°]:", "Avoids the placement of meshes on slopes and hills whose angles exceed this value.\nA low value of 20° for example would restrain the painting of meshes onto very flat surfaces, while the maximum value of 180° would deactivate the slope filter completely.");

            toolTipInvSlope = new GUIContent("Inverse slope filter", "Inverts the slope filter's functionality; low values of the filter would therefore focus the placement of meshes onto slopes instead of avoiding it.");

            toolTipManualRefVecS = new GUIContent("Manual reference vector sampling", "You can choose to manually sample a reference slope vector, whose direction will then be used by the slope filter instead of the world's Y-Up axis, to further help you paint meshes with the slope filter applied onto arbitrary geometry (like for instance painting onto huge round planet-meshes, concave topologies like caves etc...).\n\nTo sample one, enter the reference vector sampling mode by clicking the 'Sample reference vector' button below.");

            toolTipRefVecSample = new GUIContent("Sample reference vector", "Activates the reference vector sampling mode, which allows you to pick a normal vector of your mesh to use as a reference by the slope filter.\n\nPress " + _mp.paintKey + " to sample a vector.\nPress Esc to cancel the sampling and return to the regular mesh painting mode.\n(Deselecting and reselecting this object will also exit the sampling mode)");

            toolTipTangentY = new GUIContent("Y-Axis tangent to surface", "As you decrease the slope influence value, you can choose whether you want your painted meshes to be kept upright along the Y-Axis, or tangent to their underlying surface.");

            toolTipInvertY = new GUIContent("Invert Y-Axis", "Flips all painted meshes on their Y-Axis.\nUseful if you are painting upside down and still want your painted meshes to be kept upright (but pointing downwards) on sloped ceiling surfaces for instance.");

            toolTipInset = new GUIContent("Scattering [%]:", "Percentage of how much the meshes are scattered away from the center of the circle brush.\n\n(Default is 60%)");

            toolTipNR = new GUIContent("Nr. of meshes to paint:", "Maximum number of meshes you are going to paint inside the circle brush area at once.");

            toolTipUniformly = new GUIContent("Scale uniformly", "Applies the scale uniformly along all three XYZ axes.");

            toolTipUniformlyRange = new GUIContent("Scale within this random range [Min/Max(XYZ)]:", "Randomly scales the painted meshes between these two minimum/maximum scale values.\n\nX stands for the minimum scale and Y for the maximum scale applied.");

            toolTipWithinRange = new GUIContent("Scale within range", "Randomly scales the meshes based on custom defined random range parameters.");

            toolTipOverlapFilter = new GUIContent(" Use overlap filter", "Activates or deactivates the overlap filter.");

            toolTipRot = new GUIContent("Random Y rotation amount [%]:", "Applies a random rotation around the local Y-axis of the painted meshes.");

            toolTipV4 = new GUIContent("[Min/Max Width (X/Y); Min/Max Height (Z/W)]", "Randomly scales meshes based on custom defined random ranges.\n\nThe X and Y values stand for the minimum and maximum width (it picks a random value between them); " +
                "the Z and W values are for the minimum and maximum height.");

            toolTipReset = new GUIContent("Reset all randomizers", "Resets all the randomize parameters back to zero.");

            toolTipAddScale = new GUIContent("Apply additive scale", "Applies a constant, fixed amount of 'additive' scale after the meshes have been placed.");

            toolTipFlagS = new GUIContent("Flag all painted\nmeshes as static", "Flags all the meshes you've painted so far as static in the editor.\nCheck out the Unity documentation about drawcall batching if you don't know what this is good for.");

            toolTipCombine = new GUIContent("Combine all painted meshes", "Once you're done painting meshes, you can click here to combine them all. This will combine all the meshes you've painted into one single mesh (one per material).\n\nVery useful for performance optimization.\nCannot be undone.");

            toolTipDelete = new GUIContent("Delete all painted meshes", "Are you sure? This will delete all the meshes you've painted onto this GameObject's surface so far (except already combined meshes).");

            toolTipPrecisionPlacementMode = new GUIContent("Precision placement mode", "Enters the precision placement mode, which allows you to place a single mesh from your set of meshes to paint into the scene with maximum precision.\nYou can cycle through the meshes in your set with the N and B keys; N selects the next mesh in your set, whereas B cycles backwards.\n\nThe placement of the mesh is divided into 3 steps, each concluded by pressing the paint button:\n\n1)\tScale (drag the mouse to adjust...)\n2)\tRotation\n3)\tVertical offset\n\nThe third press of the paint button terminates the placement and prepares the next one.");

            for (int i = 0; i < _mp.paintBuffer.Count; i++)
            {
                if (_mp.paintBuffer[i] == null)
                {
                    ClearPaintBuffer();
                }
            }
        }

        void OnDisable()
        {
            ClearPaintBuffer(true);
        }

        void CreateHolder()
        {
            GameObject newHolder = new GameObject(_mp.groupName);
            newHolder.AddComponent<MeshBrushParent>();
            newHolder.transform.rotation = thisTransform.rotation;
            newHolder.transform.parent = thisTransform;
            newHolder.transform.localPosition = Vector3.zero;

            _mp.holderObj = newHolder.transform;
        }
        #endregion

        #region MenuItem functions
        [MenuItem("GameObject/MeshBrush/Paint meshes on selected GameObject")] // Define a custom menu entry in the Unity toolbar above (this way we don't have to go through the add component menu every time).
        static void AddMeshPaint() // This function gets called every time we click the above defined menu entry (since it is being defined exactly below the [MenuItem()] statement).
        {
            // Check if there is a GameObject selected.
            if (Selection.activeGameObject != null)
            {
                // Check if the selected GameObject has a collider on it (without it, where would we paint our meshes on?) :-|
                if (Selection.activeGameObject.GetComponent<Collider>())
                    Selection.activeGameObject.AddComponent<MeshBrush>();
                else
                {
                    if (EditorUtility.DisplayDialog("GameObject has no collider component", "The GameObject on which you want to paint meshes doesn't have a collider...\nOn top of what did you expect to paint meshes? :)\n\n" +
                        "Do you want me to put a collider on it for you (it'll be a mesh collider)?", "Yes please!", "No thanks"))
                    {
                        Selection.activeGameObject.AddComponent<MeshCollider>();
                        Selection.activeGameObject.AddComponent<MeshBrush>();
                    }
                    else return;
                }
            }
            else EditorUtility.DisplayDialog("No GameObject selected", "No GameObject selected man... that's not cool bro D: what did you expect? To paint your meshes onto nothingness? :DDDDD", "Uhmmm...");
        }

        // This initializes the global painting MeshBrush instance.
        [MenuItem("GameObject/MeshBrush/Global painting")]
        static void OpenWindow()
        {
            GameObject mb_obj = new GameObject("MeshBrush Global Painting");

            Camera sceneCam = SceneView.lastActiveSceneView.camera;
            mb_obj.transform.position = sceneCam.transform.position + sceneCam.transform.forward * 1.5f;

            MeshBrush mb = mb_obj.AddComponent<MeshBrush>();
            mb.globalPaintingMode = true;

            Selection.activeGameObject = mb_obj;
        }
        #endregion

        // Method used to update the global painting layer mask.
        void UpdatePaintLayerMask()
        {
            globalPaintLayerMask = 1;
            for (int i = 0; i < _mp.globalPaintingLayers.Length; i++)
            {
                if (_mp.globalPaintingLayers[i])
                    globalPaintLayerMask |= 1 << i;
            }
        }

        #region OnInspectorGUI
        public override void OnInspectorGUI() // Works like OnGUI, except that it updates only the inspector view.
        {
            EditorGUILayout.Space();

            #region Global Painting 
            if (_mp.globalPaintingMode)
            {
                // Global painting interface:
                EditorGUILayout.BeginVertical("Box");
                {
                    // Title
                    GUI.color = new Color(3f, 1f, 0f, 1f);
                    EditorGUILayout.LabelField("MeshBrush - Global Painting Mode", EditorStyles.boldLabel);
                    GUI.color = Color.white;

                    EditorGUILayout.LabelField(new GUIContent("Layer based painting", "You have the control over where MeshBrush is allowed to paint your meshes and where not.\nMeshBrush will only paint onto objects in the scene whose layers are enabled here inside this layer selection."));

                    // Scrollbar for the layer selection
                    layersScroll = EditorGUILayout.BeginScrollView(layersScroll, false, false, GUILayout.Height(166f));
                    {
                        EditorGUILayout.BeginHorizontal();
                        {
                            EditorGUILayout.BeginVertical();
                            {
                                // Always leave the Unity built-in Ignore Raycast layer unticked.
                                _mp.globalPaintingLayers[2] = false;

                                // First column of layers (these are the built-in standard Unity layers).
                                for (int i = 0; i < 8; i++)
                                {
                                    EditorGUILayout.BeginHorizontal();
                                    {
                                        GUI.enabled = i != 2;
                                        _mp.globalPaintingLayers[i] = EditorGUILayout.Toggle(_mp.globalPaintingLayers[i], GUILayout.Width(15f));
                                        EditorGUILayout.LabelField(LayerMask.LayerToName(i), GUILayout.Width(90f));
                                        GUI.enabled = true;
                                    }
                                    EditorGUILayout.EndHorizontal();
                                }
                            }
                            EditorGUILayout.EndVertical();

                            // The next 3 vertical groups represent the second, third and fourth column of the layer selection.
                            EditorGUILayout.BeginVertical();
                            {
                                for (int i = 8; i < 16; i++)
                                {
                                    EditorGUILayout.BeginHorizontal();
                                    {
                                        _mp.globalPaintingLayers[i] = EditorGUILayout.Toggle(_mp.globalPaintingLayers[i], GUILayout.Width(15f));
                                        GUI.color = string.CompareOrdinal(LayerMask.LayerToName(i), string.Empty) == 0 ? new Color(0.65f, 0.65f, 0.65f, 1.0f) : Color.white;
                                        EditorGUILayout.LabelField(string.CompareOrdinal(LayerMask.LayerToName(i), string.Empty) == 0 ? "Layer " + i : LayerMask.LayerToName(i), GUILayout.Width(90f));
                                        GUI.color = Color.white;
                                    }
                                    EditorGUILayout.EndHorizontal();
                                }
                            }
                            EditorGUILayout.EndVertical();
                            EditorGUILayout.BeginVertical();
                            {
                                for (int i = 16; i < 24; i++)
                                {
                                    EditorGUILayout.BeginHorizontal();
                                    {
                                        _mp.globalPaintingLayers[i] = EditorGUILayout.Toggle(_mp.globalPaintingLayers[i], GUILayout.Width(15f));
                                        GUI.color = string.CompareOrdinal(LayerMask.LayerToName(i), string.Empty) == 0 ? new Color(0.65f, 0.65f, 0.65f, 1.0f) : Color.white;
                                        EditorGUILayout.LabelField(string.CompareOrdinal(LayerMask.LayerToName(i), string.Empty) == 0 ? "Layer " + i : LayerMask.LayerToName(i), GUILayout.Width(90f));
                                        GUI.color = Color.white;
                                    }
                                    EditorGUILayout.EndHorizontal();
                                }
                            }
                            EditorGUILayout.EndVertical();
                            EditorGUILayout.BeginVertical();
                            {
                                for (int i = 24; i < 32; i++)
                                {
                                    EditorGUILayout.BeginHorizontal();
                                    {
                                        _mp.globalPaintingLayers[i] = EditorGUILayout.Toggle(_mp.globalPaintingLayers[i], GUILayout.Width(15f));
                                        GUI.color = string.CompareOrdinal(LayerMask.LayerToName(i), string.Empty) == 0 ? new Color(0.65f, 0.65f, 0.65f, 1.0f) : Color.white;
                                        EditorGUILayout.LabelField(string.CompareOrdinal(LayerMask.LayerToName(i), string.Empty) == 0 ? "Layer " + i : LayerMask.LayerToName(i), GUILayout.Width(90f));
                                        GUI.color = Color.white;
                                    }
                                    EditorGUILayout.EndHorizontal();
                                }
                            }
                            EditorGUILayout.EndVertical();
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    EditorGUILayout.EndScrollView();

                    // Buttons to automatically select/deselect all layers.
                    EditorGUILayout.BeginHorizontal();
                    {
                        if (GUILayout.Button("All", GUILayout.Width(55f), GUILayout.Height(20f)))
                        {
                            for (int i = 0; i < _mp.globalPaintingLayers.Length; i++)
                                _mp.globalPaintingLayers[i] = true;
                        }
                        if (GUILayout.Button("None", GUILayout.Width(55f), GUILayout.Height(20f)))
                        {
                            for (int i = 0; i < _mp.globalPaintingLayers.Length; i++)
                                _mp.globalPaintingLayers[i] = false;
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();

                // If there are changes in the inspector (the toggles in the layer selection!), 
                // update the layer mask for global painting.
                if (GUI.changed)
                {
                    UpdatePaintLayerMask();
                }
            }
            #endregion

            EditorGUILayout.Space();

            // MAIN TOGGLE (this one can entirely turn the meshbrush on and off)
            EditorGUILayout.BeginHorizontal();
            {
                _mp.isActive = EditorGUILayout.Toggle(_mp.isActive, GUILayout.Width(15f));
                EditorGUILayout.LabelField("Enabled", GUILayout.Width(70f), GUILayout.ExpandWidth(false));
            }
            EditorGUILayout.EndHorizontal();

            // Useful textfield to name and organize your groups.
            _mp.groupName = EditorGUILayout.TextField(_mp.groupName);
            if (_mp.holderObj) _mp.holderObj.name = _mp.groupName;
            EditorGUILayout.Space();

            #region Help section
            // Foldout menu for the help section, see below for further information
            _mp.b_Help = EditorGUILayout.Foldout(_mp.b_Help, "Help");

            // The help foldout menu in the inspector.
            if (_mp.b_Help)
            {
                EditorGUI.indentLevel = 1;
                EditorGUILayout.HelpBox("Paint meshes onto your GameObject's surface.\n_______\n\nKeyBoard shortcuts:\n\nPaint meshes:\tpress or hold    " + _mp.paintKey + "\nDelete meshes:\tpress or hold    " + _mp.deleteKey + "\nCombine meshes:\tpress or hold    " + _mp.combineAreaKey + "\nIncrease radius:\tpress or hold    " + _mp.increaseRadiusKey + "\nDecrease radius:\tpress or hold    " + _mp.decreaseRadiusKey + "\n_______\n", MessageType.None);

                _mp.b_Help_GeneralUsage = EditorGUILayout.Foldout(_mp.b_Help_GeneralUsage, "General usage");
                if (_mp.b_Help_GeneralUsage)
                {
                    EditorGUILayout.HelpBox("Assign one or more prefab objects to the 'Set of meshes to paint' array below and press " + _mp.paintKey + " while hovering your mouse above your GameObject to start painting meshes. Press " + _mp.deleteKey + " to delete painted meshes." +
                    "\n\nMake sure that the local Y-axis of each prefab mesh is the one pointing away from the surface on which you are painting (to avoid weird rotation errors).\n\n" +
                    "Check the documentation text file that comes with MeshBrush (or the YouTube tutorials) to find out more about the individual brush parameters (but most of them should be quite self explainatory, or at least supplied with a tooltip text label after hovering your mouse over them for a couple of seconds).\n\nFeel free to add multiple MeshBrush script instances to one GameObject for multiple mesh painting sets, with defineable group names and parameters for each of them;\n" +
                    "MeshBrush will then randomly cycle through all of your MeshBrush instances and paint your meshes within the corresponding circle brush based on the corresponding parameters for that set.", MessageType.None);
                }

                _mp.b_Help_Templates = EditorGUILayout.Foldout(_mp.b_Help_Templates, "Templates");
                if (_mp.b_Help_Templates)
                {
                    EditorGUILayout.HelpBox("In the templates foldout menu you can save your favourite brush settings and setups to MeshBrush template files for later reusage.\n\nJust press the save button and name your file and path.\nTo load a template file, press the load button and load up your .meshbrush template file from disk; it's as simple as that.\n\nMeshBrush template files are xml formatted text files, so if you want, you can also open them up with notepad (or some other text editor) and change the settings from there.", MessageType.None);
                }

                _mp.b_Help_Optimization = EditorGUILayout.Foldout(_mp.b_Help_Optimization, "Optimization");
                if (_mp.b_Help_Optimization)
                {
                    EditorGUILayout.HelpBox("You can press 'Flag/Unflag all painted meshes as static' to mark/unmark as static all the meshes you've painted so far.\nFlagging painted meshes as static will improve performance overhead thanks to Unity's built-in static batching functionality, " +
                    "as long as the meshes obviously don't move (and as long as they share the same material).\nSo don't flag meshes as static if you have fancy looking animations on your prefab meshes (like, for instance, swaying animations for vegetation or similar properties that make the mesh move, rotate or scale in any way).\n_______\n\n" +
                    "Once you're done painting you can combine your meshes either with the 'Combine all painted meshes button' or by pressing " + _mp.combineAreaKey + " (this will combine all the meshes inside the brush area).\nCheck out the documentation for further information.\n\n" +
                    "If you are painting grass or other kinds of small vegetation, I recommend using the '2-Sided Vegetation' shader that comes with the MeshBrush package. It's the " +
                    "built-in Unity transparent cutout diffuse shader, just without backface culling, so that you get 2-sided materials.\nYou can obviously also use your own custom shaders if you want.", MessageType.None);
                }

                EditorGUI.indentLevel = 0;
            }
            #endregion
            GUI.enabled = _mp.isActive;

            // Templates foldout
            _mp.b_Templates = EditorGUILayout.Foldout(_mp.b_Templates, "Templates");
            if (_mp.b_Templates)
            {
                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button(new GUIContent((Texture)Resources.Load("MeshBrushSaveIcon"), "Save these current MeshBrush settings to a .meshbrush template file."), GUILayout.Height(55f), GUILayout.Width(55f)))
                    {
                        MeshBrushTemplate.SaveTemplate(_mp);
                    }

                    if (GUILayout.Button(new GUIContent((Texture)Resources.Load("MeshBrushLoadIcon"), "Load up a template file."), GUILayout.Height(55f), GUILayout.Width(55f)))
                    {
                        MeshBrushTemplate.LoadTemplate(_mp);
                    }
                    GUILayout.Space(2);

                    // This vertical box is dedicated to the MeshBrush templates and favourites.
                    GUILayout.BeginVertical("Box");
                    {
                        templatesScroll = EditorGUILayout.BeginScrollView(templatesScroll, false, false);
                        {
                            if (MeshBrush.favourites.Count < 1)
                                EditorGUILayout.LabelField("Favourites list is empty", EditorStyles.wordWrappedLabel);

                            for (int i = 0; i < MeshBrush.favourites.Count; i++)
                            {
                                EditorGUILayout.BeginHorizontal();
                                {
                                    if (GUILayout.Button(System.IO.Path.GetFileNameWithoutExtension(MeshBrush.favourites[i])))
                                    {
                                        if (System.IO.File.Exists(MeshBrush.favourites[i]))
                                        {
                                            if (!MeshBrushTemplate.LoadTemplate(_mp, MeshBrush.favourites[i]))
                                            {
                                                MeshBrush.favourites.RemoveAt(i);
                                                MeshBrushTemplate.SaveFavourites();
                                            }
                                        }
                                        else
                                        {
                                            EditorUtility.DisplayDialog("Failed to load template!", "The selected template file couldn't be loaded.\n\nIt's probably been renamed, deleted or moved elsewhere.\n\nThe corresponding entry in the list of favourite templates will be removed.", "Okay");

                                            MeshBrush.favourites.RemoveAt(i);
                                            MeshBrushTemplate.SaveFavourites();
                                        }
                                    }

                                    if (GUILayout.Button(new GUIContent("...", "Reassign this template"), GUILayout.Width(27f)))
                                    {
                                        string oldPath = MeshBrush.favourites[i];
                                        MeshBrush.favourites[i] = EditorUtility.OpenFilePanel("Reassignment - Select MeshBrush Template", "Assets/MeshBrush/Saved Templates", "meshbrush");

                                        // Revert back to the previous template in case the user cancels the reassignment.
                                        if (string.CompareOrdinal(MeshBrush.favourites[i], string.Empty) == 0)
                                            MeshBrush.favourites[i] = oldPath;

                                        MeshBrushTemplate.SaveFavourites();
                                    }

                                    if (GUILayout.Button(new GUIContent("-", "Removes this template from the list."), GUILayout.Width(27f)))
                                    {
                                        MeshBrush.favourites.RemoveAt(i);
                                        MeshBrushTemplate.SaveFavourites();
                                    }
                                }
                                EditorGUILayout.EndHorizontal();
                            }

                            EditorGUILayout.BeginHorizontal();
                            {
                                if (GUILayout.Button(new GUIContent("+", "You can add your favourite MeshBrush Templates here to this list."), GUILayout.Width(30f), GUILayout.Height(22f), GUILayout.ExpandWidth(false)))
                                {
                                    string path = EditorUtility.OpenFilePanel("Select favourite MeshBrush Template", "Assets/MeshBrush/Saved Templates", "meshbrush");
                                    if (string.CompareOrdinal(path, string.Empty) != 0)
                                    {
                                        MeshBrush.favourites.Add(path);
                                        MeshBrushTemplate.SaveFavourites();
                                    }
                                }

                                GUI.enabled = MeshBrush.favourites.Count > 0;
                                if (GUILayout.Button(new GUIContent("-", "Removes the bottom-most template from the list."), GUILayout.Width(30f), GUILayout.Height(22f)))
                                {
                                    if (MeshBrush.favourites.Count > 0)
                                    {
                                        MeshBrush.favourites.RemoveAt(MeshBrush.favourites.Count - 1);
                                        MeshBrushTemplate.SaveFavourites();
                                    }
                                }
                                GUI.enabled = _mp.isActive;

                                GUI.enabled = MeshBrush.favourites.Count > 0;
                                if (GUILayout.Button(new GUIContent("C", "Clears the entire favourites list."), GUILayout.Width(30f), GUILayout.Height(22f)))
                                {
                                    MeshBrush.favourites.Clear();
                                    MeshBrushTemplate.SaveFavourites();
                                }
                                GUI.enabled = _mp.isActive;
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                        EditorGUILayout.EndScrollView();
                    }
                    GUILayout.EndVertical();
                }
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(1.5f);
            }

            // The next big block of code is responsible for the set of meshes to paint ui.
            _mp.b_SetOfMeshesToPaint = EditorGUILayout.Foldout(_mp.b_SetOfMeshesToPaint, "Set of meshes to paint");
            if (_mp.b_SetOfMeshesToPaint)
            {
                // Never allow an empty set of meshes to paint.
                if (_mp.setOfMeshesToPaint.Count < 1)
                    _mp.setOfMeshesToPaint.Add(null);

                EditorGUILayout.BeginVertical("Box");
                {
                    setScroll = EditorGUILayout.BeginScrollView(setScroll, false, false, GUILayout.ExpandWidth(false), GUILayout.Height(193f));
                    {
                        for (int i = 0; i < _mp.setOfMeshesToPaint.Count; i++)
                        {
                            EditorGUILayout.BeginHorizontal();
                            {
                                GUI.enabled = _mp.isActive && _mp.setOfMeshesToPaint.Count > 1;
                                if (GUILayout.Button(new GUIContent("-", "Removes this entry from the list."), GUILayout.Width(30f), GUILayout.Height(16.5f)))
                                {
                                    _mp.setOfMeshesToPaint.RemoveAt(i);
                                    continue;
                                }
                                GUI.enabled = _mp.isActive;

                                _mp.setOfMeshesToPaint[i] = (GameObject)EditorGUILayout.ObjectField(_mp.setOfMeshesToPaint[i], typeof(GameObject), false, GUILayout.Height(16.35f));
                            }
                            EditorGUILayout.EndHorizontal();
                            GUILayout.Space(2.75f);
                        }
                        EditorGUILayout.Space();
                    }
                    EditorGUILayout.EndScrollView();
                    EditorGUILayout.BeginHorizontal();
                    {
                        if (GUILayout.Button(new GUIContent("+", "Adds an entry to the list."), GUILayout.Width(30f), GUILayout.Height(22f)))
                        {
                            _mp.setOfMeshesToPaint.Add(null);
                        }

                        GUI.enabled = _mp.isActive && _mp.setOfMeshesToPaint.Count > 1;
                        if (GUILayout.Button(new GUIContent("-", "Removes the bottom row from the list."), GUILayout.Width(30f), GUILayout.Height(22f)))
                        {
                            if (_mp.setOfMeshesToPaint.Count > 1)
                            {
                                _mp.setOfMeshesToPaint.RemoveAt(_mp.setOfMeshesToPaint.Count - 1);
                            }
                        }
                        GUI.enabled = _mp.isActive;

                        GUI.enabled = _mp.isActive && _mp.setOfMeshesToPaint.Count > 0;
                        if (GUILayout.Button(new GUIContent("X", "Clears all values from all rows in the list."), GUILayout.Width(30f), GUILayout.Height(22f)))
                        {
                            if (_mp.setOfMeshesToPaint.Count > 0)
                            {
                                for (int i = 0; i < _mp.setOfMeshesToPaint.Count; i++)
                                {
                                    _mp.setOfMeshesToPaint[i] = null;
                                }
                            }
                        }
                        GUI.enabled = _mp.isActive;

                        GUI.enabled = _mp.isActive && _mp.setOfMeshesToPaint.Count > 1;
                        if (GUILayout.Button(new GUIContent("C", "Clears the list (deleting all rows at once)."), GUILayout.Width(30f), GUILayout.Height(22f)))
                        {
                            if (_mp.setOfMeshesToPaint.Count > 1)
                            {
                                _mp.setOfMeshesToPaint.Clear();
                                _mp.setOfMeshesToPaint.Add(null);
                            }
                        }
                        GUI.enabled = _mp.isActive;
                    }
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal(); // Editor version of the GUILayout.BeginHorizontal().
                    {
                        _mp.autoStatic = EditorGUILayout.Toggle(_mp.autoStatic, GUILayout.Width(15f));
                        EditorGUILayout.LabelField("Automatically flag meshes as static", GUILayout.Width(210f), GUILayout.ExpandWidth(false));
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
                GUILayout.Space(2);

                GUI.color = brushMode == BrushMode.PrecisionPlacement ? Color.yellow : Color.white;
                if (GUILayout.Button(toolTipPrecisionPlacementMode, GUILayout.Height(38f)))
                {
                    brushMode = BrushMode.PrecisionPlacement;
                }
                GUI.color = Color.white;
            }

            _mp.b_CustomKeys = EditorGUILayout.Foldout(_mp.b_CustomKeys, "Customize Keyboard Shortcuts");
            if (_mp.b_CustomKeys)
            {
                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.LabelField("Paint");
                    _mp.paintKey = (KeyCode)EditorGUILayout.EnumPopup(_mp.paintKey);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.LabelField("Delete");
                    _mp.deleteKey = (KeyCode)EditorGUILayout.EnumPopup(_mp.deleteKey);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.LabelField("Combine meshes");
                    _mp.combineAreaKey = (KeyCode)EditorGUILayout.EnumPopup(_mp.combineAreaKey);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.LabelField("Increase radius");
                    _mp.increaseRadiusKey = (KeyCode)EditorGUILayout.EnumPopup(_mp.increaseRadiusKey);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.LabelField("Decrease radius");
                    _mp.decreaseRadiusKey = (KeyCode)EditorGUILayout.EnumPopup(_mp.decreaseRadiusKey);
                }
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Reset to default keys"))
                {
                    _mp.paintKey = KeyCode.P;
                    _mp.increaseRadiusKey = KeyCode.O;
                    _mp.decreaseRadiusKey = KeyCode.I;
                }
                EditorGUILayout.Space();
            }

            if (thisCollider == null && !_mp.globalPaintingMode)
            {
                EditorGUILayout.HelpBox("This GameObject has no collider attached to it.\nMeshBrush needs a collider in order to work properly! Do you want to add a collider now?", MessageType.Error);

                if (GUILayout.Button("Yes, add a MeshCollider now please", GUILayout.Height(27f)))
                {
                    _mp.gameObject.AddComponent<MeshCollider>();
                }
                if (GUILayout.Button("Yes, add a BoxCollider now please", GUILayout.Height(27f)))
                {
                    _mp.gameObject.AddComponent<BoxCollider>();
                }
                if (GUILayout.Button("Yes, add a SphereCollider now please", GUILayout.Height(27f)))
                {
                    _mp.gameObject.AddComponent<SphereCollider>();
                }
                if (GUILayout.Button("Yes, add a CapsuleCollider now please", GUILayout.Height(27f)))
                {
                    _mp.gameObject.AddComponent<CapsuleCollider>();
                }
                if (GUILayout.Button("No, switch to global painting mode now please", GUILayout.Height(27f)))
                {
                    _mp.globalPaintingMode = true;
                }
                EditorGUILayout.Space();

                GUI.enabled = false;
            }

            // Avoid having unassigned keys in MeshBrush; 
            // reset to the default value in case the user tries to set the button to "None"
            _mp.paintKey = (_mp.paintKey == KeyCode.None) ? KeyCode.P : _mp.paintKey;
            _mp.deleteKey = (_mp.deleteKey == KeyCode.None) ? KeyCode.L : _mp.deleteKey;

            _mp.combineAreaKey = (_mp.combineAreaKey == KeyCode.None) ? KeyCode.K : _mp.combineAreaKey;

            _mp.increaseRadiusKey = (_mp.increaseRadiusKey == KeyCode.None) ? KeyCode.O : _mp.increaseRadiusKey;
            _mp.decreaseRadiusKey = (_mp.decreaseRadiusKey == KeyCode.None) ? KeyCode.I : _mp.decreaseRadiusKey;

            _mp.b_BrushSettings = EditorGUILayout.Foldout(_mp.b_BrushSettings, "Brush settings");
            if (_mp.b_BrushSettings)
            {
                // Color picker for our circle brush.
                _mp.hColor = EditorGUILayout.ColorField(toolTipColor, _mp.hColor);

                EditorGUILayout.BeginHorizontal();
                {
                    // Radius value
                    _mp.hRadius = EditorGUILayout.FloatField(toolTipRadius, _mp.hRadius, GUILayout.Width(175f), GUILayout.ExpandWidth(true));

                    // Random mesh count toggle
                    _mp.useRandomMeshCount = EditorGUILayout.Toggle(_mp.useRandomMeshCount, GUILayout.Width(15f));

                    EditorGUILayout.LabelField(new GUIContent("Use random number", "Paint a random amount of meshes per stroke (within a defined range)."), GUILayout.Width(140f));
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space();

                // Clamp the meshcount so that it never goes below 1 or above 100.
                if (_mp.useRandomMeshCount)
                {
                    EditorGUILayout.BeginHorizontal();
                    {
                        EditorGUILayout.BeginHorizontal();
                        {
                            EditorGUILayout.LabelField("Min. nr. of meshes:", GUILayout.Width(115f));
                            _mp.minNrOfMeshes = EditorGUILayout.IntField(Mathf.Clamp(_mp.minNrOfMeshes, 1, 100), GUILayout.Width(50f));
                            GUILayout.Space(3f);
                            EditorGUILayout.LabelField("Max. nr. of meshes:", GUILayout.Width(117f));
                            _mp.maxNrOfMeshes = EditorGUILayout.IntField(Mathf.Clamp(_mp.maxNrOfMeshes, 1, 100), GUILayout.Width(50f));
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    {
                        EditorGUILayout.LabelField(toolTipNR, GUILayout.Width(140f));
                        _mp.meshCount = EditorGUILayout.IntField(Mathf.Clamp(_mp.meshCount, 1, 100), GUILayout.Width(50f));
                    }
                    EditorGUILayout.EndHorizontal();
                }

                // Avoid negative or null radii.
                if (_mp.hRadius < 0.01f) _mp.hRadius = 0.01f;
                EditorGUILayout.Space();

                // Slider for the delay between paint strokes.
                _mp.delay = EditorGUILayout.Slider(toolTipFreq, _mp.delay, 0.05f, 1.0f);
                EditorGUILayout.Space(); EditorGUILayout.Space();

                // Slider for the offset amount.
                EditorGUILayout.LabelField(toolTipOffset);
                _mp.meshOffset = EditorGUILayout.Slider(_mp.meshOffset, -50.0f, 50.0f);

                EditorGUILayout.Space();

                // Self-explanatory.
                if (_mp.meshCount <= 1 && !_mp.useRandomMeshCount)
                    GUI.enabled = false;

                // Slider for the scattering.
                EditorGUILayout.LabelField(toolTipInset);
                _mp.scattering = EditorGUILayout.Slider(_mp.scattering, 0, 100.0f);
                EditorGUILayout.Space();

                GUI.enabled = _mp.isActive;

                EditorGUILayout.BeginHorizontal();
                {
                    _mp.yAxisIsTangent = EditorGUILayout.Toggle(_mp.yAxisIsTangent, GUILayout.Width(15f));
                    EditorGUILayout.LabelField(toolTipTangentY, GUILayout.Width(150f));
                    _mp.invertY = EditorGUILayout.Toggle(_mp.invertY, GUILayout.Width(15f));
                    EditorGUILayout.LabelField(toolTipInvertY, GUILayout.Width(150f));
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space();
            }

            _mp.b_Slopes = EditorGUILayout.Foldout(_mp.b_Slopes, "Slopes");
            if (_mp.b_Slopes)
            {
                EditorGUILayout.LabelField(toolTipSlopeInfluence);
                _mp.slopeInfluence = EditorGUILayout.Slider(_mp.slopeInfluence, 0f, 100f); // Slider for slope influence.
                EditorGUILayout.Space();

                EditorGUILayout.BeginHorizontal();
                {
                    _mp.activeSlopeFilter = EditorGUILayout.Toggle(_mp.activeSlopeFilter, GUILayout.Width(15f));
                    EditorGUILayout.LabelField("Use slope filter");
                }
                EditorGUILayout.EndHorizontal();

                if (_mp.activeSlopeFilter == false)
                    GUI.enabled = false;

                EditorGUILayout.LabelField(toolTipSlopeFilter);
                _mp.maxSlopeFilterAngle = EditorGUILayout.Slider(_mp.maxSlopeFilterAngle, 1f, 180f);


                EditorGUILayout.Space();
                EditorGUILayout.BeginHorizontal();
                {
                    _mp.inverseSlopeFilter = EditorGUILayout.Toggle(_mp.inverseSlopeFilter, GUILayout.Width(15f));
                    EditorGUILayout.LabelField(toolTipInvSlope, GUILayout.Width(120f));
                    _mp.manualRefVecSampling = EditorGUILayout.Toggle(_mp.manualRefVecSampling, GUILayout.Width(15f));
                    EditorGUILayout.LabelField(toolTipManualRefVecS, GUILayout.Width(200f));
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space();

                if (_mp.manualRefVecSampling == false)
                    GUI.enabled = false;

                EditorGUILayout.BeginHorizontal();
                {
                    _mp.showRefVecInSceneGUI = EditorGUILayout.Toggle(_mp.showRefVecInSceneGUI, GUILayout.Width(15f));
                    EditorGUILayout.LabelField("Show sampled vector", GUILayout.Width(130f));

                    GUI.color = brushMode == BrushMode.Sample ? Color.yellow : Color.white;
                    if (GUILayout.Button(toolTipRefVecSample, GUILayout.Height(27f), GUILayout.Width(150f), GUILayout.ExpandWidth(true)))
                        brushMode = BrushMode.Sample;
                    GUI.color = Color.white;
                }
                EditorGUILayout.EndHorizontal();

                GUI.enabled = _mp.isActive;
                EditorGUILayout.Space();

                if (GUILayout.Button("Reset all slope settings", GUILayout.Height(27f), GUILayout.Width(150f), GUILayout.ExpandWidth(true)))
                    _mp.ResetSlopeSettings();
            }

            _mp.b_Randomizers = EditorGUILayout.Foldout(_mp.b_Randomizers, "Randomize"); // This makes the little awesome arrow for the foldout menu in the inspector view appear...

            // ...and this below here makes it actually fold stuff in and out 
            // (the menu is closed if the arrow points to the right and thus rScale is false).
            if (_mp.b_Randomizers)
            {
                EditorGUILayout.BeginHorizontal();
                {
                    _mp.uniformScale = EditorGUILayout.Toggle("", _mp.uniformScale, GUILayout.Width(15f));
                    EditorGUILayout.LabelField(toolTipUniformly, GUILayout.Width(100f));

                    _mp.rWithinRange = EditorGUILayout.Toggle("", _mp.rWithinRange, GUILayout.Width(15f));
                    EditorGUILayout.LabelField(toolTipWithinRange);
                }
                EditorGUILayout.EndHorizontal();

                if (_mp.uniformScale == true)
                {
                    if (_mp.rWithinRange == false)
                        _mp.rScale = EditorGUILayout.Slider("Random scale:", _mp.rScale, 0, 5f);
                    else
                    {
                        EditorGUILayout.Space();

                        EditorGUILayout.LabelField(toolTipUniformlyRange);
                        _mp.rUniformRange = EditorGUILayout.Vector2Field("", _mp.rUniformRange);
                    }
                }
                else
                {
                    if (_mp.rWithinRange == false)
                    {
                        EditorGUILayout.Space();

                        _mp.rScaleW = EditorGUILayout.Slider("Random width (X/Z)", _mp.rScaleW, 0, 3f);
                        _mp.rScaleH = EditorGUILayout.Slider("Random height (Y)", _mp.rScaleH, 0, 3f);
                    }
                    else
                    {
                        EditorGUILayout.Space();

                        EditorGUILayout.LabelField("Randomly scale within these ranges:");
                        EditorGUILayout.LabelField(toolTipV4);
                        _mp.rNonUniformRange = EditorGUILayout.Vector4Field("", _mp.rNonUniformRange);
                        EditorGUILayout.Space();
                    }
                }
                EditorGUILayout.Space();

                EditorGUILayout.LabelField(toolTipRot);

                // Create the slider for the percentage of random rotation around the Y axis applied to our painted meshes.
                _mp.rRot = EditorGUILayout.Slider(_mp.rRot, 0.0f, 100.0f);
                EditorGUILayout.Space();

                if (GUILayout.Button(toolTipReset, GUILayout.Height(27f), GUILayout.Width(150f), GUILayout.ExpandWidth(true)))
                    _mp.ResetRandomizers();
            }

            _mp.b_OverlapFilter = EditorGUILayout.Foldout(_mp.b_OverlapFilter, new GUIContent("Overlap filter", "")); // TODO: description!!!
            if (_mp.b_OverlapFilter)
            {
                EditorGUILayout.BeginHorizontal();
                {
                    _mp.useOverlapFilter = EditorGUILayout.Toggle(_mp.useOverlapFilter, GUILayout.Width(15f));
                    EditorGUILayout.LabelField(toolTipOverlapFilter, GUILayout.Width(111.5f));

                    GUI.enabled = _mp.useOverlapFilter;

                    _mp.useRandomAbsMinDist = EditorGUILayout.Toggle(_mp.useRandomAbsMinDist, GUILayout.Width(15f));
                    EditorGUILayout.LabelField(new GUIContent("Random value", "Pick a random value (within a defined range) for the minimum distance between painted meshes."), GUILayout.Width(90f));
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space();


                if (!_mp.useRandomAbsMinDist)
                {
                    EditorGUILayout.BeginHorizontal();
                    {
                        EditorGUILayout.LabelField(new GUIContent("Min. absolute distance [m]:", "The minimum absolute distance (in meters) between painted meshes."), GUILayout.Width(163f));
                        _mp.absoluteMinDist = Mathf.Abs(EditorGUILayout.FloatField(_mp.absoluteMinDist));
                    }
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    {
                        EditorGUILayout.LabelField(new GUIContent("Min. absolute distance [m]:", "The minimum absolute distance (in meters) between painted meshes. A random value will be chosen within the range of [X, Y]."), GUILayout.Width(163f));
                        _mp.randomAbsMinDist = EditorGUILayout.Vector2Field(string.Empty, _mp.randomAbsMinDist);
                        _mp.randomAbsMinDist = new Vector2(Mathf.Abs(_mp.randomAbsMinDist.x), Mathf.Abs(_mp.randomAbsMinDist.y));
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.Space();

                if (GUILayout.Button(new GUIContent("Reset overlap filter settings", "Resets all overlap filter settings back to their default values."), GUILayout.Height(27f), GUILayout.Width(150f), GUILayout.ExpandWidth(true)))
                    _mp.ResetOverlapFilterSettings();
            }
            GUI.enabled = _mp.isActive;

            // Foldout for the additive scale.
            _mp.b_AdditiveScale = EditorGUILayout.Foldout(_mp.b_AdditiveScale, toolTipAddScale);

            if (_mp.b_AdditiveScale)
            {
                _mp.constUniformScale = EditorGUILayout.Toggle(toolTipUniformly, _mp.constUniformScale);
                if (_mp.constUniformScale == true)
                    _mp.cScale = EditorGUILayout.FloatField("Add to scale", _mp.cScale);
                else
                {
                    _mp.cScaleXYZ = EditorGUILayout.Vector3Field("Add to scale", _mp.cScaleXYZ);
                }
                if (_mp.cScale < -0.9f) _mp.cScale = -0.9f;
                if (_mp.cScaleXYZ.x < -0.9f) _mp.cScaleXYZ.x = -0.9f;
                if (_mp.cScaleXYZ.y < -0.9f) _mp.cScaleXYZ.y = -0.9f;
                if (_mp.cScaleXYZ.z < -0.9f) _mp.cScaleXYZ.z = -0.9f;
                EditorGUILayout.Space();

                if (GUILayout.Button("Reset additive scale", GUILayout.Height(27f), GUILayout.Width(150f), GUILayout.ExpandWidth(true)))
                {
                    _mp.cScale = 0;
                    _mp.cScaleXYZ = Vector3.zero;
                }
                EditorGUILayout.Space();
            }

            _mp.b_Opt = EditorGUILayout.Foldout(_mp.b_Opt, "Optimize");
            if (_mp.b_Opt)
            {
                if (_mp.holderObj == null) OnEnable();
                MeshBrushParent mbp = _mp.holderObj.GetComponent<MeshBrushParent>(); ;

                // Create 2 buttons for quickly flagging/unflagging all painted meshes as static...
                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button(toolTipFlagS, GUILayout.Height(50f), GUILayout.Width(150f), GUILayout.ExpandWidth(true)) && mbp)
                        mbp.FlagMeshesAsStatic();
                    if (GUILayout.Button("Unflag all painted\nmeshes as static", GUILayout.Height(50f), GUILayout.Width(150f), GUILayout.ExpandWidth(true)) && mbp)
                        mbp.UnflagMeshesAsStatic();
                }
                EditorGUILayout.EndHorizontal();

                // ...and 2 other buttons for combining and deleting.
                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button(toolTipCombine, GUILayout.Height(50f), GUILayout.Width(150f), GUILayout.ExpandWidth(true)))
                    {
                        if (_mp.holderObj != null)
                        {
                            _mp.holderObj.GetComponent<MeshBrushParent>().CombinePaintedMeshes(_mp.autoSelectOnCombine, _mp.holderObj.GetComponentsInChildren<MeshFilter>());
                        }
                    }

                    //...and one to delete all the meshes we painted on this GameObject so far.
                    if (GUILayout.Button(toolTipDelete, GUILayout.Height(50f), GUILayout.Width(150f), GUILayout.ExpandWidth(true)) && mbp)
                    {
                        ClearDeletionBuffer();
                        Undo.DestroyObjectImmediate(_mp.holderObj.gameObject);
                    }
                }
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(3f);
                EditorGUILayout.BeginHorizontal();
                {
                    _mp.autoSelectOnCombine = EditorGUILayout.Toggle(new GUIContent("", "Automatically select the combined mesh GameObject after having pressed the combine button."), _mp.autoSelectOnCombine, GUILayout.Width(15f));
                    EditorGUILayout.LabelField(new GUIContent("Auto-select combined mesh", "Automatically select the combined mesh GameObject after having pressed the combine button."));
                }
                EditorGUILayout.EndHorizontal();

            }
            EditorGUILayout.Space();


            // Repaint the scene view whenever the inspector's gui is changed in some way. 
            // This avoids weird disturbing snaps of the reference slope vector and the circle 
            // brush GUI handle inside the scene view when we return to it after changing some settings in the inspector.
            if (GUI.changed)
            {
                SceneView.RepaintAll();
                ClearPaintBuffer();
            }
        }
        #endregion

        #region Scene GUI
        void OnSceneGUI() // http://docs.unity3d.com/Documentation/ScriptReference/Editor.OnSceneGUI.html
        {
            // Only enable the meshbrush when the user sets the 
            // specific instance to enabled (through the toggle in the inspector).
            if (_mp.isActive)
            {
                if (!_mp.globalPaintingMode && thisCollider == null) return;

                Handles.color = _mp.hColor;

                Time2Die = EditorApplication.timeSinceStartup;

                canPaint = (_t > Time2Die) ? false : true;

                activeBrushMode(); // Call the delegate method.

                // Assign the various brushmode methods to the delegate based on the current value of the brushmode enum. 
                // This is very comfortable, because now I can just change the enum's value to swap the brushmodes with ease.
                switch (brushMode)
                {
                    case BrushMode.MeshPaint:
                        activeBrushMode = BrushMode_MeshPaint;
                        break;
                    case BrushMode.PrecisionPlacement:
                        activeBrushMode = BrushMode_PrecisionPlacement;
                        break;
                    case BrushMode.Sample:
                        activeBrushMode = BrushMode_SampleReferenceVector;
                        break;

                    default:
                        activeBrushMode = BrushMode_MeshPaint;
                        break;
                }

                switch (Event.current.type)
                {
                    // Increase/decrease the radius with the keyboard buttons I and O
                    case EventType.KeyDown:
                        if (Event.current.keyCode == _mp.increaseRadiusKey)
                            _mp.hRadius += 0.05f;
                        else if (Event.current.keyCode == _mp.decreaseRadiusKey && _mp.hRadius > 0)
                            _mp.hRadius -= 0.05f;
                        break;
                }

                // Draw the custom sampled reference slope vector in the scene view (given that the user wants it to appear and he is actually using the slope filter at all)...
                if (_mp.showRefVecInSceneGUI == true && _mp.manualRefVecSampling == true && _mp.activeSlopeFilter == true)
                    Handles.ArrowCap(0, _mp.slopeRefVec_HandleLocation, Quaternion.LookRotation(_mp.slopeRefVec), 0.9f);
            }

            // Smoothly clear the deletion buffer lobby 
            // instead of deleting all meshes inside the brush area at once.
            // See more infos about that in the script MeshBrush.cs @ line 36.
            if (_mp.deletionBuffer.Count > 0)
            {
                if (nextDeletion < Time2Die || nextDeletion == 0d)
                {
                    nextDeletion = Time2Die + 0.25d;

                    for (int i = 0; i < _mp.deletionBuffer.Count; i++)
                    {
                        DestroyImmediate(_mp.deletionBuffer[i]);
                    }
                    _mp.deletionBuffer.Clear();
                }
            }

            // Constantly fill up the paint buffer (with short delays between strokes, the buffer
            // can get drained out pretty quickly, so we wanna avoid that by filling it up regularly).
            int bufferSize = (_mp.useRandomMeshCount ? _mp.maxNrOfMeshes : _mp.meshCount) * 2;
            if (_mp.paintBuffer.Count < bufferSize)
            {
                if (nextBufferRefill < Time2Die || nextBufferRefill == 0d)
                {
                    nextBufferRefill = Time2Die + 0.1d;

                    for (int i = 0; i < bufferSize / 6; i++)
                    {
                        int r = Random.Range(0, _mp.setOfMeshesToPaint.Count);
                        if (_mp.setOfMeshesToPaint[r] != null)
                        {
                            AddObjectToBuffer(_mp.setOfMeshesToPaint[r]);
                        }
                    }
                }
            }
        }
        #endregion

        #region Paint buffer methods
        void AddObjectToBuffer(GameObject obj)
        {
            // Avoid the lack of a paint buffer root (holder) object.
            if (!_mp.paintBufferRoot)
            {
                _mp.paintBufferRoot = new GameObject("MeshBrush Paint Buffer");
                _mp.paintBufferRoot.hideFlags = HideFlags.HideInHierarchy; // The paint buffer should be hidden.
            }

            GameObject bufferedObj = (GameObject)PrefabUtility.InstantiatePrefab(obj);

            bufferedObj.name = obj.name;
            bufferedObj.transform.parent = _mp.paintBufferRoot.transform;
            bufferedObj.hideFlags = HideFlags.HideInHierarchy;
            bufferedObj.SetActive(false);

            _mp.paintBuffer.Add(bufferedObj);
        }

        GameObject GetObjectFromBuffer()
        {
            // If the buffer is empty, give it an extra kick of +5 elements.
            if (_mp.paintBuffer.Count == 0)
            {
                for (int i = 0; i < 5; i++)
                {
                    int r = Random.Range(0, _mp.setOfMeshesToPaint.Count);

                    if (_mp.setOfMeshesToPaint[r] != null)
                        AddObjectToBuffer(_mp.setOfMeshesToPaint[r]);
                }
            }

            // Calculate a random index value for the buffer of meshes to paint.
            int randomIndex = Random.Range(0, _mp.paintBuffer.Count);

            // Free a random object from the buffer and set it up for placement.
            GameObject obj = _mp.paintBuffer[randomIndex];
            obj.hideFlags = HideFlags.None;
            obj.SetActive(true);

            // Remove it from the buffer collection.
            _mp.paintBuffer.RemoveAt(randomIndex);

            // Return it.
            return obj;
        }

        // This clears the paint buffer.
        // Should be done each time the user changes the MeshBrush's inspector in some way.
        void ClearPaintBuffer(bool instantDeletion = false)
        {
            if (instantDeletion)
            {
                if (_mp.paintBufferRoot != null)
                {
                    DestroyImmediate(_mp.paintBufferRoot);
                    _mp.paintBuffer.Clear();

                    return;
                }
            }

            if (_mp.paintBuffer.Count > 0)
            {
                for (int i = 0; i < _mp.paintBuffer.Count; i++)
                {
                    if (_mp.paintBuffer[i])
                    {
                        _mp.paintBuffer[i].SetActive(false);
                        _mp.deletionBuffer.Add(_mp.paintBuffer[i]);
                    }
                }
                _mp.paintBuffer.Clear();
            }
        }
        #endregion

        #region Brush mode functions
        void BrushMode_MeshPaint() // This method represents the MeshPaint mode for the brush. This is the default brush mode.
        {
            // Only cast rays if we have our object selected (for the sake of performance).
            if (Selection.gameObjects.Length == 1 && Selection.activeGameObject.transform == thisTransform)
            {
                // Shoot the ray through the 2D mouse position on the scene view window.
                scRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

                if (_mp.globalPaintingMode ? Physics.Raycast(scRay, out scHit, Mathf.Infinity, globalPaintLayerMask) : thisCollider.Raycast(scRay, out scHit, Mathf.Infinity))
                {
                    // Filter out the unselected layers (for global painting mode).
                    if (!_mp.globalPaintingLayers[scHit.transform.gameObject.layer])
                        return;

                    // Constantly update scene view at this point 
                    // (to avoid the circle handle jumping around as we click in and out of the scene view).
                    SceneView.RepaintAll();

                    // Thanks to the RepaintAll() function above, the circle handle that we draw here gets updated at all times inside our scene view.
                    Handles.DrawWireDisc(scHit.point, scHit.normal, _mp.hRadius);

                    // If a paint stroke is possible (depends on the delay defined in the inspector), call the paint function when the user presses the paint button. 
                    if (canPaint)
                    {
                        if (Event.current.type == EventType.KeyDown)
                        {
                            _t = Time2Die + _mp.delay;

                            if (Event.current.keyCode == _mp.paintKey)
                                Paint();
                            else if (Event.current.keyCode == _mp.deleteKey)
                                Delete();
                            else if (Event.current.keyCode == _mp.combineAreaKey)
                                CombineMeshesInBrushArea();
                        }
                    }
                }
            }
        }

        int _i = 0; int _phase = 0; float previewY;
        Vector2 lastMousePos; GameObject previewObj;
        void BrushMode_PrecisionPlacement() // Mode for the precise placement of single meshes.
        {
            for (int i = 0; i < _mp.setOfMeshesToPaint.Count; i++)
            {
                // Here the user can choose to cancel the precision placement mode and return to 
                // the regular painting mode by pressing escape. The same happens (in conjuction
                // with an error dialog) if the user forgets to assign one or more fields in the set of meshes to paint.
                if (_mp.setOfMeshesToPaint[i] == null || (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape))
                {
                    if (_mp.setOfMeshesToPaint[i] == null)
                        EditorUtility.DisplayDialog("Warning!", "One or more fields in the set of meshes to paint is empty. Please assign something to all fields before painting.", "Okay");

                    if (previewObj != null)
                    {
                        DestroyImmediate(previewObj);
                        previewObj = null;
                    }
                    _phase = 0;
                    lastMousePos = Vector2.zero;
                    brushMode = BrushMode.MeshPaint;

                    Repaint();
                    return;
                }
            }

            if (Selection.gameObjects.Length == 1 && Selection.activeGameObject.transform == thisTransform)
            {
                int layer = _mp.setOfMeshesToPaint[_i].layer;
                scRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

                if (_mp.globalPaintingMode ? Physics.Raycast(scRay, out scHit, Mathf.Infinity, globalPaintLayerMask) : thisCollider.Raycast(scRay, out scHit, Mathf.Infinity))
                {
                    SceneView.RepaintAll();

                    // Initiate the preview mesh object...
                    if (previewObj == null)
                    {
                        previewObj = (GameObject)PrefabUtility.InstantiatePrefab(_mp.setOfMeshesToPaint[_i]);
                        previewObj.transform.position = scHit.point;
                        previewObj.transform.rotation = Quaternion.identity;

                        previewObj.name = "Preview";
                        previewObj.layer = 2;
                        previewObj.transform.parent = _mp.holderObj;

                        if (_mp.autoStatic) previewObj.isStatic = true;
                    }

                    // Cycle through the set of meshes to paint 
                    // and select a GameObject to place with the left and right arrow keys.
                    if (_mp.setOfMeshesToPaint.Count > 1)
                    {
                        if (Event.current.type == EventType.KeyDown && _phase < 1)
                            switch (Event.current.keyCode)
                            {
                                case KeyCode.B:
                                    _i--;
                                    if (previewObj != null)
                                    {
                                        DestroyImmediate(previewObj);
                                        previewObj = null;
                                    }
                                    break;
                                case KeyCode.N:
                                    _i++;
                                    if (previewObj != null)
                                    {
                                        DestroyImmediate(previewObj);
                                        previewObj = null;
                                    }
                                    break;
                            }

                        if (_i < 0) _i = _mp.setOfMeshesToPaint.Count - 1;
                        if (_i >= _mp.setOfMeshesToPaint.Count) _i = 0;
                    }
                    else _i = 0;
                    _i = Mathf.Clamp(_i, 0, _mp.setOfMeshesToPaint.Count - 1);
                }

                switch (_phase)
                {
                    case 0:
                        // Choose a precise location for the mesh inside the scene view.
                        if (previewObj != null)
                        {
                            previewObj.transform.position = scHit.point;
                            Handles.Label(scHit.point + Vector3.right + Vector3.up, "Currently selected: " + _mp.setOfMeshesToPaint[_i].name + "\nSelect next: [N]  /  Select previous: [B]\nConfirm location: [" + _mp.paintKey + "]  /  Cancel placement: [ESC]", EditorStyles.helpBox);
                        }

                        // Confirm placement location and go to the next phase.
                        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == _mp.paintKey)
                        {
                            previewY = scHit.point.y;
                            lastMousePos = Event.current.mousePosition;
                            _phase = 1;
                        }

                        break;
                    case 1:
                        // Adjust the scale by dragging around the mouse.
                        if (previewObj != null)
                        {
                            previewObj.transform.localScale = Vector3.one * (lastMousePos - Event.current.mousePosition).magnitude * 0.01f;
                            Handles.Label(scHit.point + Vector3.right + Vector3.up, "Currently selected: " + _mp.setOfMeshesToPaint[_i].name + "\nAdjust the scale by dragging the mouse away from the center\nConfirm scale: [" + _mp.paintKey + "]  /  Cancel placement: [ESC]", EditorStyles.helpBox);
                        }

                        // Confirm the adjusted scale and move over to the rotation phase.
                        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == _mp.paintKey)
                        {
                            lastMousePos = Event.current.mousePosition;
                            _phase = 2;
                        }

                        break;
                    case 2:
                        // Adjust the rotation (along the Y axis) by dragging aroung the mouse. 
                        if (previewObj != null)
                        {
                            float yRot = (lastMousePos - Event.current.mousePosition).x;
                            if (yRot < -360.0f) yRot += 360.0f;
                            if (yRot > 360.0f) yRot -= 360.0f;

                            previewObj.transform.eulerAngles = new Vector3(previewObj.transform.eulerAngles.x, yRot, previewObj.transform.eulerAngles.z);

                            Handles.Label(scHit.point + Vector3.right + Vector3.up, "Currently selected: " + _mp.setOfMeshesToPaint[_i].name + "\nAdjust the rotation along the Y-Axis by dragging your mouse horizontally\nConfirm rotation: [" + _mp.paintKey + "]  /  Cancel placement: [ESC]", EditorStyles.helpBox);
                        }

                        // Confirm the adjusted rotation and move to the last phase: vertical offset.
                        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == _mp.paintKey)
                        {
                            lastMousePos = Event.current.mousePosition;
                            _phase = 3;
                        }

                        break;
                    case 3:
                        // In this step of the placement we adjust the vertical offset along the local Y axis.
                        if (previewObj != null)
                        {
                            previewObj.transform.position = new Vector3(previewObj.transform.position.x, previewY + ((lastMousePos - Event.current.mousePosition) * 0.01f).y, previewObj.transform.position.z);
                            Handles.Label(scHit.point + Vector3.right + Vector3.up, "Currently selected: " + _mp.setOfMeshesToPaint[_i].name + "\nAdjust the offset along the Y-axis by dragging your mouse vertically\nConfirm offset: [" + _mp.paintKey + "]  /  Cancel placement: [ESC]", EditorStyles.helpBox);
                        }

                        // Final placement confirmation
                        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == _mp.paintKey)
                        {
                            GameObject finalObj = (GameObject)PrefabUtility.InstantiatePrefab(_mp.setOfMeshesToPaint[_i]);

                            finalObj.transform.position = previewObj.transform.position;
                            finalObj.transform.rotation = previewObj.transform.rotation;

                            finalObj.transform.localScale = previewObj.transform.localScale;
                            finalObj.name = _mp.setOfMeshesToPaint[_i].name;
                            finalObj.layer = layer;
                            finalObj.transform.parent = _mp.holderObj;

                            if (_mp.autoStatic) finalObj.isStatic = true;

                            if (previewObj != null)
                            {
                                DestroyImmediate(previewObj);
                                previewObj = null;
                            }

                            _phase = 0;
                            lastMousePos = Vector2.zero;
                            brushMode = BrushMode.MeshPaint;

                            Repaint();
                        }
                        break;
                }
            }
        }

        // This one represents the vector sampling mode. 
        // This brushmode allows the user to sample a custom defined slope reference 
        // vector used by the slope filter... Check out the tutorial to find out what this does in case you're confused.
        void BrushMode_SampleReferenceVector()
        {
            if (Selection.gameObjects.Length == 1 && Selection.activeGameObject.transform == thisTransform)
            {
                scRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

                if (_mp.globalPaintingMode ? Physics.Raycast(scRay, out scHit, Mathf.Infinity, globalPaintLayerMask) : thisCollider.Raycast(scRay, out scHit, Mathf.Infinity))
                {
                    SceneView.RepaintAll();

                    // Draw a GUI handle arrow to represent the vector to sample. 
                    Handles.ArrowCap(0, scHit.point, Quaternion.LookRotation(scHit.normal), 0.9f);

                    // Sample the reference vector for the slope filter and store it in the slopeRefVec variable.
                    if (Event.current.type == EventType.KeyDown && Event.current.keyCode == _mp.paintKey)
                    {
                        _mp.slopeRefVec = scHit.normal.normalized;
                        _mp.slopeRefVec_HandleLocation = scHit.point;
                        brushMode = BrushMode.MeshPaint; // Jump back to the meshpaint mode automatically.
                    }

                    // Cancel the sampling mode by pressing the escape button.
                    if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
                    {
                        brushMode = BrushMode.MeshPaint;
                        Repaint();
                    }
                }
            }
        }
        #endregion

        void Paint() // The actual paint function.
        {
            // Display an error dialog box if we are trying to paint 'nothing' onto our GameObject.
            if (_mp.setOfMeshesToPaint.Count == 0)
            {
                EditorUtility.DisplayDialog("No meshes to paint...", "Please add at least one prefab mesh to the array of meshes to paint.", "Okay");
                return;
            }
            else
            {
                // Check if every single field of the array has a GameObject assigned (this is necessary to avoid a disturbing error printed in the console).
                for (int i = _mp.setOfMeshesToPaint.Count - 1; i >= 0; i--)
                {
                    if (_mp.setOfMeshesToPaint[i] == null)
                    {
                        EditorUtility.DisplayDialog("Warning!", "One or more fields in the set of meshes to paint is empty. Please assign something to all fields before painting.", "Okay");
                        return;
                    }
                }

                if (_mp.holderObj == null)
                    OnEnable();

                // Call the correct meshpaint method. 
                // This is where the actual paint-stuff happens :)
                if (_mp.useRandomMeshCount)
                {
                    if (_mp.minNrOfMeshes == 1 && _mp.maxNrOfMeshes == 1)
                        PaintSingleMesh();
                    else
                        PaintMultipleMeshes();
                }
                else
                {
                    if (_mp.meshCount == 1)
                        PaintSingleMesh();
                    else if (_mp.meshCount > 1)
                        PaintMultipleMeshes();
                }
            }
        }

        #region Single Meshpaint
        void PaintSingleMesh() // Single meshpaint function.
        {
            // Calculate the angle between the world's upvector (or a manually sampled reference vector) and the normal vector of our hit.
            slopeAngle = _mp.activeSlopeFilter ? Vector3.Angle(scHit.normal, _mp.manualRefVecSampling ? _mp.slopeRefVec : Vector3.up) : _mp.inverseSlopeFilter ? 180f : 0f;

            // Here I'm applying the slope filter based on the angle value obtained above...
            if ((_mp.inverseSlopeFilter == true) ? (slopeAngle > _mp.maxSlopeFilterAngle) : (slopeAngle < _mp.maxSlopeFilterAngle))
            {
                // Apply the overlap filter.
                if (_mp.useOverlapFilter && CheckOverlap(scHit.point))
                    return;

                // This is the creation of the mesh. 
                // In the following lines of code it gets retrieved out of the buffer, 
                // placed and rotated correctly at the location of our brush object's center.
                paintedMesh = GetObjectFromBuffer();

                paintedMeshTransform = paintedMesh.transform;

                paintedMeshTransform.position = scHit.point;
                paintedMeshTransform.rotation = Quaternion.LookRotation(scHit.normal);

                // Align the painted mesh's up vector to the corresponding direction (defined by the user).
                paintedMeshTransform.up = Vector3.Lerp(_mp.yAxisIsTangent ? paintedMeshTransform.up : Vector3.up, paintedMeshTransform.forward, _mp.slopeInfluence * 0.01f);

                // Set the instantiated object as a parent of the "Painted meshes" holder gameobject.
                paintedMeshTransform.parent = _mp.holderObj;

                // Automatically flag the painted mesh as static if the "Automatically flag as static" toggle is set to true.
                if (_mp.autoStatic) paintedMesh.isStatic = true;

                // The various states of the toggles:
                if (!_mp.rWithinRange && !_mp.uniformScale)
                {
                    if (_mp.rScaleW > 0 || _mp.rScaleH > 0)
                        ApplyRandomScale(paintedMesh, _mp.rScaleW, _mp.rScaleH);
                }
                else if (!_mp.rWithinRange && _mp.uniformScale)
                {
                    if (_mp.rScale > 0)
                        ApplyRandomScale(paintedMesh, _mp.rScale);
                }
                else if (_mp.rWithinRange && !_mp.uniformScale)
                {
                    if (_mp.rNonUniformRange != Vector4.zero)
                        ApplyRandomScale(paintedMesh, _mp.rNonUniformRange);
                }
                else
                {
                    if (_mp.rUniformRange != Vector2.zero)
                        ApplyRandomScale(paintedMesh, _mp.rUniformRange);
                }

                // Constant, additive scale (adds up to the total scale after everything else):
                if (!_mp.constUniformScale)
                {
                    if (_mp.cScaleXYZ != Vector3.zero)
                        AddConstantScale(paintedMesh, _mp.cScaleXYZ.x, _mp.cScaleXYZ.y, _mp.cScaleXYZ.z);
                }
                else
                {
                    if (_mp.cScale != 0)
                        AddConstantScale(paintedMesh, _mp.cScale);
                }

                if (_mp.rRot > 0)
                    ApplyRandomRotation(paintedMesh, _mp.rRot);
                if (_mp.meshOffset != 0)
                    ApplyMeshOffset(paintedMesh, _mp.meshOffset, scHit.normal);

                // Allow the "undo" operation for the creation of meshes.
                Undo.RegisterCreatedObjectUndo(paintedMesh, paintedMesh.name);
            }
        }
        #endregion

        #region Multiple Meshpaint
        void PaintMultipleMeshes() // Multiple meshpaint function.
        {
            insetThreshold = (_mp.hRadius * 0.01f * _mp.scattering);

            // For the creation of multiple meshes at once we need a temporary brush gameobject, 
            // which will wander around our circle brush's area to shoot rays and adapt the meshes.
            if (_mp.holderObj.FindChild("Brush") == null)
            {
                // In case we don't have one yet (or the user deleted it), create a new one.
                _mp.brush = new GameObject("Brush");

                // Initialize the brush's transform variable.
                _mp.brushTransform = _mp.brush.transform;
                _mp.brushTransform.position = thisTransform.position;
                _mp.brushTransform.parent = _mp.holderObj;
            }

            for (int i = _mp.useRandomMeshCount ? Random.Range(_mp.minNrOfMeshes, _mp.maxNrOfMeshes + 1) : _mp.meshCount; i > 0; i--)
            {
                // Position the brush object slightly away from our raycasthit and rotate it correctly.
                _mp.brushTransform.position = scHit.point + (scHit.normal * 0.5f);
                _mp.brushTransform.rotation = Quaternion.LookRotation(scHit.normal); _mp.brushTransform.up = _mp.brushTransform.forward;

                // Afterwards, translate it inside the brush's circle area based on the scattering percentage defined by the user.
                _mp.brushTransform.Translate(Random.Range(-Random.insideUnitCircle.x * insetThreshold, Random.insideUnitCircle.x * insetThreshold), 0f, Random.Range(-Random.insideUnitCircle.y * insetThreshold, Random.insideUnitCircle.y * insetThreshold), Space.Self);

                // Perform the final raycast from the brush object's location to our gameobject's surface. 
                // I'm giving this a limit of 2.5m to avoid meshes being painted behind hills and walls when the brush's radius is big.
                if (_mp.globalPaintingMode ? Physics.Raycast(new Ray(_mp.brushTransform.position, -scHit.normal), out brHit, 2.5f) : thisCollider.Raycast(new Ray(_mp.brushTransform.position, -scHit.normal), out brHit, 2.5f))
                {
                    // Calculate the slope angle based on the angle between the world's upvector (or a manually sampled reference vector) and the normal vector of our hit.
                    slopeAngle = _mp.activeSlopeFilter ? Vector3.Angle(brHit.normal, _mp.manualRefVecSampling ? _mp.slopeRefVec : Vector3.up) : _mp.inverseSlopeFilter ? 180f : 0f;

                    // And if all conditions are met, paint our meshes according to the user's parameters.
                    if (_mp.inverseSlopeFilter == true ? slopeAngle > _mp.maxSlopeFilterAngle : slopeAngle < _mp.maxSlopeFilterAngle)
                    {
                        // Apply the overlap filter.
                        if (_mp.useOverlapFilter && CheckOverlap(brHit.point))
                            continue;

                        // This is the creation of the mesh. 
                        // In the following lines of code it gets retrieved out of the buffer, 
                        // placed and rotated correctly at the location of our brush object's center.
                        paintedMesh = GetObjectFromBuffer();

                        paintedMeshTransform = paintedMesh.transform;

                        paintedMeshTransform.position = brHit.point;
                        paintedMeshTransform.rotation = Quaternion.LookRotation(brHit.normal);

                        paintedMeshTransform.up = Vector3.Lerp(_mp.yAxisIsTangent ? paintedMeshTransform.up : Vector3.up, paintedMeshTransform.forward, _mp.slopeInfluence * 0.01f);

                        // Afterwards we set the instantiated object as a parent of the holder GameObject.
                        paintedMeshTransform.parent = _mp.holderObj;

                        if (_mp.autoStatic) paintedMesh.isStatic = true;

                        if (!_mp.rWithinRange && !_mp.uniformScale)
                        {
                            if (_mp.rScaleW > 0f || _mp.rScaleH > 0f)
                                ApplyRandomScale(paintedMesh, _mp.rScaleW, _mp.rScaleH);
                        }
                        else if (!_mp.rWithinRange && _mp.uniformScale)
                        {
                            if (_mp.rScale > 0f)
                                ApplyRandomScale(paintedMesh, _mp.rScale);
                        }
                        else if (_mp.rWithinRange && !_mp.uniformScale)
                        {
                            if (_mp.rNonUniformRange != Vector4.zero)
                                ApplyRandomScale(paintedMesh, _mp.rNonUniformRange);
                        }
                        else
                        {
                            if (_mp.rUniformRange != Vector2.zero)
                                ApplyRandomScale(paintedMesh, _mp.rUniformRange);
                        }

                        // Constant, additive scale (adds up to the total scale after everything else:
                        if (!_mp.constUniformScale)
                        {
                            if (_mp.cScaleXYZ != Vector3.zero)
                                AddConstantScale(paintedMesh, _mp.cScaleXYZ.x, _mp.cScaleXYZ.y, _mp.cScaleXYZ.z);
                        }
                        else
                        {
                            if (_mp.cScale != 0f)
                                AddConstantScale(paintedMesh, _mp.cScale);
                        }

                        // The next two if-statements apply the random rotation and the vertical offset to the mesh.
                        if (_mp.rRot > 0f)
                            ApplyRandomRotation(paintedMesh, _mp.rRot);
                        if (_mp.meshOffset != 0f)
                            ApplyMeshOffset(paintedMesh, _mp.meshOffset, scHit.normal);

                        // Allow the undo operation for the creation of meshes.
                        Undo.RegisterCreatedObjectUndo(paintedMesh, paintedMesh.name);
                    }
                }
            }
        }
        #endregion

        #region Overlap Filter
        bool CheckOverlap(Vector3 objPos)
        {
            Transform[] otherPaintedMeshes = _mp.holderObj.GetComponentsInChildren<Transform>();
            if (otherPaintedMeshes != null && otherPaintedMeshes.Length > 0)
            {
                foreach (Transform otherPaintedMesh in otherPaintedMeshes)
                {
                    if (otherPaintedMesh != _mp.brushTransform && otherPaintedMesh != _mp.holderObj.transform && Vector3.Distance(objPos, otherPaintedMesh.position) < (_mp.useRandomAbsMinDist ? Random.Range(_mp.randomAbsMinDist.x, _mp.randomAbsMinDist.y) : _mp.absoluteMinDist))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        #endregion

        #region Delete
        void Delete() // Method for deleting painted meshes inside the circle brush.
        {
            // Get all of the painted meshes.
            Transform[] paintedMeshes = _mp.holderObj.GetComponentsInChildren<Transform>();

            if (paintedMeshes != null && paintedMeshes.Length > 0)
            {
                foreach (Transform paintedMesh in paintedMeshes)
                {
                    // Delete all meshes inside the circle area of the brush
                    // (an object is considered inside of our circle brush area if the distance between its transform and the current location of the circle brush's center point is smaller than the circle's radius).
                    if (paintedMesh != _mp.brushTransform && paintedMesh != _mp.holderObj.transform && Vector3.Distance(scHit.point, paintedMesh.position) < _mp.hRadius)
                    {
                        paintedMesh.gameObject.SetActive(false);

                        _mp.deletionBuffer.Add(paintedMesh.gameObject);
                        if (_mp.deletionBuffer.Count > 30)
                        {
                            ClearDeletionBuffer();
                        }
                    }
                }
            }
        }

        void ClearDeletionBuffer()
        {
            for (int i = 0; i < _mp.deletionBuffer.Count; i++)
            {
                DestroyImmediate(_mp.deletionBuffer[i]);
            }
            _mp.deletionBuffer.Clear();
        }
        #endregion

        #region Area Combine
        void CombineMeshesInBrushArea()
        {
            // Get all of the painted meshes (the MeshFilters are what we need here).
            MeshFilter[] meshFilters = _mp.holderObj.GetComponentsInChildren<MeshFilter>();

            // Create a container for all mesh filters to combine.
            List<MeshFilter> meshFiltersToCombine = new List<MeshFilter>();

            if (meshFilters != null && meshFilters.Length > 0)
            {
                // Combine all meshes inside the circle area of the brush (create one major mesh per material used)
                // (an object is considered inside of our circle brush area if the distance between its transform and the current location of the circle brush's center point is smaller than the circle's radius).                    
                foreach (MeshFilter meshFilter in meshFilters)
                    if (meshFilter.transform != _mp.brushTransform && meshFilter.transform != _mp.holderObj.transform && Vector3.Distance(scHit.point, meshFilter.transform.position) < _mp.hRadius)
                        meshFiltersToCombine.Add(meshFilter);

                if (meshFiltersToCombine.Count > 0)
                    _mp.holderObj.GetComponent<MeshBrushParent>().CombinePaintedMeshes(_mp.autoSelectOnCombine, meshFiltersToCombine.ToArray());
            }
        }
        #endregion

        #region Other functions

        float rW, rH;
        void ApplyRandomScale(GameObject sMesh, float W, float H) // Apply some random scale (non-uniformly) to the freshly painted object.
        {
            rW = Mathf.Abs(W * Random.value + 0.15f);
            rH = Mathf.Abs(H * Random.value + 0.15f);
            sMesh.transform.localScale = new Vector3(rW, rH, rW);
        }

        float r;
        void ApplyRandomScale(GameObject sMesh, float U) // Here I overload the ApplyRandomScale function for the uniform random scale.
        {
            r = Mathf.Abs(U * Random.value + 0.15f);
            sMesh.transform.localScale = new Vector3(r, r, r);
        }

        float s;
        void ApplyRandomScale(GameObject sMesh, Vector2 range) // Overload for the customized uniform random scale.
        {
            s = Random.Range(range.x, range.y);
            s = Mathf.Abs(s);
            sMesh.transform.localScale = new Vector3(s, s, s);
        }


        void ApplyRandomScale(GameObject sMesh, Vector4 ranges) // Non-uniform custom random range scale.
        {
            rW = Random.Range(ranges.x, ranges.y);
            rH = Random.Range(ranges.z, ranges.w);
            Vector3 a = new Vector3(rW, rH, rW);
            a.x = Mathf.Abs(a.x); a.y = Mathf.Abs(a.y); a.z = Mathf.Abs(a.z);
            sMesh.transform.localScale = a;
        }

        void AddConstantScale(GameObject sMesh, float X, float Y, float Z) // Same procedure for the constant scale methods.
        {
            Vector3 a = sMesh.transform.localScale + new Vector3(X, Y, Z);
            a.x = Mathf.Abs(a.x); a.y = Mathf.Abs(a.y); a.z = Mathf.Abs(a.z);
            sMesh.transform.localScale = a;
        }

        void AddConstantScale(GameObject sMesh, float S)
        {
            Vector3 a = sMesh.transform.localScale + new Vector3(S, S, S);
            a.x = Mathf.Abs(a.x); a.y = Mathf.Abs(a.y); a.z = Mathf.Abs(a.z);
            sMesh.transform.localScale = a;
        }

        void ApplyRandomRotation(GameObject rMesh, float rot) // Apply some random rotation (around local Y axis) to the freshly painted mesh.
        {
            float randomRotation = Random.Range(0f, 3.60f * rot);
            rMesh.transform.Rotate(new Vector3(0f, randomRotation, 0f));
        }

        void ApplyMeshOffset(GameObject oMesh, float offset, Vector3 direction) // Apply the offset
        {
            // We divide offset by 100 since we want to use centimeters as our offset unit (because 1cm = 0.01m).
            oMesh.transform.Translate((direction.normalized * offset * 0.01f), Space.World);
        }

        #endregion
    }
}

// Copyright (C) 2016, Raphael Beck
