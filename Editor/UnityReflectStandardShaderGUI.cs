using System;
using UnityEngine;
using UnityEngine.Reflect;

namespace UnityEditor.Reflect
{
    public class UnityReflectStandardShaderGUI : ShaderGUI
    {
        class MapProperties
        {
            public MaterialProperty map;
            public MaterialProperty brightness;
            public MaterialProperty rotation;
            public MaterialProperty invert;
            public bool foldout;
            
            public MapProperties(string name, MaterialProperty[] props)
            {
                map = FindProperty(name, props);
                brightness = FindProperty(name + "_B", props);
                rotation = FindProperty(name + "_R", props);
                invert = FindProperty(name + "_I", props);
                foldout = false;
            }
        }

        MaterialProperty m_Tint;
        MapProperties m_AlbedoMap;
        MaterialProperty m_AlbedoColor;
        MaterialProperty m_AlbedoFade;
        
        MaterialProperty m_BumpScale;
        MapProperties m_BumpMap;
        
        MaterialProperty m_Smoothness;
        MapProperties m_SmoothnessMap;
        
        MaterialProperty m_Metallic;
        MapProperties m_MetallicMap;
        
        MaterialProperty m_EmissionMode;
        MaterialProperty m_Emission;
        MapProperties m_EmissionMap;

        MaterialProperty m_CutoutThreshold;
        MapProperties m_CutoutMap;
        
        MaterialProperty m_Alpha;
        MapProperties m_AlphaMap;
        
        MaterialEditor m_MaterialEditor;

        bool m_FirstTimeApply = true;

        void FindProperties(MaterialProperty[] props)
        {
            m_Tint = FindProperty("_Tint", props);
            m_AlbedoMap = new MapProperties("_MainTex", props);
            m_AlbedoColor = FindProperty("_AlbedoColor", props);
            m_AlbedoFade = FindProperty("_MainTex_Fade", props);
        
            m_BumpScale = FindProperty("_BumpScale", props);
            m_BumpMap = new MapProperties("_BumpMap", props);
        
            m_Smoothness = FindProperty("_Smoothness", props);
            m_SmoothnessMap = new MapProperties("_SmoothnessMap", props);
        
            m_Metallic = FindProperty("_Metallic", props);
            m_MetallicMap = new MapProperties("_MetallicMap", props);
        
            m_EmissionMode = FindProperty("_EmissionMode", props);
            m_Emission = FindProperty("_Emission", props);
            m_EmissionMap = new MapProperties("_EmissionMap", props);

            m_CutoutThreshold = FindProperty("_CutoutThreshold", props, false);
            if (m_CutoutThreshold != null)
                m_CutoutMap = new MapProperties("_CutoutMap", props);
            
            m_Alpha = FindProperty("_Alpha", props, false);
            if (m_Alpha != null)
                m_AlphaMap = new MapProperties("_AlphaMap", props);
        }

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
        {            
            var material = materialEditor.target as Material;
            if (m_FirstTimeApply)
            {
                FindProperties(props);
                m_MaterialEditor = materialEditor;
                
                StandardShaderHelper.ComputeKeywords(material);
                m_FirstTimeApply = false;
            }

            ShaderPropertiesGUI(material);
        }

        void ShaderPropertiesGUI(Material material)
        {
            EditorGUIUtility.labelWidth = 0f;

            DoAlbedoArea();
            EditorGUILayout.Space();

            DoNormalArea();
            EditorGUILayout.Space();

            if (DoCutoutArea())
                EditorGUILayout.Space();
                
            if (DoAlphaArea())
                EditorGUILayout.Space();
           
            DoSmoothnessArea();
            EditorGUILayout.Space();
            
            DoMetallicArea();
            EditorGUILayout.Space();
            
            DoEmissionArea();
            EditorGUILayout.Space();

            DoTintArea();
            
            StandardShaderHelper.ComputeKeywords(material);

            EditorGUILayout.Space();
            
            GUILayout.Label("Advanced", EditorStyles.boldLabel);
            m_MaterialEditor.EnableInstancingField();
            m_MaterialEditor.DoubleSidedGIField();
        }
        
        void DoMapProperty(MapProperties mapProperties, string name)
        {
            m_MaterialEditor.TextureProperty(mapProperties.map, name);

            ++EditorGUI.indentLevel;
            ++EditorGUI.indentLevel;

            using (new EditorGUI.DisabledScope(mapProperties.map.textureValue == null))
            {
                mapProperties.foldout = EditorGUILayout.Foldout(mapProperties.foldout, "more");
            
                if (mapProperties.foldout)
                {
                    ++EditorGUI.indentLevel;
                    m_MaterialEditor.ShaderProperty(mapProperties.brightness, "Brightness");
                    m_MaterialEditor.ShaderProperty(mapProperties.rotation, "Rotation");
                    m_MaterialEditor.ShaderProperty(mapProperties.invert, "Invert");
                    --EditorGUI.indentLevel;
                }
            }
            
            --EditorGUI.indentLevel;
            --EditorGUI.indentLevel;
        }

        void DoAlbedoArea()
        {
            EditorGUILayout.LabelField("Albedo");
            ++EditorGUI.indentLevel;
            m_MaterialEditor.ShaderProperty(m_AlbedoColor, "Color");

            DoMapProperty(m_AlbedoMap, "Map");
            
            ++EditorGUI.indentLevel;
            using (new EditorGUI.DisabledScope(m_AlbedoMap.map.textureValue == null))
            {
                m_MaterialEditor.ShaderProperty(m_AlbedoFade, "Image Fade");
            }
            --EditorGUI.indentLevel;
            --EditorGUI.indentLevel;
        }
        
        void DoNormalArea()
        {
            EditorGUILayout.LabelField("Normal");
            ++EditorGUI.indentLevel;
            DoMapProperty(m_BumpMap, "Map");
            
            using (new EditorGUI.DisabledScope(m_BumpMap.map.textureValue == null))
            {
                ++EditorGUI.indentLevel;
                m_MaterialEditor.ShaderProperty(m_BumpScale, "Scale");
                --EditorGUI.indentLevel;
            }
            --EditorGUI.indentLevel;
        }

        void DoSmoothnessArea()
        {
            EditorGUILayout.LabelField("Smoothness");
            ++EditorGUI.indentLevel;
            m_MaterialEditor.ShaderProperty(m_Smoothness, "Smoothness");

            using (new EditorGUI.DisabledScope(m_Smoothness.floatValue.Equals(0.0f)))
            {
                DoMapProperty(m_SmoothnessMap, "Map");
            }
            --EditorGUI.indentLevel;
        }
        
        void DoMetallicArea()
        {
            EditorGUILayout.LabelField("Metallic");
            ++EditorGUI.indentLevel;
            m_MaterialEditor.ShaderProperty(m_Metallic, "Metallic");

            using (new EditorGUI.DisabledScope(m_Metallic.floatValue.Equals(0.0f)))
            {
                DoMapProperty(m_MetallicMap, "Map");
            }
            --EditorGUI.indentLevel;
        }
        
        void DoEmissionArea()
        {
            EditorGUILayout.LabelField("Emission");
            ++EditorGUI.indentLevel;
            EditorGUI.BeginChangeCheck();
            
            m_EmissionMode.floatValue = EditorGUILayout.Popup("Emission Mode", (int)m_EmissionMode.floatValue,
                Enum.GetNames(typeof(EmissionMode)));

            var mode = (EmissionMode) m_EmissionMode.floatValue;
            if (mode == EmissionMode.Color)
            {
                m_MaterialEditor.ShaderProperty(m_Emission, "Color");    
            }
            else if (mode == EmissionMode.Map)
            {
                DoMapProperty(m_EmissionMap, "Map");    
            }
            --EditorGUI.indentLevel;
        }
        
        bool DoCutoutArea()
        {
            if (m_CutoutThreshold == null)
                return false;

            EditorGUILayout.LabelField("Cutout");
            ++EditorGUI.indentLevel;
            DoMapProperty(m_CutoutMap, "Map");

            using (new EditorGUI.DisabledScope(m_CutoutMap.map.textureValue == null))
            {
                ++EditorGUI.indentLevel;
                m_MaterialEditor.ShaderProperty(m_CutoutThreshold, "Cutout");
                --EditorGUI.indentLevel;
            }
            --EditorGUI.indentLevel;
            return true;
        }
        
        bool DoAlphaArea()
        {
            if (m_Alpha == null)
                return false;
            
            EditorGUILayout.LabelField("Alpha");
            ++EditorGUI.indentLevel;
            m_MaterialEditor.ShaderProperty(m_Alpha, "Alpha");

            DoMapProperty(m_AlphaMap, "Map");
            --EditorGUI.indentLevel;
            return true;
        }

        void DoTintArea()
        {
            EditorGUILayout.LabelField("Tint");
            ++EditorGUI.indentLevel;
            m_MaterialEditor.ShaderProperty(m_Tint, "Tint");
            --EditorGUI.indentLevel;
        }

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            base.AssignNewShaderToMaterial(material, oldShader, newShader);
            m_FirstTimeApply = true;
        }
    }
}
