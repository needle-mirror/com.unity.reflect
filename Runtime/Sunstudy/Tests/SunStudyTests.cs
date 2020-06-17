// Copyright (c) 2019 Unity Technologies. All rights reserved.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Unity.SunStudy;

namespace Tests
{
	namespace SunStudyTestsJson
	{
		[System.Serializable]
		public struct RootTest
		{
			public string name;
			public float latitude;
			public float longitude;
			public double utcOffset;
			public TestValue[] values;
		}

		[System.Serializable]
		public struct TestValue
		{
			public int dateY;
			public int dateM;
			public int dateD;

			public int timeH;
			public int timeM;
			public int timeS;
			public float azimuth;
			public float elevation;
			public float localSunriseHourFraction;  // fractional hour: https://midcdmz.nrel.gov/solpos/spa.html
			public float localSunsetHourFraction;   // fractional hour: https://midcdmz.nrel.gov/solpos/spa.html
		}
	}

	internal class SunStudyBaseTests
	{
		protected const float ERROR_RATE = 1f / 60f;       // Less than 1 minute of error is acceptable.

		private SunStudyTestsJson.RootTest LoadTestData(string in_testfilename)
		{
			string file_path = System.IO.Path.Combine(Application.dataPath, "Tests", "Data", in_testfilename);
			if(!System.IO.File.Exists(file_path))
			{
				string[] guids = AssetDatabase.FindAssets(System.IO.Path.GetFileNameWithoutExtension(in_testfilename));
				if (guids.Length > 0)
				{
					foreach (string guid in guids)
					{
						string path = AssetDatabase.GUIDToAssetPath(guid);
						if (System.IO.File.Exists(path))
						{
							file_path = path;
							break;
						}
					}
				}
			}

			string json = System.IO.File.ReadAllText(file_path);
			SunStudyTestsJson.RootTest testData = JsonUtility.FromJson<SunStudyTestsJson.RootTest>(json);
			return testData;
		}

		//protected void ExecuteSolarCycleTest(float ERROR_RATE_MINUTES, string testJsonFileName)
		//{
		//	SunStudyTestsJson.RootTest testData = LoadTestData(testJsonFileName);
		//	var geoCoordinates = Tuple.Create<float, float>(testData.latitude, testData.longitude);
		//	int utcOffsetHour = (int)testData.utcOffset;
		//	int utcOffsetMinute = (int)((testData.utcOffset - utcOffsetHour) * 60f);
		//	TimeSpan offset = new TimeSpan(utcOffsetHour, utcOffsetMinute, 0);

		//	foreach(SunStudyTestsJson.TestValue value in testData.values)
		//	{
		//		if (value.timeH != 0)
		//			continue;       // For Solar tests, we can test only one hour entry per day since we are testing sunrise and sunset times.

		//		double sunriseCheck = value.localSunriseHourFraction;
		//		int sunriseCheckHour = (int)sunriseCheck;
		//		double sunriseCheckMinutes = sunriseCheckHour * 60.0 + (sunriseCheck - sunriseCheckHour);

		//		double sunsetCheck = value.localSunsetHourFraction;
		//		int sunsetCheckHour = (int)sunsetCheck;
		//		double sunsetCheckMinutes = sunsetCheckHour * 60.0 + (sunsetCheck - sunsetCheckHour);

		//		DateTimeOffset solarCheckDate = new DateTimeOffset(value.dateY, value.dateM, value.dateD, 0, 0, 0, offset);
		//		(double julianSunrise, double julianSunset, double transit) = SunStudy.GetJulianDatesForSunriseAndSunset(solarCheckDate, geoCoordinates.Item1, geoCoordinates.Item2);
		//		DateTimeOffset sunriseDateTime = SunStudy.MakeDateTimeOffsetWithJDateTimeFraction(julianSunrise, solarCheckDate);
		//		DateTimeOffset sunsetDateTime = SunStudy.MakeDateTimeOffsetWithJDateTimeFraction(julianSunset, solarCheckDate);

		//		double sunriseTestMinutes = sunriseDateTime.Hour * 60.0 + sunriseDateTime.Minute;
		//		double sunsetTestMinutes = sunsetDateTime.Hour * 60.0 + sunsetDateTime.Minute;
		//		Assert.AreEqual(sunriseCheckMinutes, sunriseTestMinutes, ERROR_RATE_MINUTES, $"Sunrise Time not equal with {ERROR_RATE_MINUTES} minutes accuracy: JD Sunrise: {julianSunrise}, SPA Check JD value: {sunriseCheck}");
		//		Assert.AreEqual(sunsetCheckMinutes, sunsetTestMinutes, ERROR_RATE_MINUTES, $"Sunset Time not equal with {ERROR_RATE_MINUTES} minutes accuracy: JD Sunrise: {julianSunrise}, SPA Check JD value: {sunsetCheck}");
		//	}
		//}

		protected void ExecuteTest(float ERROR_RATE, string testJsonFileName, bool testAzimuth = true)
		{
			SunStudyTestsJson.RootTest testData = LoadTestData(testJsonFileName);
			var geoCoordinates = Tuple.Create<float, float>(testData.latitude, testData.longitude);

			int utcOffsetHour = (int)testData.utcOffset;
			int utcOffsetMinute = (int)((testData.utcOffset - utcOffsetHour) * 60f);
			TimeSpan offset = new TimeSpan(utcOffsetHour, utcOffsetMinute, 0);

			List<DateTimeOffset> datetimeInputs = new List<DateTimeOffset>();
			List<Tuple<float, float>> outputResults = new List<Tuple<float, float>>();
			foreach (SunStudyTestsJson.TestValue test in testData.values)
			{
				datetimeInputs.Add(new DateTimeOffset(test.dateY, test.dateM, test.dateD, test.timeH, test.timeM, test.timeS, offset));
				outputResults.Add(Tuple.Create<float, float>(test.azimuth, test.elevation));
			}

			GeoPositionExecuteTests(geoCoordinates, datetimeInputs, outputResults, ERROR_RATE, testAzimuth);
		}

		private void GeoPositionExecuteTests(
			Tuple<float, float> geoCoordinates,         // latitude, longitude
			List<DateTimeOffset> dateTimeInputList,
			List<Tuple<float, float>> expectedOutputsList,   // azimuth, elevation (uncorrected)
			float ERROR_RATE,
			bool testAzimuth)
		{
			Assert.IsTrue(dateTimeInputList.Count == expectedOutputsList.Count);
			for (int testCaseIndex = 0; testCaseIndex < dateTimeInputList.Count; ++testCaseIndex)
			{
				DateTimeOffset dt = dateTimeInputList[testCaseIndex];
				var expectedResult = expectedOutputsList[testCaseIndex];
				(float resultAzimuth, float resultAltitude) = SunStudy.CalculateSunPosition(dt, geoCoordinates.Item1, geoCoordinates.Item2);

				if (testAzimuth)
				{
					Assert.AreEqual(expectedResult.Item1, resultAzimuth, ERROR_RATE, $"Azimuth not equal with {ERROR_RATE} acc.: TestH: {dt.Hour}");
				}
				else
				{
					Assert.AreEqual(expectedResult.Item2, resultAltitude, ERROR_RATE, $"Elevation not equal with {ERROR_RATE} acc.: TestH: {dt.Hour}");
				}
			}
		}
	}

	class SunStudySunriseSunsetTests : SunStudyBaseTests
	{
		//float ERROR_RATE_MINUTES = 60f;

		[Test]
		public void SunStudyAlertAlwaysDayTimeDuringSummer2019()
		{
			double latitude = 82.496299;
			double longitude = -62.359018;
			DateTimeOffset alertSummerDateDayAt2AM = new DateTimeOffset(2019, 06, 21, 2, 0, 0, new TimeSpan(-4, 0, 0));
			float testAlwaysDay = SunStudy.CalculateSolarDimming(alertSummerDateDayAt2AM, latitude, longitude, 16 /* fake altitude */);
			Debug.Assert(testAlwaysDay == 1);

			DateTimeOffset alertSummerDateDayAt10PM = new DateTimeOffset(2019, 06, 21, 22, 0, 0, new TimeSpan(-4, 0, 0));
			testAlwaysDay = SunStudy.CalculateSolarDimming(alertSummerDateDayAt10PM, latitude, longitude, 16 /* fake altitude */);
			Debug.Assert(testAlwaysDay == 1);
		}

		[Test]
		public void SunStudyAlertAlwaysNightTimeDuringWinter2019()
		{
			double latitude = 82.496299;
			double longitude = -62.359018;
			DateTimeOffset alertWinterDateDayAtNoon = new DateTimeOffset(2019, 12, 3, 0, 01, 0, new TimeSpan(-5, 0, 0));
			float testAlwaysNightRatio = SunStudy.CalculateSolarDimming(alertWinterDateDayAtNoon, latitude, longitude, 16 /* fake altitude */);
			Debug.Assert(testAlwaysNightRatio == 0);
		}

		[Test]
		public void SunStudyAlertAlwaysNightTimeDuringWinter2020()
		{
			double latitude = 82.496299;
			double longitude = -62.359018;
			DateTimeOffset alertWinterDateDayAtNoon = new DateTimeOffset(2020, 1, 2, 12, 0, 0, new TimeSpan(-5, 0, 0));
			float testAlwaysNightRatio = SunStudy.CalculateSolarDimming(alertWinterDateDayAtNoon, latitude, longitude, 16 /* fake altitude */);
			Debug.Assert(testAlwaysNightRatio == 0);
		}

		[Test]
		public void SunStudyMurmanskAlwaysDayTimeDuringSummer2019()
		{
			double latitude = 68.971557;
			double longitude = 33.070630;
			DateTimeOffset alertSummerDateDayAt2AM = new DateTimeOffset(2019, 06, 21, 2, 0, 0, new TimeSpan(-4, 0, 0));
			float testAlwaysDay = SunStudy.CalculateSolarDimming(alertSummerDateDayAt2AM, latitude, longitude, 16 /* fake altitude */);
			Debug.Assert(testAlwaysDay == 1);

			DateTimeOffset alertSummerDateDayAt10PM = new DateTimeOffset(2019, 06, 21, 22, 0, 0, new TimeSpan(-4, 0, 0));
			testAlwaysDay = SunStudy.CalculateSolarDimming(alertSummerDateDayAt10PM, latitude, longitude, 16 /* fake altitude */);
			Debug.Assert(testAlwaysDay == 1);
		}

		[Test]
		public void SunStudyMurmanskAlwaysNightTimeDuringWinter2019()
		{
			double latitude = 68.971557;
			double longitude = 33.070630;
			DateTimeOffset alertWinterDateDayAtNoon = new DateTimeOffset(2019, 12, 3, 0, 01, 0, new TimeSpan(-5, 0, 0));
			float testAlwaysNightRatio = SunStudy.CalculateSolarDimming(alertWinterDateDayAtNoon, latitude, longitude, 16 /* fake altitude */);
			Debug.Assert(testAlwaysNightRatio == 0);
		}

		[Test]
		public void SunStudyMurmanskAlwaysNightTimeDuringWinter2020()
		{
			double latitude = 68.971557;
			double longitude = 33.070630;
			DateTimeOffset alertWinterDateDayAtNoon = new DateTimeOffset(2020, 1, 2, 12, 0, 0, new TimeSpan(-5, 0, 0));
			float testAlwaysNightRatio = SunStudy.CalculateSolarDimming(alertWinterDateDayAtNoon, latitude, longitude, 16 /* fake altitude */);
			Debug.Assert(testAlwaysNightRatio == 0);
		}

		[Test]
		public void SunStudyAntarticaDavisAlwaysNightTimeDuringWinter2019()
		{
			double latitude = -68.682687;
			double longitude = 79.295626;
			DateTimeOffset davisWinterDateNightAtNoon = new DateTimeOffset(2019, 06, 21, 12, 0, 0, new TimeSpan(-4, 0, 0));
			float testAlwayNightRatio = SunStudy.CalculateSolarDimming(davisWinterDateNightAtNoon, latitude, longitude, 1 /* fake altitude */);
			Debug.Assert(testAlwayNightRatio == 0);
		}

		[Test]
		public void SunStudyAntarticaDavisAlwaysDayTimeDuringSummer2019()
		{
			double latitude = -68.682687;
			double longitude = 79.295626;
			DateTimeOffset davisSummerDateAt2AM = new DateTimeOffset(2019, 12, 15, 2, 0, 0, new TimeSpan(13, 0, 0));
			float testAlwaysDay = SunStudy.CalculateSolarDimming(davisSummerDateAt2AM, latitude, longitude, 1 /* fake altitude */);
			Debug.Assert(testAlwaysDay == 1);

			DateTimeOffset davisSummerDateAt10PM = new DateTimeOffset(2019, 12, 27, 22, 0, 0, new TimeSpan(13, 0, 0));
			testAlwaysDay = SunStudy.CalculateSolarDimming(davisSummerDateAt10PM, latitude, longitude, 1 /* fake altitude */);
			Debug.Assert(testAlwaysDay == 1);
		}

		[Test]
		public void SunStudyAntarticaDavisAlwaysDayTimeDuringSummer2020()
		{
			double latitude = -68.682687;
			double longitude = 79.295626;
			DateTimeOffset davisSummerDateAt2AM = new DateTimeOffset(2020, 01, 11, 2, 0, 0, new TimeSpan(13, 0, 0));
			float testAlwaysDay = SunStudy.CalculateSolarDimming(davisSummerDateAt2AM, latitude, longitude, 1 /* fake altitude */);
			Debug.Assert(testAlwaysDay == 1);

			DateTimeOffset davisSummerDateAt10PM = new DateTimeOffset(2020, 01, 11, 22, 0, 0, new TimeSpan(13, 0, 0));
			testAlwaysDay = SunStudy.CalculateSolarDimming(davisSummerDateAt10PM, latitude, longitude, 1 /* fake altitude */);
			Debug.Assert(testAlwaysDay == 1);
		}

		//[Test]
		//public void SunStudyBrazilRioDeJaneiroCopacabana2015()
		//{
		//	ExecuteSolarCycleTest(75f /* Failure at error rate of 60f: issue with locations around the equator */, "SPA_BrazilRio_Copacobana_4Dec2015_Offset_minus2_Elev_0m.json");
		//}

		//[Test]
		//public void SunStudyBrazilRioDeJaneiroCopacabana2019()
		//{
		//	ExecuteSolarCycleTest(65f /* Failure at error rate of 60f: compared to others, it is very close to pass */, "SPA_BrazilRio_Copacobana_3Oct2019_Offset_minus3_Elev_0m.json");
		//}

		//[Test]
		//public void SunStudyColombiaIpiales_LasLapas2015()
		//{
		//	ExecuteSolarCycleTest(75f /* Failure at error rate of 60f: issue with locations around the equator */, "SPA_Colombia_Ipiales_LasLajas_4Dec2015_Offset_minus5_Elev_0m.json");
		//}

		//[Test]
		//public void SunStudyColombiaIpiales_LasLapas2019()
		//{
		//	ExecuteSolarCycleTest(75f /* Failure at error rate of 60f: issue with locations around the equator */, "SPA_Colombia_Ipiales_LasLajas_3Oct2019_Offset_minus5_Elev_0m.json");
		//}

		//[Test]
		//public void SunStudyFranceNicePromDesAnglais1950()
		//{
		//	ExecuteSolarCycleTest(ERROR_RATE_MINUTES, "SPA_FranceNice_PromAnglais_15July1950_Offset_2_Elev0m.json");
		//}

		//[Test]
		//public void SunStudyFranceNicePromDesAnglais2019()
		//{
		//	ExecuteSolarCycleTest(ERROR_RATE_MINUTES, "SPA_FranceNice_PromAnglais_3Oct2019_Offset_2_Elev0m.json");
		//}

		//[Test]
		//public void SunStudyIndiaBangalore2019()
		//{
		//	ExecuteSolarCycleTest(ERROR_RATE_MINUTES, "SPA_India_Bangalore_5Oct2019_Offset_5_5_Elev_0m.json");
		//}

		//[Test]
		//public void SunStudyKievIndependenceSquare2019()
		//{
		//	ExecuteSolarCycleTest(ERROR_RATE_MINUTES, "SPA_Kiev_Indp_Square_3Oct2019_Offset_3_Elev_0m.json");
		//}

		//[Test]
		//public void SunStudyKievIndependenceSquareHoliday2019()
		//{
		//	ExecuteSolarCycleTest(ERROR_RATE_MINUTES, "SPA_Kiev_Indp_Square_24Dec2019_Offset_2_DSTEnd_Elev_0m.json");
		//}

		//[Test]
		//public void SunStudyLosAngelesDodgers2019()
		//{
		//	ExecuteSolarCycleTest(ERROR_RATE_MINUTES, "SPA_LosAngeles_Dodgers_3Oct2019_Offset_minus7_Elev_0m.json");
		//}

		//[Test]
		//public void SunStudyLosAngelesDodgers2045()
		//{
		//	ExecuteSolarCycleTest(ERROR_RATE_MINUTES, "SPA_LosAngeles_Dodgers_1Feb2045_Offset_minus8_Elev0m.json");
		//}

		//[Test]
		//public void SunStudyMadridSanSebastian2019()
		//{
		//	ExecuteSolarCycleTest(65.1f /* Failure at error rate of 60f: compared to others, it is very close to pass */, "SPA_Madrid_SanSebastian_3Oct2019_Offset_2_Elev_0m.json");
		//}

		//[Test]
		//public void SunStudyMexicoAirport2015()
		//{
		//	ExecuteSolarCycleTest(75f /* Failure at error rate of 60f: issue with locations around the equator */, "SPA_Mexico_Airport_4Dec2015_Offset_minus6_Elev_0m.json");
		//}

		//[Test]
		//public void SunStudyMexicoAirport2019()
		//{
		//	ExecuteSolarCycleTest(ERROR_RATE_MINUTES, "SPA_Mexico_Airport_3Oct2019_Offset_minus5_Elev_0m.json");
		//}

		//[Test]
		//public void SunStudyMontrealUnity2019()
		//{
		//	ExecuteSolarCycleTest(ERROR_RATE_MINUTES, "SPA_Montreal_23Sept2019_Offset_minus4_Elev_0m.json");
		//}

		//[Test]
		//public void SunStudyNewZealandVictoriaUniversity2015()
		//{
		//	ExecuteSolarCycleTest(ERROR_RATE_MINUTES, "SPA_NZWellington_UniversityCampus_4Dec2015_Elev_0m_Offset_13.json");
		//}

		//[Test]
		//public void SunStudyNewZealandVictoriaUniversity2019()
		//{
		//	ExecuteSolarCycleTest(65f /* Failure at error rate of 60f: compared to others, it is very close to pass */, "SPA_NZWellington_UniversityCampus_3Oct2019_Elev_0m_Offset_13.json");
		//}

		//[Test]
		//public void SunStudyReykyavik2019()
		//{
		//	ExecuteSolarCycleTest(ERROR_RATE_MINUTES, "SPA_Reykyavik_11May2019_Elev_0m.json");
		//}

		//[Test]
		//public void SunStudyRussiaMoscowSheremetyevo2019()
		//{
		//	ExecuteSolarCycleTest(ERROR_RATE_MINUTES, "SPA_Moscow_Sheremetyevo_3Oct2019_Offset_3_Elev_0m.json");
		//}

		//[Test]
		//public void SunStudyRussiaMoscowSheremetyevo2050()
		//{
		//	ExecuteSolarCycleTest(ERROR_RATE_MINUTES, "SPA_Moscow_Sheremetyevo_30May2050_Offset_3_Elev_0m.json");
		//}

		//[Test]
		//public void SunStudyRussiaSiberia2019()
		//{
		//	ExecuteSolarCycleTest(ERROR_RATE_MINUTES, "SPA_Siberia_3Oct2019_Offset_7_Elev_0m.json");
		//}


		//[Test]
		//public void SunStudyTokyoUnityOfficeGinza1980()
		//{
		//	ExecuteSolarCycleTest(ERROR_RATE_MINUTES, "SPA_Tokyo_UnityOffice_27April1980_Offset_9_Elev_0m.json");
		//}

		//[Test]
		//public void SunStudyTokyoUnityOfficeGinza2019()
		//{
		//	ExecuteSolarCycleTest(ERROR_RATE_MINUTES, "SPA_Tokyo_UnityOffice_3Oct2019_Offset_9_Elev_0m.json");
		//}

		//[Test]
		//public void SunStudyTokyoUnityOfficeGinzaLeapYear2040()
		//{
		//	ExecuteSolarCycleTest(ERROR_RATE_MINUTES, "SPA_Tokyo_UnityOffice_29Feb2040_Offset_9_Elev_0m.json");
		//}
	}
	class SunStudyGeoAltitudeTests : SunStudyBaseTests
	{
		[Test]
		public void SunStudyColombiaIpiales_LasLapas_2015GeoTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_Colombia_Ipiales_LasLajas_4Dec2015_Offset_minus5_Elev_0m.json", false);
		}
		[Test]
		public void SunStudyColombiaIpiales_LasLapas_2019GeoTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_Colombia_Ipiales_LasLajas_3Oct2019_Offset_minus5_Elev_0m.json", false);
		}

		[Test]
		public void SunStudyFranceNicePromDesAnglais1950GeoAltitudeTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_FranceNice_PromAnglais_15July1950_Offset_2_Elev0m.json", false);
		}

		[Test]
		public void SunStudyFranceNicePromDesAnglais2019GeoAltitudeTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_FranceNice_PromAnglais_3Oct2019_Offset_2_Elev0m.json", false);
		}

		[Test]
		public void SunStudyIndiaBangalore2019GeoAltitudeTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_India_Bangalore_5Oct2019_Offset_5_5_Elev_0m.json", false);
		}

		[Test]
		public void SunStudyKievIndependenceSquare2019GeoAltitudeTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_Kiev_Indp_Square_3Oct2019_Offset_3_Elev_0m.json", false);
		}

		[Test]
		public void SunStudyKievIndependenceSquareHoliday2019GeoAltitudeTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_Kiev_Indp_Square_24Dec2019_Offset_2_DSTEnd_Elev_0m.json", false);
		}

		[Test]
		public void SunStudyLosAngeles2019GeoAltitudeTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_LosAngeles_Dodgers_3Oct2019_Offset_minus7_Elev_0m.json", false);
		}

		[Test]
		public void SunStudyLosAngeles2045GeoAltitudeTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_LosAngeles_Dodgers_1Feb2045_Offset_minus8_Elev0m.json", false);
		}

		[Test]
		public void SunStudyMadridSanSebastianGeoAltitudeTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_Madrid_SanSebastian_3Oct2019_Offset_2_Elev_0m.json", false);
		}

		[Test]
		public void SunStudyMexicoAirport2015GeoAltitudeTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_Mexico_Airport_4Dec2015_Offset_minus6_Elev_0m.json", false);
		}

		[Test]
		public void SunStudyMexicoAirport2019GeoAltitudeTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_Mexico_Airport_3Oct2019_Offset_minus5_Elev_0m.json", false);
		}

		[Test]
		public void SunStudyMontrealUnityGeoAltitudeTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_Montreal_23Sept2019_Offset_minus4_Elev_0m.json", false);
		}

		[Test]
		public void SunStudyNewZealandVictoriaUniversity2015GeoAltitudeTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_NZWellington_UniversityCampus_4Dec2015_Elev_0m_Offset_13.json", false);
		}

		[Test]
		public void SunStudyNewZealandVictoriaUniversity2019GeoAltitudeTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_NZWellington_UniversityCampus_3Oct2019_Elev_0m_Offset_13.json", false);
		}

		[Test]
		public void SunStudyRussiaMoscowSheremetyevoGeoAltitudeTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_Moscow_Sheremetyevo_3Oct2019_Offset_3_Elev_0m.json", false);
		}

		[Test]
		public void SunStudyRussiaMoscowSheremetyevo2050GeoAltitudeTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_Moscow_Sheremetyevo_30May2050_Offset_3_Elev_0m.json", false);
		}

		[Test]
		public void SunStudyRussiaSiberiaGeoAltitudeTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_Siberia_3Oct2019_Offset_7_Elev_0m.json", false);
		}

		[Test]
		public void SunStudyReykyavikGeoAltitudeTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_Reykyavik_11May2019_Elev_0m.json", false);
		}

		[Test]
		public void SunStudyRioDeJaneiro2015GeoAltitudeTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_BrazilRio_Copacobana_4Dec2015_Offset_minus2_Elev_0m.json", false);
		}

		[Test]
		public void SunStudyRioDeJaneiro2019GeoAltitudeTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_BrazilRio_Copacobana_3Oct2019_Offset_minus3_Elev_0m.json", false);
		}

		[Test]
		public void SunStudyTokyoUnityOffice1980GeoAltitudeTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_Tokyo_UnityOffice_27April1980_Offset_9_Elev_0m.json", false);
		}

		[Test]
		public void SunStudyTokyoUnityOffice2019GeoAltitudeTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_Tokyo_UnityOffice_3Oct2019_Offset_9_Elev_0m.json", false);
		}

		[Test]
		public void SunStudyTokyoUnityOfficeLeap2040GeoAltitudeTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_Tokyo_UnityOffice_29Feb2040_Offset_9_Elev_0m.json", false);
		}
	}

	class SunStudyGeoAzimuthTests : SunStudyBaseTests
	{
		[Test]
		public void SunStudyColombiaIpiales_LasLapas_2015GeoTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_Colombia_Ipiales_LasLajas_4Dec2015_Offset_minus5_Elev_0m.json");
		}
		[Test]
		public void SunStudyColombiaIpiales_LasLapas_2019GeoTest()
		{
			ExecuteTest(2f / 60f /*Failure at normal error_rate = 1f/60f: issue with locations around the equator*/, "SPA_Colombia_Ipiales_LasLajas_3Oct2019_Offset_minus5_Elev_0m.json");
		}

		[Test]
		public void SunStudyFranceNicePromDesAnglais1950GeoTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_FranceNice_PromAnglais_15July1950_Offset_2_Elev0m.json");
		}

		[Test]
		public void SunStudyFranceNicePromDesAnglais2019GeoTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_FranceNice_PromAnglais_3Oct2019_Offset_2_Elev0m.json");
		}

		[Test]
		public void SunStudyIndiaBangalore2019GeoTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_India_Bangalore_5Oct2019_Offset_5_5_Elev_0m.json");
		}

		[Test]
		public void SunStudyKievIndependenceSquare2019GeoTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_Kiev_Indp_Square_3Oct2019_Offset_3_Elev_0m.json");
		}

		[Test]
		public void SunStudyKievIndependenceSquareHoliday2019GeoTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_Kiev_Indp_Square_24Dec2019_Offset_2_DSTEnd_Elev_0m.json");
		}

		[Test]
		public void SunStudyLosAngeles2019GeoTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_LosAngeles_Dodgers_3Oct2019_Offset_minus7_Elev_0m.json");
		}

		[Test]
		public void SunStudyLosAngeles2045GeoTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_LosAngeles_Dodgers_1Feb2045_Offset_minus8_Elev0m.json");
		}

		[Test]
		public void SunStudyMadridSanSebastianGeoTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_Madrid_SanSebastian_3Oct2019_Offset_2_Elev_0m.json");
		}

		[Test]
		public void SunStudyMexicoAirport2015GeoAltitudeTest()
		{
			ExecuteTest(2f / 60f /*Failure at normal error_rate = 1f/60f: issue with locations around the equator */, "SPA_Mexico_Airport_4Dec2015_Offset_minus6_Elev_0m.json");
		}

		[Test]
		public void SunStudyMexicoAirport2019GeoAltitudeTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_Mexico_Airport_3Oct2019_Offset_minus5_Elev_0m.json");
		}

		[Test]
		public void SunStudyMontrealUnityGeoTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_Montreal_23Sept2019_Offset_minus4_Elev_0m.json");
		}

		[Test]
		public void SunStudyNewZealandVictoriaUniversity2015GeoTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_NZWellington_UniversityCampus_4Dec2015_Elev_0m_Offset_13.json");
		}

		[Test]
		public void SunStudyNewZealandVictoriaUniversity2019GeoTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_NZWellington_UniversityCampus_3Oct2019_Elev_0m_Offset_13.json");
		}

		[Test]
		public void SunStudyRussiaMoscowSheremetyevoGeoTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_Moscow_Sheremetyevo_3Oct2019_Offset_3_Elev_0m.json");
		}

		[Test]
		public void SunStudyRussiaMoscowSheremetyevo2050GeoTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_Moscow_Sheremetyevo_30May2050_Offset_3_Elev_0m.json");
		}

		[Test]
		public void SunStudyRussiaSiberiaGeoTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_Siberia_3Oct2019_Offset_7_Elev_0m.json");
		}

		[Test]
		public void SunStudyReykyavikGeoTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_Reykyavik_11May2019_Elev_0m.json");
		}

		[Test]
		public void SunStudyRioDeJaneiro2015GeoTest()
		{
			ExecuteTest(2f / 60f /*Failure at normal error_rate = 1f/60f: issue with locations around the equator */, "SPA_BrazilRio_Copacobana_4Dec2015_Offset_minus2_Elev_0m.json");
		}

		[Test]
		public void SunStudyRioDeJaneiro2019GeoTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_BrazilRio_Copacobana_3Oct2019_Offset_minus3_Elev_0m.json");
		}

		[Test]
		public void SunStudyTokyoUnityOffice1980GeoTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_Tokyo_UnityOffice_27April1980_Offset_9_Elev_0m.json");
		}

		[Test]
		public void SunStudyTokyoUnityOffice2019GeoTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_Tokyo_UnityOffice_3Oct2019_Offset_9_Elev_0m.json");
		}

		[Test]
		public void SunStudyTokyoUnityOfficeLeap2040GeoTest()
		{
			ExecuteTest(ERROR_RATE, "SPA_Tokyo_UnityOffice_29Feb2040_Offset_9_Elev_0m.json");
		}
	}
}
