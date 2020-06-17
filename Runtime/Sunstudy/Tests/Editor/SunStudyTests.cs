using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Unity.SunStudy.Tests
{
    // Background information for these structures: https://forum.unity.com/threads/how-parse-json-data-in-unity-in-c.383804/
    // WARNING: The data members in these structures have to be public fields, not properties, and have the exact spelling used in the JSON.
    namespace SunStudyTestJson
    {
        [System.Serializable]
        public struct RootTest
        {
            public string      name;
            public float       latitude;
            public float       longitude;
            public double      utcOffset;
            public TestValue[] values;
        }

        [System.Serializable]
        public struct TestValue
        {
            public int   dateY;
            public int   dateM;
            public int   dateD;
            public int   timeH;
            public int   timeM;
            public int   timeS;
            public float azimuth;
            public float elevation;
            public float localSunriseHourFraction; // fractional hour as from https://midcdmz.nrel.gov/solpos/spa.html
            public float localSunsetHourFraction;  // fractional hour as from https://midcdmz.nrel.gov/solpos/spa.html
        }
    }

    class SunStudyBaseTests
    {
        SunStudyTestJson.RootTest LoadTestData(string testFileName)
        {
            string testFilePath = Path.Combine(Application.dataPath, "Tests", "Editor", "Data", testFileName);

            if (!File.Exists(testFilePath))
            {
                string[] guids = AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(testFileName));
                if (guids.Length > 0)
                {
                    foreach (var guid in guids)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        if (File.Exists(path))
                        {
                            testFilePath = path;
                            break;
                        }
                    }
                }
            }

            string json = File.ReadAllText(testFilePath);
            SunStudyTestJson.RootTest testData = JsonUtility.FromJson<SunStudyTestJson.RootTest>(json);

            return testData;
        }

        protected void ExecuteSolarCycleTest(float errorThresholdMinutes, string testJsonFileName)
        {
            SunStudyTestJson.RootTest testData = LoadTestData(testJsonFileName);

            var geoCoordinates  = Tuple.Create<float, float>(testData.latitude, testData.longitude);
            int utcOffsetHour   = (int)testData.utcOffset;
            int utcOffsetMinute = (int)((testData.utcOffset - utcOffsetHour) * 60f);
            var offset          = new TimeSpan(utcOffsetHour, utcOffsetMinute, 0);

            foreach (var value in testData.values)
            {
                if (value.timeH != 0)
                {
                    // For solar tests, we can test only one hour entry per day since we are testing sunrise and sunset times.
                    continue;
                }

                double sunriseCheck        = value.localSunriseHourFraction;
                int    sunriseCheckHour    = (int)sunriseCheck;
                double sunriseCheckMinutes = sunriseCheckHour * 60.0 + (sunriseCheck - sunriseCheckHour);

                double sunsetCheck        = value.localSunsetHourFraction;
                int    sunsetCheckHour    = (int)sunsetCheck;
                double sunsetCheckMinutes = sunsetCheckHour * 60.0 + (sunsetCheck - sunsetCheckHour);

                var solarCheckDate = new DateTimeOffset(value.dateY, value.dateM, value.dateD, 0, 0, 0, offset);
                (double julianSunrise, double julianSunset, double transit) = SunStudy.GetJulianDatesForSunriseAndSunset(solarCheckDate, geoCoordinates.Item1, geoCoordinates.Item2);

                DateTimeOffset sunriseDateTime = SunStudy.MakeDateTimeOffsetWithJulianDateTimeFraction(julianSunrise, solarCheckDate);
                DateTimeOffset sunsetDateTime  = SunStudy.MakeDateTimeOffsetWithJulianDateTimeFraction(julianSunset, solarCheckDate);

                double sunriseTestMinutes = sunriseDateTime.Hour * 60.0 + sunriseDateTime.Minute;
                double sunsetTestMinutes  = sunsetDateTime.Hour * 60.0 + sunsetDateTime.Minute;

                Assert.AreEqual(sunriseCheckMinutes, sunriseTestMinutes, errorThresholdMinutes, $"Sunrise time not equal with {errorThresholdMinutes} minutes accuracy: JD Sunrise = {julianSunrise}, SPA Check JD value = {sunriseCheck}");
                Assert.AreEqual(sunsetCheckMinutes, sunsetTestMinutes, errorThresholdMinutes, $"Sunset time not equal with {errorThresholdMinutes} minutes accuracy: JD Sunrise = {julianSunrise}, SPA Check JD value = {sunsetCheck}");
            }
        }

        protected void ExecuteAzimuthTest(float errorThreshold, string testJsonFileName)
        {
            ExecuteTest(errorThreshold, testJsonFileName, true);
        }

        protected void ExecuteElevationTest(float errorThreshold, string testJsonFileName)
        {
            ExecuteTest(errorThreshold, testJsonFileName, false);
        }

        void ExecuteTest(float errorThreshold, string testJsonFileName, bool testAzimuth)
        {
            SunStudyTestJson.RootTest testData = LoadTestData(testJsonFileName);

            var geoCoordinates  = Tuple.Create<float, float>(testData.latitude, testData.longitude);
            int utcOffsetHour   = (int)testData.utcOffset;
            int utcOffsetMinute = (int)((testData.utcOffset - utcOffsetHour) * 60f);
            var offset          = new TimeSpan(utcOffsetHour, utcOffsetMinute, 0);

            var dateTimeInputs  = new List<DateTimeOffset>();
            var expectedOutputs = new List<Tuple<float, float>>();

            foreach (var test in testData.values)
            {
                dateTimeInputs.Add(new DateTimeOffset(test.dateY, test.dateM, test.dateD, test.timeH, test.timeM, test.timeS, offset));
                expectedOutputs.Add(Tuple.Create<float, float>(test.azimuth, test.elevation));
            }

            Assert.IsTrue(dateTimeInputs.Count == expectedOutputs.Count);

            for (int testCaseIndex = 0; testCaseIndex < dateTimeInputs.Count; ++testCaseIndex)
            {
                DateTimeOffset dateTime       = dateTimeInputs[testCaseIndex];
                var            expectedResult = expectedOutputs[testCaseIndex];
                (float resultAzimuth, float resultAltitude) = SunStudy.CalculateSunPosition(dateTime, geoCoordinates.Item1, geoCoordinates.Item2);

                if (testAzimuth)
                {
                    Assert.AreEqual(expectedResult.Item1, resultAzimuth, errorThreshold, $"Azimuth not equal with {errorThreshold} accuracy at Hour {dateTime.Hour}.");
                }
                else
                {
                    Assert.AreEqual(expectedResult.Item2, resultAltitude, errorThreshold, $"Elevation not equal with {errorThreshold} accuracy at Hour {dateTime.Hour}.");
                }
            }
        }
    }

    class SunStudySunriseSunsetTests : SunStudyBaseTests
    {
        const float k_ErrorThresholdMinutes = 60f;

        [Test]
        public void SunStudyAlertAlwaysDayTimeDuringSummer2019()
        {
            double latitude  = 82.496299;
            double longitude = -62.359018;

            var alertSummerDateDayAt2AM = new DateTimeOffset(2019, 06, 21, 2, 0, 0, new TimeSpan(-4, 0, 0));
            float testAlwaysDay = SunStudy.CalculateSolarDimming(alertSummerDateDayAt2AM, latitude, longitude, 16); // using a fake altitude
            Debug.Assert(testAlwaysDay == 1);

            var alertSummerDateDayAt10PM = new DateTimeOffset(2019, 06, 21, 22, 0, 0, new TimeSpan(-4, 0, 0));
            testAlwaysDay = SunStudy.CalculateSolarDimming(alertSummerDateDayAt10PM, latitude, longitude, 16); // using a fake altitude
            Debug.Assert(testAlwaysDay == 1);
        }

        [Test]
        public void SunStudyAlertAlwaysNightTimeDuringWinter2019()
        {
            double latitude  = 82.496299;
            double longitude = -62.359018;

            var alertWinterDateDayAtNoon = new DateTimeOffset(2019, 12, 3, 0, 01, 0, new TimeSpan(-5, 0, 0));
            float testAlwaysNightRatio = SunStudy.CalculateSolarDimming(alertWinterDateDayAtNoon, latitude, longitude, 16); // using a fake altitude
            Debug.Assert(testAlwaysNightRatio == 0);
        }

        [Test]
        public void SunStudyAlertAlwaysNightTimeDuringWinter2020()
        {
            double latitude  = 82.496299;
            double longitude = -62.359018;

            var alertWinterDateDayAtNoon = new DateTimeOffset(2020, 1, 2, 12, 0, 0, new TimeSpan(-5, 0, 0));
            float testAlwaysNightRatio = SunStudy.CalculateSolarDimming(alertWinterDateDayAtNoon, latitude, longitude, 16); // using a fake altitude
            Debug.Assert(testAlwaysNightRatio == 0);
        }

        [Test]
        public void SunStudyMurmanskAlwaysDayTimeDuringSummer2019()
        {
            double latitude  = 68.971557;
            double longitude = 33.070630;

            var alertSummerDateDayAt2AM = new DateTimeOffset(2019, 06, 21, 2, 0, 0, new TimeSpan(-4, 0, 0));
            float testAlwaysDay = SunStudy.CalculateSolarDimming(alertSummerDateDayAt2AM, latitude, longitude, 16); // using a fake altitude
            Debug.Assert(testAlwaysDay == 1);

            var alertSummerDateDayAt10PM = new DateTimeOffset(2019, 06, 21, 22, 0, 0, new TimeSpan(-4, 0, 0));
            testAlwaysDay = SunStudy.CalculateSolarDimming(alertSummerDateDayAt10PM, latitude, longitude, 16); // using a fake altitude
            Debug.Assert(testAlwaysDay == 1);
        }

        [Test]
        public void SunStudyMurmanskAlwaysNightTimeDuringWinter2019()
        {
            double latitude  = 68.971557;
            double longitude = 33.070630;

            var alertWinterDateDayAtNoon = new DateTimeOffset(2019, 12, 3, 0, 01, 0, new TimeSpan(-5, 0, 0));
            float testAlwaysNightRatio = SunStudy.CalculateSolarDimming(alertWinterDateDayAtNoon, latitude, longitude, 16); // using a fake altitude
            Debug.Assert(testAlwaysNightRatio == 0);
        }

        [Test]
        public void SunStudyMurmanskAlwaysNightTimeDuringWinter2020()
        {
            double latitude  = 68.971557;
            double longitude = 33.070630;

            var alertWinterDateDayAtNoon = new DateTimeOffset(2020, 1, 2, 12, 0, 0, new TimeSpan(-5, 0, 0));
            float testAlwaysNightRatio = SunStudy.CalculateSolarDimming(alertWinterDateDayAtNoon, latitude, longitude, 16); // using a fake altitude
            Debug.Assert(testAlwaysNightRatio == 0);
        }

        [Test]
        public void SunStudyAntarticaDavisAlwaysNightTimeDuringWinter2019()
        {
            double latitude  = -68.682687;
            double longitude = 79.295626;

            var davisWinterDateNightAtNoon = new DateTimeOffset(2019, 06, 21, 12, 0, 0, new TimeSpan(-4, 0, 0));
            float testAlwayNightRatio = SunStudy.CalculateSolarDimming(davisWinterDateNightAtNoon, latitude, longitude, 1); // using a fake altitude
            Debug.Assert(testAlwayNightRatio == 0);
        }

        [Test]
        public void SunStudyAntarticaDavisAlwaysDayTimeDuringSummer2019()
        {
            double latitude  = -68.682687;
            double longitude = 79.295626;

            var davisSummerDateAt2AM = new DateTimeOffset(2019, 12, 15, 2, 0, 0, new TimeSpan(13, 0, 0));
            float testAlwaysDay = SunStudy.CalculateSolarDimming(davisSummerDateAt2AM, latitude, longitude, 1); // using a fake altitude
            Debug.Assert(testAlwaysDay == 1);

            var davisSummerDateAt10PM = new DateTimeOffset(2019, 12, 27, 22, 0, 0, new TimeSpan(13, 0, 0));
            testAlwaysDay = SunStudy.CalculateSolarDimming(davisSummerDateAt10PM, latitude, longitude, 1); // using a fake altitude
            Debug.Assert(testAlwaysDay == 1);
        }

        [Test]
        public void SunStudyAntarticaDavisAlwaysDayTimeDuringSummer2020()
        {
            double latitude  = -68.682687;
            double longitude = 79.295626;

            var davisSummerDateAt2AM = new DateTimeOffset(2020, 01, 11, 2, 0, 0, new TimeSpan(13, 0, 0));
            float testAlwaysDay = SunStudy.CalculateSolarDimming(davisSummerDateAt2AM, latitude, longitude, 1); // using a fake altitude
            Debug.Assert(testAlwaysDay == 1);

            var davisSummerDateAt10PM = new DateTimeOffset(2020, 01, 11, 22, 0, 0, new TimeSpan(13, 0, 0));
            testAlwaysDay = SunStudy.CalculateSolarDimming(davisSummerDateAt10PM, latitude, longitude, 1); // using a fake altitude
            Debug.Assert(testAlwaysDay == 1);
        }

        [Test]
        public void SunStudyBrazilRioDeJaneiroCopacabana2015()
        {
            const float errorThresholdMinutes = 75f; // failure at 60 since there is an issue with locations around the equator
            ExecuteSolarCycleTest(errorThresholdMinutes, "SPA_BrazilRio_Copacobana_4Dec2015_Offset_minus2_Elev_0m.json");
        }

        [Test]
        public void SunStudyBrazilRioDeJaneiroCopacabana2019()
        {
            const float errorThresholdMinutes = 65f; // failure at 60, but compared to others, it is very close to pass
            ExecuteSolarCycleTest(errorThresholdMinutes, "SPA_BrazilRio_Copacobana_3Oct2019_Offset_minus3_Elev_0m.json");
        }

        [Test]
        public void SunStudyColombiaIpiales_LasLapas2015()
        {
            const float errorThresholdMinutes = 75f; // failure at 60 since there is an issue with locations around the equator
            ExecuteSolarCycleTest(errorThresholdMinutes, "SPA_Colombia_Ipiales_LasLajas_4Dec2015_Offset_minus5_Elev_0m.json");
        }

        [Test]
        public void SunStudyColombiaIpiales_LasLapas2019()
        {
            const float errorThresholdMinutes = 75f; // failure at 60 since there is an issue with locations around the equator
            ExecuteSolarCycleTest(errorThresholdMinutes, "SPA_Colombia_Ipiales_LasLajas_3Oct2019_Offset_minus5_Elev_0m.json");
        }

        [Test]
        public void SunStudyFranceNicePromDesAnglais1950()
        {
            ExecuteSolarCycleTest(k_ErrorThresholdMinutes, "SPA_FranceNice_PromAnglais_15July1950_Offset_2_Elev0m.json");
        }

        [Test]
        public void SunStudyFranceNicePromDesAnglais2019()
        {
            ExecuteSolarCycleTest(k_ErrorThresholdMinutes, "SPA_FranceNice_PromAnglais_3Oct2019_Offset_2_Elev0m.json");
        }

        [Test]
        public void SunStudyIndiaBangalore2019()
        {
            ExecuteSolarCycleTest(k_ErrorThresholdMinutes, "SPA_India_Bangalore_5Oct2019_Offset_5_5_Elev_0m.json");
        }

        [Test]
        public void SunStudyKievIndependenceSquare2019()
        {
            ExecuteSolarCycleTest(k_ErrorThresholdMinutes, "SPA_Kiev_Indp_Square_3Oct2019_Offset_3_Elev_0m.json");
        }

        [Test]
        public void SunStudyKievIndependenceSquareHoliday2019()
        {
            ExecuteSolarCycleTest(k_ErrorThresholdMinutes, "SPA_Kiev_Indp_Square_24Dec2019_Offset_2_DSTEnd_Elev_0m.json");
        }

        [Test]
        public void SunStudyLosAngelesDodgers2019()
        {
            ExecuteSolarCycleTest(k_ErrorThresholdMinutes, "SPA_LosAngeles_Dodgers_3Oct2019_Offset_minus7_Elev_0m.json");
        }

        [Test]
        public void SunStudyLosAngelesDodgers2045()
        {
            ExecuteSolarCycleTest(k_ErrorThresholdMinutes, "SPA_LosAngeles_Dodgers_1Feb2045_Offset_minus8_Elev0m.json");
        }

        [Test]
        public void SunStudyMadridSanSebastian2019()
        {
            const float errorThresholdMinutes = 65.1f; // failure at 60, but compared to others, it is very close to pass
            ExecuteSolarCycleTest(errorThresholdMinutes, "SPA_Madrid_SanSebastian_3Oct2019_Offset_2_Elev_0m.json");
        }

        [Test]
        public void SunStudyMexicoAirport2015()
        {
            const float errorThresholdMinutes = 75f; // failure at 60 since there is an issue with locations around the equator
            ExecuteSolarCycleTest(errorThresholdMinutes, "SPA_Mexico_Airport_4Dec2015_Offset_minus6_Elev_0m.json");
        }

        [Test]
        public void SunStudyMexicoAirport2019()
        {
            ExecuteSolarCycleTest(k_ErrorThresholdMinutes, "SPA_Mexico_Airport_3Oct2019_Offset_minus5_Elev_0m.json");
        }

        [Test]
        public void SunStudyMontrealUnity2019()
        {
            ExecuteSolarCycleTest(k_ErrorThresholdMinutes, "SPA_Montreal_23Sept2019_Offset_minus4_Elev_0m.json");
        }

        [Test]
        public void SunStudyNewZealandVictoriaUniversity2015()
        {
            ExecuteSolarCycleTest(k_ErrorThresholdMinutes, "SPA_NZWellington_UniversityCampus_4Dec2015_Elev_0m_Offset_13.json");
        }

        [Test]
        public void SunStudyNewZealandVictoriaUniversity2019()
        {
            const float errorThresholdMinutes = 65f; // failure at 60, but compared to others, it is very close to pass
            ExecuteSolarCycleTest(errorThresholdMinutes, "SPA_NZWellington_UniversityCampus_3Oct2019_Elev_0m_Offset_13.json");
        }

        [Test]
        public void SunStudyReykyavik2019()
        {
            ExecuteSolarCycleTest(k_ErrorThresholdMinutes, "SPA_Reykyavik_11May2019_Elev_0m.json");
        }

        [Test]
        public void SunStudyRussiaMoscowSheremetyevo2019()
        {
            ExecuteSolarCycleTest(k_ErrorThresholdMinutes, "SPA_Moscow_Sheremetyevo_3Oct2019_Offset_3_Elev_0m.json");
        }

        [Test]
        public void SunStudyRussiaMoscowSheremetyevo2050()
        {
            ExecuteSolarCycleTest(k_ErrorThresholdMinutes, "SPA_Moscow_Sheremetyevo_30May2050_Offset_3_Elev_0m.json");
        }

        [Test]
        public void SunStudyRussiaSiberia2019()
        {
            ExecuteSolarCycleTest(k_ErrorThresholdMinutes, "SPA_Siberia_3Oct2019_Offset_7_Elev_0m.json");
        }

        [Test]
        public void SunStudyTokyoUnityOfficeGinza1980()
        {
            ExecuteSolarCycleTest(k_ErrorThresholdMinutes, "SPA_Tokyo_UnityOffice_27April1980_Offset_9_Elev_0m.json");
        }

        [Test]
        public void SunStudyTokyoUnityOfficeGinza2019()
        {
            ExecuteSolarCycleTest(k_ErrorThresholdMinutes, "SPA_Tokyo_UnityOffice_3Oct2019_Offset_9_Elev_0m.json");
        }

        [Test]
        public void SunStudyTokyoUnityOfficeGinzaLeapYear2040()
        {
            ExecuteSolarCycleTest(k_ErrorThresholdMinutes, "SPA_Tokyo_UnityOffice_29Feb2040_Offset_9_Elev_0m.json");
        }
    }

    class SunStudyGeographicalAltitudeTests : SunStudyBaseTests
    {
        const float k_ErrorThreshold = 1f / 60f; // less than 1 minute of error is acceptable

        [Test]
        public void SunStudyColombiaIpiales_LasLapas_2015GeoTest()
        {
            ExecuteElevationTest(k_ErrorThreshold, "SPA_Colombia_Ipiales_LasLajas_4Dec2015_Offset_minus5_Elev_0m.json");
        }

        [Test]
        public void SunStudyColombiaIpiales_LasLapas_2019GeoTest()
        {
            ExecuteElevationTest(k_ErrorThreshold, "SPA_Colombia_Ipiales_LasLajas_3Oct2019_Offset_minus5_Elev_0m.json");
        }

        [Test]
        public void SunStudyFranceNicePromDesAnglais1950GeoAltitudeTest()
        {
            ExecuteElevationTest(k_ErrorThreshold, "SPA_FranceNice_PromAnglais_15July1950_Offset_2_Elev0m.json");
        }

        [Test]
        public void SunStudyFranceNicePromDesAnglais2019GeoAltitudeTest()
        {
            ExecuteElevationTest(k_ErrorThreshold, "SPA_FranceNice_PromAnglais_3Oct2019_Offset_2_Elev0m.json");
        }

        [Test]
        public void SunStudyIndiaBangalore2019GeoAltitudeTest()
        {
            ExecuteElevationTest(k_ErrorThreshold, "SPA_India_Bangalore_5Oct2019_Offset_5_5_Elev_0m.json");
        }

        [Test]
        public void SunStudyKievIndependenceSquare2019GeoAltitudeTest()
        {
            ExecuteElevationTest(k_ErrorThreshold, "SPA_Kiev_Indp_Square_3Oct2019_Offset_3_Elev_0m.json");
        }

        [Test]
        public void SunStudyKievIndependenceSquareHoliday2019GeoAltitudeTest()
        {
            ExecuteElevationTest(k_ErrorThreshold, "SPA_Kiev_Indp_Square_24Dec2019_Offset_2_DSTEnd_Elev_0m.json");
        }

        [Test]
        public void SunStudyLosAngeles2019GeoAltitudeTest()
        {
            ExecuteElevationTest(k_ErrorThreshold, "SPA_LosAngeles_Dodgers_3Oct2019_Offset_minus7_Elev_0m.json");
        }

        [Test]
        public void SunStudyLosAngeles2045GeoAltitudeTest()
        {
            ExecuteElevationTest(k_ErrorThreshold, "SPA_LosAngeles_Dodgers_1Feb2045_Offset_minus8_Elev0m.json");
        }

        [Test]
        public void SunStudyMadridSanSebastianGeoAltitudeTest()
        {
            ExecuteElevationTest(k_ErrorThreshold, "SPA_Madrid_SanSebastian_3Oct2019_Offset_2_Elev_0m.json");
        }

        [Test]
        public void SunStudyMexicoAirport2015GeoAltitudeTest()
        {
            ExecuteElevationTest(k_ErrorThreshold, "SPA_Mexico_Airport_4Dec2015_Offset_minus6_Elev_0m.json");
        }

        [Test]
        public void SunStudyMexicoAirport2019GeoAltitudeTest()
        {
            ExecuteElevationTest(k_ErrorThreshold, "SPA_Mexico_Airport_3Oct2019_Offset_minus5_Elev_0m.json");
        }

        [Test]
        public void SunStudyMontrealUnityGeoAltitudeTest()
        {
            ExecuteElevationTest(k_ErrorThreshold, "SPA_Montreal_23Sept2019_Offset_minus4_Elev_0m.json");
        }

        [Test]
        public void SunStudyNewZealandVictoriaUniversity2015GeoAltitudeTest()
        {
            ExecuteElevationTest(k_ErrorThreshold, "SPA_NZWellington_UniversityCampus_4Dec2015_Elev_0m_Offset_13.json");
        }

        [Test]
        public void SunStudyNewZealandVictoriaUniversity2019GeoAltitudeTest()
        {
            ExecuteElevationTest(k_ErrorThreshold, "SPA_NZWellington_UniversityCampus_3Oct2019_Elev_0m_Offset_13.json");
        }

        [Test]
        public void SunStudyRussiaMoscowSheremetyevoGeoAltitudeTest()
        {
            ExecuteElevationTest(k_ErrorThreshold, "SPA_Moscow_Sheremetyevo_3Oct2019_Offset_3_Elev_0m.json");
        }

        [Test]
        public void SunStudyRussiaMoscowSheremetyevo2050GeoAltitudeTest()
        {
            ExecuteElevationTest(k_ErrorThreshold, "SPA_Moscow_Sheremetyevo_30May2050_Offset_3_Elev_0m.json");
        }

        [Test]
        public void SunStudyRussiaSiberiaGeoAltitudeTest()
        {
            ExecuteElevationTest(k_ErrorThreshold, "SPA_Siberia_3Oct2019_Offset_7_Elev_0m.json");
        }

        [Test]
        public void SunStudyReykyavikGeoAltitudeTest()
        {
            ExecuteElevationTest(k_ErrorThreshold, "SPA_Reykyavik_11May2019_Elev_0m.json");
        }

        [Test]
        public void SunStudyRioDeJaneiro2015GeoAltitudeTest()
        {
            ExecuteElevationTest(k_ErrorThreshold, "SPA_BrazilRio_Copacobana_4Dec2015_Offset_minus2_Elev_0m.json");
        }

        [Test]
        public void SunStudyRioDeJaneiro2019GeoAltitudeTest()
        {
            ExecuteElevationTest(k_ErrorThreshold, "SPA_BrazilRio_Copacobana_3Oct2019_Offset_minus3_Elev_0m.json");
        }

        [Test]
        public void SunStudyTokyoUnityOffice1980GeoAltitudeTest()
        {
            ExecuteElevationTest(k_ErrorThreshold, "SPA_Tokyo_UnityOffice_27April1980_Offset_9_Elev_0m.json");
        }

        [Test]
        public void SunStudyTokyoUnityOffice2019GeoAltitudeTest()
        {
            ExecuteElevationTest(k_ErrorThreshold, "SPA_Tokyo_UnityOffice_3Oct2019_Offset_9_Elev_0m.json");
        }

        [Test]
        public void SunStudyTokyoUnityOfficeLeap2040GeoAltitudeTest()
        {
            ExecuteElevationTest(k_ErrorThreshold, "SPA_Tokyo_UnityOffice_29Feb2040_Offset_9_Elev_0m.json");
        }
    }

    class SunStudyGeographicalAzimuthTests : SunStudyBaseTests
    {
        const float k_ErrorThreshold = 1f / 60f; // less than 1 minute of error is acceptable

        [Test]
        public void SunStudyColombiaIpiales_LasLapas_2015GeoTest()
        {
            ExecuteAzimuthTest(k_ErrorThreshold, "SPA_Colombia_Ipiales_LasLajas_4Dec2015_Offset_minus5_Elev_0m.json");
        }

        [Test]
        public void SunStudyColombiaIpiales_LasLapas_2019GeoTest()
        {
            const float errorThreshold = 2f / 60f; ; // failure at 1/60 since there is an issue with locations around the equator
            ExecuteAzimuthTest(errorThreshold, "SPA_Colombia_Ipiales_LasLajas_3Oct2019_Offset_minus5_Elev_0m.json");
        }

        [Test]
        public void SunStudyFranceNicePromDesAnglais1950GeoTest()
        {
            ExecuteAzimuthTest(k_ErrorThreshold, "SPA_FranceNice_PromAnglais_15July1950_Offset_2_Elev0m.json");
        }

        [Test]
        public void SunStudyFranceNicePromDesAnglais2019GeoTest()
        {
            ExecuteAzimuthTest(k_ErrorThreshold, "SPA_FranceNice_PromAnglais_3Oct2019_Offset_2_Elev0m.json");
        }

        [Test]
        public void SunStudyIndiaBangalore2019GeoTest()
        {
            ExecuteAzimuthTest(k_ErrorThreshold, "SPA_India_Bangalore_5Oct2019_Offset_5_5_Elev_0m.json");
        }

        [Test]
        public void SunStudyKievIndependenceSquare2019GeoTest()
        {
            ExecuteAzimuthTest(k_ErrorThreshold, "SPA_Kiev_Indp_Square_3Oct2019_Offset_3_Elev_0m.json");
        }

        [Test]
        public void SunStudyKievIndependenceSquareHoliday2019GeoTest()
        {
            ExecuteAzimuthTest(k_ErrorThreshold, "SPA_Kiev_Indp_Square_24Dec2019_Offset_2_DSTEnd_Elev_0m.json");
        }

        [Test]
        public void SunStudyLosAngeles2019GeoTest()
        {
            ExecuteAzimuthTest(k_ErrorThreshold, "SPA_LosAngeles_Dodgers_3Oct2019_Offset_minus7_Elev_0m.json");
        }

        [Test]
        public void SunStudyLosAngeles2045GeoTest()
        {
            ExecuteAzimuthTest(k_ErrorThreshold, "SPA_LosAngeles_Dodgers_1Feb2045_Offset_minus8_Elev0m.json");
        }

        [Test]
        public void SunStudyMadridSanSebastianGeoTest()
        {
            ExecuteAzimuthTest(k_ErrorThreshold, "SPA_Madrid_SanSebastian_3Oct2019_Offset_2_Elev_0m.json");
        }

        [Test]
        public void SunStudyMexicoAirport2015GeoAltitudeTest()
        {
            const float errorThreshold = 2f / 60f; ; // failure at 1/60 since there is an issue with locations around the equator
            ExecuteAzimuthTest(errorThreshold, "SPA_Mexico_Airport_4Dec2015_Offset_minus6_Elev_0m.json");
        }

        [Test]
        public void SunStudyMexicoAirport2019GeoAltitudeTest()
        {
            ExecuteAzimuthTest(k_ErrorThreshold, "SPA_Mexico_Airport_3Oct2019_Offset_minus5_Elev_0m.json");
        }

        [Test]
        public void SunStudyMontrealUnityGeoTest()
        {
            ExecuteAzimuthTest(k_ErrorThreshold, "SPA_Montreal_23Sept2019_Offset_minus4_Elev_0m.json");
        }

        [Test]
        public void SunStudyNewZealandVictoriaUniversity2015GeoTest()
        {
            ExecuteAzimuthTest(k_ErrorThreshold, "SPA_NZWellington_UniversityCampus_4Dec2015_Elev_0m_Offset_13.json");
        }

        [Test]
        public void SunStudyNewZealandVictoriaUniversity2019GeoTest()
        {
            ExecuteAzimuthTest(k_ErrorThreshold, "SPA_NZWellington_UniversityCampus_3Oct2019_Elev_0m_Offset_13.json");
        }

        [Test]
        public void SunStudyRussiaMoscowSheremetyevoGeoTest()
        {
            ExecuteAzimuthTest(k_ErrorThreshold, "SPA_Moscow_Sheremetyevo_3Oct2019_Offset_3_Elev_0m.json");
        }

        [Test]
        public void SunStudyRussiaMoscowSheremetyevo2050GeoTest()
        {
            ExecuteAzimuthTest(k_ErrorThreshold, "SPA_Moscow_Sheremetyevo_30May2050_Offset_3_Elev_0m.json");
        }

        [Test]
        public void SunStudyRussiaSiberiaGeoTest()
        {
            ExecuteAzimuthTest(k_ErrorThreshold, "SPA_Siberia_3Oct2019_Offset_7_Elev_0m.json");
        }

        [Test]
        public void SunStudyReykyavikGeoTest()
        {
            ExecuteAzimuthTest(k_ErrorThreshold, "SPA_Reykyavik_11May2019_Elev_0m.json");
        }

        [Test]
        public void SunStudyRioDeJaneiro2015GeoTest()
        {
            const float errorThreshold = 2f / 60f; ; // failure at 1/60 since there is an issue with locations around the equator
            ExecuteAzimuthTest(errorThreshold, "SPA_BrazilRio_Copacobana_4Dec2015_Offset_minus2_Elev_0m.json");
        }

        [Test]
        public void SunStudyRioDeJaneiro2019GeoTest()
        {
            ExecuteAzimuthTest(k_ErrorThreshold, "SPA_BrazilRio_Copacobana_3Oct2019_Offset_minus3_Elev_0m.json");
        }

        [Test]
        public void SunStudyTokyoUnityOffice1980GeoTest()
        {
            ExecuteAzimuthTest(k_ErrorThreshold, "SPA_Tokyo_UnityOffice_27April1980_Offset_9_Elev_0m.json");
        }

        [Test]
        public void SunStudyTokyoUnityOffice2019GeoTest()
        {
            ExecuteAzimuthTest(k_ErrorThreshold, "SPA_Tokyo_UnityOffice_3Oct2019_Offset_9_Elev_0m.json");
        }

        [Test]
        public void SunStudyTokyoUnityOfficeLeap2040GeoTest()
        {
            ExecuteAzimuthTest(k_ErrorThreshold, "SPA_Tokyo_UnityOffice_29Feb2040_Offset_9_Elev_0m.json");
        }
    }
}
