using UnityEngine;
using UnityEditor;

using System;
using System.IO;
using System.Xml.Serialization;
using System.Collections.Generic;

namespace MeshBrush
{
    // Static class used to save and load MeshBrush templates.
    public static class MeshBrushTemplate
    {
        // XML relevant variables.
        static FileStream xml_templateFile = null, xml_favouritesFile = null;
        static XmlSerializer xml_templateSerializer = null, xml_favouritesSerializer = null;

        // This is the path to where the 
        // list of favourite templates is stored.
        static string FavouritesFilePath
        {
            get
            {
                return Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("MeshBrushEditor")[0])) + "/FavouriteTemplates.xml";
            }
        }

        // Save the settings of a specific MeshBrush instance
        // (global or inspector) to a MeshBrush template file.
        public static void SaveTemplate(MeshBrush instance)
        {
            // Abort the procedure in case the instance parameter is null.
            if (instance == null)
            {
                Debug.LogError("MeshBrush instance parameter is null!");
                return;
            }

            // Create the serializer.
            if (xml_templateSerializer == null)
            {
                xml_templateSerializer = new XmlSerializer(typeof(TemplateData));
            }

            // Ask the user via a file saving panel for the path where the MeshBrush template shall be stored.
            string templatePath = EditorUtility.SaveFilePanelInProject("Save MeshBrush template", "<template_name>", "meshbrush", "Save your favorite MeshBrush settings to disk for later reusage, so you don't have to set up the brushes again everytime.", "Assets/MeshBrush/Saved Templates");
            if (templatePath.Length != 0 && xml_templateSerializer != null)
            {
                // Create an instance of the TemplateData class (it holds the data we want to store).
                TemplateData data = new TemplateData(instance);

                // Initialize the xml file via the FileStream.
                using (xml_templateFile = new FileStream(templatePath, FileMode.Create))
                {
                    // Serialize the data to the xml file 
                    // (this is where the data actually gets written into the file).
                    xml_templateSerializer.Serialize(xml_templateFile, data);
                }
            }

            // Refresh Unity's AssetDatabase.
            // If we don't do this, the newly 
            // created xml file might not immediately show up in the project panel.
            AssetDatabase.Refresh();
        }

        // Method used to load up a template into a  
        // specific MeshBrush instance (global painting  
        // instance or traditional MeshBrush instance).
        public static bool LoadTemplate(MeshBrush instance)
        {
            if (instance == null)
            {
                Debug.LogError("MeshBrush instance parameter is null!");
                return false;
            }

            if (xml_templateSerializer == null)
            {
                xml_templateSerializer = new XmlSerializer(typeof(TemplateData));
            }

            string templatePath = EditorUtility.OpenFilePanel("Load MeshBrush Template", "Assets/MeshBrush/Saved Templates", "meshbrush");
            if (templatePath != null && templatePath.Length > 0 && xml_templateSerializer != null)
            {
                try
                {
                    // Deserialize the template into our transitory data holder class.
                    TemplateData data;
                    using (xml_templateFile = File.Open(templatePath, FileMode.Open))
                        data = (TemplateData)xml_templateSerializer.Deserialize(xml_templateFile);

                    // If the deserialization was successful, start 
                    // loading up the template data into the instance variables.
                    if (data != null)
                    {
                        instance.isActive = data.active;
                        instance.groupName = data.groupName;

                        if (data.setOfMeshesToPaint != null && data.setOfMeshesToPaint.Length != 0)
                        {
                            instance.setOfMeshesToPaint = new List<GameObject>(data.setOfMeshesToPaint.Length);
                            for (int i = 0; i < data.setOfMeshesToPaint.Length; i++)
                            {
                                instance.setOfMeshesToPaint.Add(string.CompareOrdinal(data.setOfMeshesToPaint[i], "null") == 0 ? null : (GameObject)AssetDatabase.LoadAssetAtPath(data.setOfMeshesToPaint[i], typeof(GameObject)));
                            }
                        }

                        if (data.globalPaintingLayers != null && data.globalPaintingLayers.Length != 0)
                        {
                            instance.globalPaintingLayers = null;
                            instance.globalPaintingLayers = new bool[32];
                            for (int i = 0; i < data.globalPaintingLayers.Length; i++)
                                instance.globalPaintingLayers[i] = data.globalPaintingLayers[i];
                        }

                        instance.paintKey = data.paintKey;
                        instance.deleteKey = data.deleteKey;
                        instance.combineAreaKey = data.combineAreaKey;
                        instance.increaseRadiusKey = data.increaseRadiusKey;
                        instance.decreaseRadiusKey = data.decreaseRadiusKey;

                        instance.hRadius = data.brushRadius;
                        instance.hColor = data.brushColor;

                        instance.meshCount = data.numberOfMeshesToPaint;
                        instance.useRandomMeshCount = data.useRandomNumberOfMeshesToPaint;
                        instance.minNrOfMeshes = data.minNrOfMeshes;
                        instance.maxNrOfMeshes = data.maxNrOfMeshes;
                        instance.delay = data.delay;
                        instance.meshOffset = data.verticalOffset;
                        instance.slopeInfluence = data.slopeInfluence;

                        instance.activeSlopeFilter = data.useSlopeFilter;
                        instance.maxSlopeFilterAngle = data.maxSlopeFilterAngle;
                        instance.inverseSlopeFilter = data.inverseSlopeFilter;
                        instance.manualRefVecSampling = data.manualReferenceVectorSampling;
                        instance.showRefVecInSceneGUI = data.showReferenceVectorInSceneGUI;

                        instance.slopeRefVec = data.slopeReferenceVector;
                        instance.slopeRefVec_HandleLocation = data.slopeReferenceVector_HandleLocation;

                        instance.useOverlapFilter = data.useOverlapFilter;
                        instance.absoluteMinDist = data.absoluteMinDist;
                        instance.useRandomAbsMinDist = data.useRandomAbsMinDist;
                        instance.randomAbsMinDist = data.randomAbsMinDist;

                        instance.yAxisIsTangent = data.yAxisIsTangent;
                        instance.invertY = data.invertY;
                        instance.scattering = data.scattering;
                        instance.autoStatic = data.autoStatic;
                        instance.uniformScale = data.uniformScale;
                        instance.constUniformScale = data.constantUniformScale;
                        instance.rWithinRange = data.randomWithinRange;

                        instance.rScaleW = data.randomScaleWidth;
                        instance.rScaleH = data.randomScaleHidth;
                        instance.rScale = data.randomScale;
                        instance.rUniformRange = data.randomUniformRange;
                        instance.rNonUniformRange = data.randomNonUniformRange;

                        instance.cScale = data.constantAdditiveScale;
                        instance.cScaleXYZ = data.constantScaleXYZ;

                        instance.rRot = data.randomRotation;

                        instance.autoSelectOnCombine = data.autoSelectOnCombine;
                    }

                    return true;
                }
                catch
                {
                    if (!EditorUtility.DisplayDialog("Template deserialization failed!", "MeshBrush Template deserialization failed!\nDid you mess with that template file?", "Yeah, sorry :/", "No..."))
                    {
                        if (EditorUtility.DisplayDialog("Hm...", "Are you sure?", "Yes", "No, lol"))
                            EditorUtility.DisplayDialog("Bug detected", "Sorry for the inconvenience! Please report this bug (including the involved template file) on MeshBrush's main Unity forum thread.\n\nLink:   http://tinyurl.com/MeshBrush", "Okay");
                        else EditorUtility.DisplayDialog("Warning", "Modifying a template file the wrong way can break it. Only modify the file directly if you know what you're doing. A simple change in the file's structure can already make the deserialization process fail.", "Okay");
                    }
                    else EditorUtility.DisplayDialog("Warning", "Modifying a template file the wrong way can break it. Only modify the file directly if you know what you're doing. A simple change in the file's structure can already make the deserialization process fail.", "Okay");

                    // If we have an exception and the deserialization fails, 
                    // we manually close the used xml file and dispose of its resources.
                    xml_templateFile.Close();

                    return false;
                }
            }
            return false;
        }

        // Overload for a specific path.
        public static bool LoadTemplate(MeshBrush instance, string templatePath)
        {
            if (instance == null)
            {
                Debug.LogError("MeshBrush instance parameter is null!");
                return false;
            }

            if (xml_templateSerializer == null)
            {
                xml_templateSerializer = new XmlSerializer(typeof(TemplateData));
            }

            if (templatePath != null && templatePath.Length > 0 && xml_templateSerializer != null)
            {
                try
                {
                    TemplateData data;
                    using (xml_templateFile = File.Open(templatePath, FileMode.Open))
                        data = (TemplateData)xml_templateSerializer.Deserialize(xml_templateFile);

                    if (data != null)
                    {
                        instance.isActive = data.active;
                        instance.groupName = data.groupName;

                        if (data.setOfMeshesToPaint != null && data.setOfMeshesToPaint.Length != 0)
                        {
                            instance.setOfMeshesToPaint = new List<GameObject>(data.setOfMeshesToPaint.Length);
                            for (int i = 0; i < data.setOfMeshesToPaint.Length; i++)
                                instance.setOfMeshesToPaint.Add(string.CompareOrdinal(data.setOfMeshesToPaint[i], "null") == 0 ? null : (GameObject)AssetDatabase.LoadAssetAtPath(data.setOfMeshesToPaint[i], typeof(GameObject)));
                        }

                        if (data.globalPaintingLayers != null && data.globalPaintingLayers.Length != 0)
                        {
                            instance.globalPaintingLayers = null;
                            instance.globalPaintingLayers = new bool[32];

                            for (int i = 0; i < data.globalPaintingLayers.Length; i++)
                                instance.globalPaintingLayers[i] = data.globalPaintingLayers[i];
                        }

                        instance.paintKey = data.paintKey;
                        instance.deleteKey = data.deleteKey;
                        instance.combineAreaKey = data.combineAreaKey;
                        instance.increaseRadiusKey = data.increaseRadiusKey;
                        instance.decreaseRadiusKey = data.decreaseRadiusKey;

                        instance.hRadius = data.brushRadius;
                        instance.hColor = data.brushColor;

                        instance.meshCount = data.numberOfMeshesToPaint;
                        instance.useRandomMeshCount = data.useRandomNumberOfMeshesToPaint;
                        instance.minNrOfMeshes = data.minNrOfMeshes;
                        instance.maxNrOfMeshes = data.maxNrOfMeshes;
                        instance.delay = data.delay;
                        instance.meshOffset = data.verticalOffset;
                        instance.slopeInfluence = data.slopeInfluence;

                        instance.activeSlopeFilter = data.useSlopeFilter;
                        instance.maxSlopeFilterAngle = data.maxSlopeFilterAngle;
                        instance.inverseSlopeFilter = data.inverseSlopeFilter;
                        instance.manualRefVecSampling = data.manualReferenceVectorSampling;
                        instance.showRefVecInSceneGUI = data.showReferenceVectorInSceneGUI;

                        instance.slopeRefVec = data.slopeReferenceVector;
                        instance.slopeRefVec_HandleLocation = data.slopeReferenceVector_HandleLocation;

                        instance.useOverlapFilter = data.useOverlapFilter;
                        instance.absoluteMinDist = data.absoluteMinDist;
                        instance.useRandomAbsMinDist = data.useRandomAbsMinDist;
                        instance.randomAbsMinDist = data.randomAbsMinDist;

                        instance.yAxisIsTangent = data.yAxisIsTangent;
                        instance.invertY = data.invertY;
                        instance.scattering = data.scattering;
                        instance.autoStatic = data.autoStatic;
                        instance.uniformScale = data.uniformScale;
                        instance.constUniformScale = data.constantUniformScale;
                        instance.rWithinRange = data.randomWithinRange;

                        instance.rScaleW = data.randomScaleWidth;
                        instance.rScaleH = data.randomScaleHidth;
                        instance.rScale = data.randomScale;
                        instance.rUniformRange = data.randomUniformRange;
                        instance.rNonUniformRange = data.randomNonUniformRange;

                        instance.cScale = data.constantAdditiveScale;
                        instance.cScaleXYZ = data.constantScaleXYZ;

                        instance.rRot = data.randomRotation;

                        instance.autoSelectOnCombine = data.autoSelectOnCombine;
                    }
                    return true;
                }
                catch
                {
                    if (!EditorUtility.DisplayDialog("Template deserialization failed!", "MeshBrush Template deserialization failed!\nDid you mess with that template file?", "Yeah, sorry :/", "No..."))
                    {
                        if (EditorUtility.DisplayDialog("Hm...", "Are you sure?", "Yes", "No, lol"))
                            EditorUtility.DisplayDialog("Bug detected", "Sorry for the inconvenience! Please report this bug (including the involved template file) on MeshBrush's main Unity forum thread.\n\nLink:   http://tinyurl.com/MeshBrush", "Okay");
                        else EditorUtility.DisplayDialog("Warning", "Modifying a template file the wrong way can break it. Only modify the file directly if you know what you're doing. A simple change in the file's structure can already make the deserialization process fail.", "Okay");
                    }
                    else EditorUtility.DisplayDialog("Warning", "Modifying a template file the wrong way can break it. Only modify the file directly if you know what you're doing. A simple change in the file's structure can already make the deserialization process fail.", "Okay");

                    xml_templateFile.Close();

                    return false;
                }
            }
            return false;
        }

        // Method for saving the favourite templates list.
        public static void SaveFavourites()
        {
            // Create the xml serializer.
            if (xml_favouritesSerializer == null)
            {
                xml_favouritesSerializer = new XmlSerializer(typeof(List<string>));
            }

            // Store the favourites list in an xml file.
            using (xml_favouritesFile = new FileStream(FavouritesFilePath, FileMode.Create))
                xml_favouritesSerializer.Serialize(xml_favouritesFile, MeshBrush.favourites);

            // Refresh the project panel.
            AssetDatabase.Refresh();
        }

        // Loads up the last saved favourite templates list.
        public static void LoadFavourites()
        {
            try
            {
                // Create the xml serializer.
                if (xml_favouritesSerializer == null)
                {
                    xml_favouritesSerializer = new XmlSerializer(typeof(List<string>));
                }

                if (!File.Exists(FavouritesFilePath))
                {
                    Debug.LogWarning("MeshBrush: Favourite templates list file missing. Creating a new one...");
                    SaveFavourites();
                }

                // Open the xml file containing the list of favourite templates.
                using (xml_favouritesFile = File.Open(FavouritesFilePath, FileMode.Open))
                {
                    // Deserialize the stored favourite list from the xml file to a temporary variable.
                    List<string> favourites = (List<string>)xml_favouritesSerializer.Deserialize(xml_favouritesFile);

                    // Clear the old favourites list.
                    MeshBrush.favourites.Clear();

                    // Write the deserialized data into the favourites list.
                    foreach (string favourite in favourites)
                        MeshBrush.favourites.Add(favourite);
                }
            }
            catch
            {
                Debug.LogError("MeshBrush: Favourite templates list deserialization failed! File structure corrupted; creating a new one...");

                xml_favouritesFile.Close();

                MeshBrush.favourites.Clear();
                SaveFavourites();
            }
        }
    }

    // Template data class:
    [Serializable]
    [XmlRoot("MeshBrush_Template")]
    public sealed class TemplateData
    {
        public bool active = true;
        public string groupName = "<group name>";

        public string[] setOfMeshesToPaint = null;

        public bool[] globalPaintingLayers = null;

        public KeyCode paintKey = KeyCode.P;
        public KeyCode deleteKey = KeyCode.L;
        public KeyCode combineAreaKey = KeyCode.K;
        public KeyCode increaseRadiusKey = KeyCode.O;
        public KeyCode decreaseRadiusKey = KeyCode.I;

        public float brushRadius = 0.3f;
        public Color brushColor = Color.white;

        public int numberOfMeshesToPaint = 1;
        public bool useRandomNumberOfMeshesToPaint = false;
        public int minNrOfMeshes = 1;
        public int maxNrOfMeshes = 1;
        public float delay = 0.25f;
        public float verticalOffset = 0.0f;
        public float slopeInfluence = 100.0f;
        
        public bool useSlopeFilter = false;
        public float maxSlopeFilterAngle = 30f;
        public bool inverseSlopeFilter = false;
        public bool manualReferenceVectorSampling = false;
        public bool showReferenceVectorInSceneGUI = true;

        public Vector3 slopeReferenceVector = Vector3.up;
        public Vector3 slopeReferenceVector_HandleLocation = Vector3.zero;

        public bool useOverlapFilter = false;
        public bool useAbsoluteDistance = true;
        public bool useRandomAbsMinDist = false;
        public bool useRandomRelMinDist = false;
        //public bool strictF
        public float absoluteMinDist = 0.5f;     
        public float relativeMinDist = 90f;
        public Vector2 randomAbsMinDist = new Vector2(.5f, 1f);
        public Vector2 randomRelMinDist = new Vector2(90, 110);

        public bool yAxisIsTangent = false;
        public bool invertY = false;
        public float scattering = 60f;
        public bool autoStatic = true;
        public bool uniformScale = true;
        public bool constantUniformScale = true;
        public bool randomWithinRange = false;

        public float randomScaleWidth = 0.0f;
        public float randomScaleHidth = 0.0f;
        public float randomScale = 0.0f;
        public Vector2 randomUniformRange = Vector2.zero;
        public Vector4 randomNonUniformRange = Vector4.zero;

        public float constantAdditiveScale = 0.0f;
        public Vector3 constantScaleXYZ = Vector3.zero;

        public float randomRotation = 0.0f;

        public bool autoSelectOnCombine = true;

        public TemplateData(MeshBrush instance)
        {
            active = instance.isActive;
            groupName = instance.groupName;

            setOfMeshesToPaint = new string[instance.setOfMeshesToPaint.Count];
            for (int i = 0; i < instance.setOfMeshesToPaint.Count; i++)
                setOfMeshesToPaint[i] = instance.setOfMeshesToPaint[i] != null ? AssetDatabase.GetAssetPath(instance.setOfMeshesToPaint[i]) : "null";

            globalPaintingLayers = new bool[instance.globalPaintingLayers.Length];
            for (int i = 0; i < instance.globalPaintingLayers.Length; i++)
                globalPaintingLayers[i] = instance.globalPaintingLayers[i];

            paintKey = instance.paintKey;
            deleteKey = instance.deleteKey;
            combineAreaKey = instance.combineAreaKey;
            increaseRadiusKey = instance.increaseRadiusKey;
            decreaseRadiusKey = instance.decreaseRadiusKey;

            brushRadius = instance.hRadius;
            brushColor = instance.hColor;

            numberOfMeshesToPaint = instance.meshCount;
            useRandomNumberOfMeshesToPaint = instance.useRandomMeshCount;
            minNrOfMeshes = instance.minNrOfMeshes;
            maxNrOfMeshes = instance.maxNrOfMeshes;
            delay = instance.delay;
            verticalOffset = instance.meshOffset;
            slopeInfluence = instance.slopeInfluence;

            useSlopeFilter = instance.activeSlopeFilter;
            maxSlopeFilterAngle = instance.maxSlopeFilterAngle;
            inverseSlopeFilter = instance.inverseSlopeFilter;
            manualReferenceVectorSampling = instance.manualRefVecSampling;
            showReferenceVectorInSceneGUI = instance.showRefVecInSceneGUI;

            slopeReferenceVector = instance.slopeRefVec;
            slopeReferenceVector_HandleLocation = instance.slopeRefVec_HandleLocation;

            useOverlapFilter = instance.useOverlapFilter;
            absoluteMinDist = instance.absoluteMinDist;
            useRandomAbsMinDist = instance.useRandomAbsMinDist;
            randomAbsMinDist = instance.randomAbsMinDist;

            yAxisIsTangent = instance.yAxisIsTangent;
            invertY = instance.invertY;
            scattering = instance.scattering;
            autoStatic = instance.autoStatic;
            uniformScale = instance.uniformScale;
            constantUniformScale = instance.constUniformScale;
            randomWithinRange = instance.rWithinRange;

            randomScaleWidth = instance.rScaleW;
            randomScaleHidth = instance.rScaleH;
            randomScale = instance.rScale;
            randomUniformRange = instance.rUniformRange;
            randomNonUniformRange = instance.rNonUniformRange;

            constantAdditiveScale = instance.cScale;
            constantScaleXYZ = instance.cScaleXYZ;

            randomRotation = instance.rRot;

            autoSelectOnCombine = instance.autoSelectOnCombine;
        }
        public TemplateData() { }
    }
}

// Copyright (C) 2016, Raphael Beck
