#if UNITY_2019_2_OR_NEWER

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Unity.SunStudy
{
    [CustomEditor(typeof(SunStudy))]
    public class SunStudyEditor : Editor
    {
        SerializedProperty m_PlacementModeProp;
        SerializedProperty m_AzimuthProp;
        SerializedProperty m_AltitudeProp;
        SerializedProperty m_NorthDirectionProp;
        SerializedProperty m_CoordinateModeProp;
        SerializedProperty m_CoordLatitudeProp;
        SerializedProperty m_CoordLongitudeProp;
        SerializedProperty m_CoordAddressProp;
        SerializedProperty m_GeocodingMessageProp;
        SerializedProperty m_CoordNSProp;
        SerializedProperty m_CoordEWProp;
        SerializedProperty m_CoordNSDegProp;
        SerializedProperty m_CoordNSMinProp;
        SerializedProperty m_CoordNSSecProp;
        SerializedProperty m_CoordEWDegProp;
        SerializedProperty m_CoordEWMinProp;
        SerializedProperty m_CoordEWSecProp;
        SerializedProperty m_YearProp;
        SerializedProperty m_MonthProp;
        SerializedProperty m_DayProp;
        SerializedProperty m_HourProp;
        SerializedProperty m_MinuteProp;
        SerializedProperty m_UtcOffsetProp;
        SerializedProperty m_SunLightProp;
        SerializedProperty m_IntensityProp;
        SerializedProperty m_ApplyDimmingProp;
        SerializedProperty m_DimmingProp;

        bool m_ShowPlacement    = true;
        bool m_ShowCoordinates  = true;
        bool m_ShowTime         = true;
        bool m_ShowIntensity    = true;
        bool m_ShowGeocodingApi = false;

        static readonly string[] k_MonthNames = DateTimeFormatInfo.InvariantInfo.MonthNames;

        // Canonical UTC offsets (including UTC DST offsets).
        // See en.wikipedia.org/wiki/List_of_tz_database_time_zones for details.
        static readonly Dictionary<string, int> k_UtcOffsets = new Dictionary<string, int>()
        {
            { "−12:00", -1200 },
            { "−11:00", -1100 },
            { "−10:00", -1000 },
            { "−09:30",  -950 },
            { "−09:00",  -900 },
            { "−08:00",  -800 },
            { "−07:00",  -700 },
            { "−06:00",  -600 },
            { "−05:00",  -500 },
            { "−04:00",  -400 },
            { "−03:30",  -350 },
            { "−03:00",  -300 },
            { "−02:30",  -250 },
            { "−02:00",  -200 },
            { "−01:00",  -100 },
            { "+00:00",   000 },
            { "+01:00",  +100 },
            { "+02:00",  +200 },
            { "+03:00",  +300 },
            { "+03:30",  +350 },
            { "+04:00",  +400 },
            { "+04:30",  +450 },
            { "+05:00",  +500 },
            { "+05:30",  +550 },
            { "+05:45",  +575 },
            { "+06:00",  +600 },
            { "+06:30",  +650 },
            { "+07:00",  +700 },
            { "+08:00",  +800 },
            { "+08:45",  +875 },
            { "+09:00",  +900 },
            { "+09:30",  +950 },
            { "+10:00", +1000 },
            { "+10:30", +1050 },
            { "+11:00", +1100 },
            { "+12:00", +1200 },
            { "+12:45", +1275 },
            { "+13:00", +1300 },
            { "+13:45", +1375 },
            { "+14:00", +1400 }
        };

        static readonly string[] k_UtcOffsetNames  = k_UtcOffsets.Keys.ToArray();
        static readonly int[]    k_UtcOffsetValues = k_UtcOffsets.Values.ToArray();

        void OnEnable()
        {
            m_PlacementModeProp    = serializedObject.FindProperty("PlacementMode");
            m_AzimuthProp          = serializedObject.FindProperty("Azimuth");
            m_AltitudeProp         = serializedObject.FindProperty("Altitude");
            m_NorthDirectionProp   = serializedObject.FindProperty("NorthDirection");
            m_CoordinateModeProp   = serializedObject.FindProperty("CoordinateMode");
            m_CoordLatitudeProp    = serializedObject.FindProperty("CoordLatitude");
            m_CoordLongitudeProp   = serializedObject.FindProperty("CoordLongitude");
            m_CoordAddressProp     = serializedObject.FindProperty("CoordAddress");
            m_GeocodingMessageProp = serializedObject.FindProperty("GeocodingMessage");
            m_CoordNSProp          = serializedObject.FindProperty("CoordNS");
            m_CoordEWProp          = serializedObject.FindProperty("CoordEW");
            m_CoordNSDegProp       = serializedObject.FindProperty("CoordNSDeg");
            m_CoordNSMinProp       = serializedObject.FindProperty("CoordNSMin");
            m_CoordNSSecProp       = serializedObject.FindProperty("CoordNSSec");
            m_CoordEWDegProp       = serializedObject.FindProperty("CoordEWDeg");
            m_CoordEWMinProp       = serializedObject.FindProperty("CoordEWMin");
            m_CoordEWSecProp       = serializedObject.FindProperty("CoordEWSec");
            m_YearProp             = serializedObject.FindProperty("Year");
            m_MonthProp            = serializedObject.FindProperty("Month");
            m_DayProp              = serializedObject.FindProperty("Day");
            m_HourProp             = serializedObject.FindProperty("Hour");
            m_MinuteProp           = serializedObject.FindProperty("Minute");
            m_UtcOffsetProp        = serializedObject.FindProperty("UtcOffset");
            m_SunLightProp         = serializedObject.FindProperty("SunLight");
            m_IntensityProp        = serializedObject.FindProperty("Intensity");
            m_ApplyDimmingProp     = serializedObject.FindProperty("ApplyDimming");
            m_DimmingProp          = serializedObject.FindProperty("Dimming");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (m_ShowPlacement = EditorGUILayout.Foldout(m_ShowPlacement, "Placement"))
            {
                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    EditorGUILayout.PropertyField(m_PlacementModeProp, new GUIContent("Mode"));

                    if (check.changed)
                    {
                        m_ShowCoordinates = true;
                        m_ShowTime        = true;
                        m_ShowIntensity   = true;
                    }
                }

                if (m_PlacementModeProp.enumValueIndex == (int)SunPlacementMode.Static)
                {
                    EditorGUILayout.Slider(m_AzimuthProp, 0f, 360f, "Azimuth");
                    EditorGUILayout.Slider(m_AltitudeProp, -90f, +90f, "Altitude");
                }
                else // SunPlacementMode.Geographical
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.PropertyField(m_AzimuthProp, new GUIContent("Azimuth"));
                        EditorGUILayout.PropertyField(m_AltitudeProp, new GUIContent("Altitude"));
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel("True North");

                    using (var check = new EditorGUI.ChangeCheckScope())
                    {
                        Vector2 northDirection = m_NorthDirectionProp.vector2Value;

                        EditorGUILayout.LabelField("X", GUILayout.Width(10));
                        northDirection.x = EditorGUILayout.FloatField(northDirection.x);
                        EditorGUILayout.LabelField("Z", GUILayout.Width(10));
                        northDirection.y = EditorGUILayout.FloatField(northDirection.y);
                        EditorGUILayout.LabelField("", GUILayout.Width(50));

                        if (check.changed)
                        {
                            m_NorthDirectionProp.vector2Value = northDirection;
                        }
                    }
                }

                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    float northAngle = EditorGUILayout.Slider(" ", SunStudy.GetNorthAngle(m_NorthDirectionProp.vector2Value), 0f, 360f);

                    if (check.changed)
                    {
                        m_NorthDirectionProp.vector2Value = m_NorthDirectionProp.vector2Value.magnitude * SunStudy.GetNorthDirection(northAngle);
                    }
                }
            }

            bool placementGeographical = m_PlacementModeProp.enumValueIndex == (int)SunPlacementMode.Geographical;

            using (new EditorGUI.DisabledScope(!placementGeographical))
            {
                m_ShowCoordinates = EditorGUILayout.Foldout(m_ShowCoordinates, "Coordinates") && placementGeographical;

                if (m_ShowCoordinates)
                {
                    EditorGUILayout.PropertyField(m_CoordinateModeProp, new GUIContent("Mode"));

                    if (m_CoordinateModeProp.enumValueIndex == (int)SunCoordinateMode.LatitudeLongitude)
                    {
                        EditorGUILayout.Slider(m_CoordLatitudeProp, -90f, +90f, "Latitude");
                        EditorGUILayout.Slider(m_CoordLongitudeProp, -180f, +180f, "Longitude");
                    }
                    else // SunCoordinateMode.Address or SunCoordinateMode.DegreesMinutesSeconds
                    {
                        using (new EditorGUI.DisabledScope(true))
                        {
                            EditorGUILayout.PropertyField(m_CoordLatitudeProp, new GUIContent("Latitude"));
                            EditorGUILayout.PropertyField(m_CoordLongitudeProp, new GUIContent("Longitude"));
                        }

                        if (m_CoordinateModeProp.enumValueIndex == (int)SunCoordinateMode.Address)
                        {
                            EditorGUILayout.DelayedTextField(m_CoordAddressProp, new GUIContent("Address"));

                            using (new EditorGUI.DisabledScope(true))
                            {
                                EditorGUILayout.PropertyField(m_GeocodingMessageProp, new GUIContent("Message"));
                            }
                        }
                        else // SunCoordinateMode.DegreesMinutesSeconds
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                EditorGUILayout.PrefixLabel(" ");

                                EditorGUILayout.LabelField("", GUILayout.Width(0.1f));

                                using (var check = new EditorGUI.ChangeCheckScope())
                                {
                                    int enumValueIndex = GUILayout.SelectionGrid(m_CoordNSProp.enumValueIndex, new string[] { "N", "S" }, 2, EditorStyles.radioButton, GUILayout.Width(60));

                                    if (check.changed)
                                    {
                                        m_CoordNSProp.enumValueIndex = enumValueIndex;
                                    }
                                }

                                EditorGUILayout.LabelField("", GUILayout.Width(5));

                                EditorGUILayout.PropertyField(m_CoordNSDegProp, GUIContent.none, GUILayout.Width(30));
                                EditorGUILayout.LabelField("deg", GUILayout.Width(25));
                                EditorGUILayout.PropertyField(m_CoordNSMinProp, GUIContent.none, GUILayout.Width(30));
                                EditorGUILayout.LabelField("min", GUILayout.Width(25));
                                EditorGUILayout.PropertyField(m_CoordNSSecProp, GUIContent.none, GUILayout.Width(30));
                                EditorGUILayout.LabelField("sec", GUILayout.Width(25));
                            }

                            using (new EditorGUILayout.HorizontalScope())
                            {
                                EditorGUILayout.PrefixLabel(" ");

                                EditorGUILayout.LabelField("", GUILayout.Width(0.1f));

                                using (var check = new EditorGUI.ChangeCheckScope())
                                {
                                    int enumValueIndex = GUILayout.SelectionGrid(m_CoordEWProp.enumValueIndex, new string[] { "E", "W" }, 2, EditorStyles.radioButton, GUILayout.Width(60));

                                    if (check.changed)
                                    {
                                        m_CoordEWProp.enumValueIndex = enumValueIndex;
                                    }
                                }

                                EditorGUILayout.LabelField("", GUILayout.Width(5));

                                EditorGUILayout.PropertyField(m_CoordEWDegProp, GUIContent.none, GUILayout.Width(30));
                                EditorGUILayout.LabelField("deg", GUILayout.Width(25));
                                EditorGUILayout.PropertyField(m_CoordEWMinProp, GUIContent.none, GUILayout.Width(30));
                                EditorGUILayout.LabelField("min", GUILayout.Width(25));
                                EditorGUILayout.PropertyField(m_CoordEWSecProp, GUIContent.none, GUILayout.Width(30));
                                EditorGUILayout.LabelField("sec", GUILayout.Width(25));
                            }
                        }
                    }
                }

                m_ShowTime = EditorGUILayout.Foldout(m_ShowTime, "Time") && placementGeographical;

                if (m_ShowTime)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PrefixLabel("Time");

                        EditorGUILayout.PropertyField(m_YearProp, GUIContent.none, GUILayout.Width(35));
                        EditorGUILayout.LabelField("−", GUILayout.Width(10));

                        using (var check = new EditorGUI.ChangeCheckScope())
                        {
                            int monthValue = EditorGUILayout.Popup(m_MonthProp.intValue - 1, k_MonthNames, GUILayout.Width(75));

                            if (check.changed)
                            {
                                m_MonthProp.intValue = monthValue + 1;
                            }
                        }

                        EditorGUILayout.LabelField("−", GUILayout.Width(10));
                        EditorGUILayout.PropertyField(m_DayProp, GUIContent.none, GUILayout.Width(20));

                        EditorGUILayout.LabelField("", GUILayout.Width(10));

                        EditorGUILayout.PropertyField(m_HourProp, GUIContent.none, GUILayout.Width(20));
                        EditorGUILayout.LabelField(":", GUILayout.Width(7));
                        EditorGUILayout.PropertyField(m_MinuteProp, GUIContent.none, GUILayout.Width(20));
                        EditorGUILayout.LabelField(": 00", GUILayout.Width(25));
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PrefixLabel(" ");

                        EditorGUILayout.LabelField("UTC Offset", GUILayout.Width(65));

                        using (var check = new EditorGUI.ChangeCheckScope())
                        {
                            int utcOffsetValue = EditorGUILayout.IntPopup((int)(m_UtcOffsetProp.floatValue * 100f), k_UtcOffsetNames, k_UtcOffsetValues, GUILayout.Width(60));

                            if (check.changed)
                            {
                                m_UtcOffsetProp.floatValue = utcOffsetValue / 100f;
                            }
                        }
                    }

                    using (var check = new EditorGUI.ChangeCheckScope())
                    {
                        int dayOfYear = EditorGUILayout.IntSlider("Day of Year", SunStudy.GetDayOfYear(m_YearProp.intValue, m_MonthProp.intValue, m_DayProp.intValue), 1, SunStudy.GetDayOfYear(m_YearProp.intValue, 12, 31));

                        if (check.changed)
                        {
                            (m_MonthProp.intValue, m_DayProp.intValue) = SunStudy.SetDayOfYear(m_YearProp.intValue, dayOfYear);
                        }
                    }

                    using (var check = new EditorGUI.ChangeCheckScope())
                    {
                        int minuteOfDay = EditorGUILayout.IntSlider("Minute of Day", SunStudy.GetMinuteOfDay(m_HourProp.intValue, m_MinuteProp.intValue), 0, 1439);

                        if (check.changed)
                        {
                            (m_HourProp.intValue, m_MinuteProp.intValue) = SunStudy.SetMinuteOfDay(minuteOfDay);
                        }
                    }
                }

                m_ShowIntensity = EditorGUILayout.Foldout(m_ShowIntensity, "Intensity") && placementGeographical;

                if (m_ShowIntensity)
                {
                    EditorGUILayout.PropertyField(m_SunLightProp);

                    using (new EditorGUI.DisabledScope(m_SunLightProp.objectReferenceValue == null))
                    {
                        EditorGUILayout.PropertyField(m_IntensityProp);
                        EditorGUILayout.PropertyField(m_ApplyDimmingProp);
                    }

                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.PropertyField(m_DimmingProp);
                    }
                }

                bool coordinateAddress = m_CoordinateModeProp.enumValueIndex == (int)SunCoordinateMode.Address;

                using (new EditorGUI.DisabledScope(!coordinateAddress))
                {
                    m_ShowGeocodingApi = EditorGUILayout.Foldout(m_ShowGeocodingApi, "Geocoding API") && placementGeographical && coordinateAddress;

                    if (m_ShowGeocodingApi)
                    {
                        using (var check = new EditorGUI.ChangeCheckScope())
                        {
                            string geocodingApiKey = EditorGUILayout.TextField("Key", PlayerPrefs.GetString(SunStudy.k_PlayerPrefGeocodingApiKey));

                            if (check.changed)
                            {
                                PlayerPrefs.SetString(SunStudy.k_PlayerPrefGeocodingApiKey, geocodingApiKey);
                            }
                        }
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}

#endif
