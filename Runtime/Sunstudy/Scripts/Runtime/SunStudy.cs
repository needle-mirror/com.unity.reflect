#if UNITY_2019_2_OR_NEWER

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Unity.SunStudy
{
    public enum SunPlacementMode
    {
        Geographical,
        Static,
    }

    public enum SunCoordinateMode
    {
        Address,
        DegreesMinutesSeconds,
        LatitudeLongitude,
    }

    public enum SunCoordinateNS
    {
        North,
        South,
    }

    public enum SunCoordinateEW
    {
        East,
        West,
    }

    [AddComponentMenu("AEC/Sun Study")]
    [ExecuteInEditMode]
    public class SunStudy : MonoBehaviour
    {
        public SunPlacementMode PlacementMode = SunPlacementMode.Static;

        // Angle along the horizontal XZ plane in range [0..360[ degrees,
        // with 0 degrees corresponding to True North, increasing in a clockwise fashion.
        public float Azimuth;

        // Angle up from the horizontal XZ plane in range [-90..+90] degrees,
        // from -90 degrees at nadir to +90 degrees at zenith, with 0 degrees exactly on the horizon.
        public float Altitude;

        // Direction of True North in the horizontal XZ plane.
        public Vector2 NorthDirection = new Vector2(0f, 1f);

        // Angle of True North in the horizontal XZ plane in range [0..360[ degrees,
        // with 0 degrees corresponding to the Z axis, increasing in a clockwise fashion.
        public float NorthAngle
        {
            get { return GetNorthAngle(NorthDirection); }
            set { NorthDirection = NorthDirection.magnitude * GetNorthDirection(value); }
        }

        public SunCoordinateMode CoordinateMode = SunCoordinateMode.LatitudeLongitude;

        public float CoordLatitude;
        public float CoordLongitude;

        [Delayed]
        public string CoordAddress;
        public string GeocodingMessage;

        public SunCoordinateNS CoordNS = SunCoordinateNS.North;
        public SunCoordinateEW CoordEW = SunCoordinateEW.West;

        public int CoordNSDeg;
        public int CoordNSMin;
        public int CoordNSSec;

        public int CoordEWDeg;
        public int CoordEWMin;
        public int CoordEWSec;

        public int Year  = DateTime.Now.Year;
        public int Month = DateTime.Now.Month;
        public int Day   = DateTime.Now.Day;

        public int DayOfYear
        {
            get { return GetDayOfYear(Year, Month, Day); }
            set { (Month, Day) = SetDayOfYear(Year, value); }
        }

        public int Hour = 12;
        public int Minute;

        public int MinuteOfDay
        {
            get { return GetMinuteOfDay(Hour, Minute); }
            set { (Hour, Minute) = SetMinuteOfDay(value); }
        }

        public float UtcOffset = (float)TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalHours;

        public Light SunLight;
        public float Intensity;
        public bool  ApplyDimming;
        public float Dimming;

        public const string k_PlayerPrefGeocodingApiKey = "SunStudy_GeocodingAPIKey";

        UnityWebRequestAsyncOperation m_WaitingOnWebOperation          = null;
        string                        m_CurrentGeocodingAddressRequest = null; // used to cancel the ongoing request when the input address changes

        const double k_JulianDateYear2000 = 2451545.0;

        public static float GetNorthAngle(Vector2 northDirection)
        {
            northDirection.Normalize();
            return (Mathf.Atan2(northDirection.x, northDirection.y) * Mathf.Rad2Deg + 360f) % 360f; // remap in range [0..360[ degrees
        }

        public static Vector2 GetNorthDirection(float northAngle)
        {
            northAngle = (northAngle % 360f + 360f) % 360f; // remap in range [0..360[ degrees
            return new Vector2(Mathf.Sin(northAngle * Mathf.Deg2Rad), Mathf.Cos(northAngle * Mathf.Deg2Rad));
        }

        public static int GetDayOfYear(int year, int month, int day)
        {
            day = Mathf.Clamp(day, 1, DateTime.DaysInMonth(year, month));
            return new DateTime(year, month, day).DayOfYear;
        }

        public static (int month, int day) SetDayOfYear(int year, int dayOfYear)
        {
            dayOfYear = Mathf.Clamp(dayOfYear, 1, GetDayOfYear(year, 12, 31));
            var date = new DateTime(year, 1, 1).AddDays(dayOfYear - 1);
            return (date.Month, date.Day);
        }

        public static int GetMinuteOfDay(int hour, int minute)
        {
            return hour * 60 + minute;
        }

        public static (int hour, int minute) SetMinuteOfDay(int minuteOfDay)
        {
            minuteOfDay = Mathf.Clamp(minuteOfDay, 0, 1439);
            return (minuteOfDay / 60, minuteOfDay % 60);
        }

        void Awake()
        {
            SunLight = gameObject.GetComponent<Light>();

            if (SunLight != null)
            {
                Intensity = SunLight.intensity;
            }
        }

        void Update()
        {
            ComputeAzimuthAltitude();

            Azimuth  = (Azimuth % 360f + 360f) % 360f;    // remap in range [0..360[ degrees
            Altitude = Mathf.Clamp(Altitude, -90f, +90f); // clamp between -90 and +90 degrees

            // Set the sun rotation.
            transform.eulerAngles = new Vector3(Altitude, GetNorthAngle(NorthDirection) + Azimuth + 180f, 0f);

            // Set the sun intensity when required.
            if (SunLight != null)
            {
                SunLight.intensity = (ApplyDimming ? Dimming : 1f) * Intensity;
            }
        }

        // Called when the component is loaded or a value is changed in the inspector.
        void OnValidate()
        {
            CoordLatitude  = Mathf.Clamp(CoordLatitude, -90f, +90f);
            CoordLongitude = Mathf.Clamp(CoordLongitude, -180f, +180f);

            CoordNSDeg = Mathf.Clamp(CoordNSDeg, 0, 90);
            CoordNSMin = Mathf.Clamp(CoordNSMin, 0, (CoordNSDeg < 90) ? 59 : 0);
            CoordNSSec = Mathf.Clamp(CoordNSSec, 0, (CoordNSDeg < 90) ? 59 : 0);

            CoordEWDeg = Mathf.Clamp(CoordEWDeg, 0, 180);
            CoordEWMin = Mathf.Clamp(CoordEWMin, 0, (CoordEWDeg < 180) ? 59 : 0);
            CoordEWSec = Mathf.Clamp(CoordEWSec, 0, (CoordEWDeg < 180) ? 59 : 0);

            Year  = Mathf.Clamp(Year, 1950, 2050);
            Month = Mathf.Clamp(Month, 1, 12);
            Day   = Mathf.Clamp(Day, 1, DateTime.DaysInMonth(Year, Month));

            Hour   = Mathf.Clamp(Hour, 0, 23);
            Minute = Mathf.Clamp(Minute, 0, 59);

            UtcOffset = Mathf.Clamp(UtcOffset, -12f, +14f);
        }

        // Must be called on every update loop, otherwise the networking code will stop functioning properly.
        void ComputeAzimuthAltitude()
        {
            if (PlacementMode == SunPlacementMode.Geographical)
            {
                if (CoordinateMode == SunCoordinateMode.Address)
                {
                    if (m_WaitingOnWebOperation != null && m_WaitingOnWebOperation.isDone)
                    {
                        var webRequest = m_WaitingOnWebOperation.webRequest;
                        m_CurrentGeocodingAddressRequest = null;
    
                        if (!webRequest.isHttpError && !webRequest.isNetworkError)
                        {
                            string status       = "";
                            string errorMessage = null;
                            List<GeocodingResult> results = SunStudyGeocoding.ParseLatLongFromOKResponse(CoordAddress, webRequest.downloadHandler.text, out status, out errorMessage);

                            if (string.IsNullOrEmpty(errorMessage))
                            {
                                if (results.Count > 0)
                                {
                                    GeocodingResult result = results[0];

                                    CoordLatitude  = result.Latitude;
                                    CoordLongitude = result.Longitude;

                                    if (result.IsApproximate)
                                    {
                                        GeocodingMessage = "This address gives an approximate location.";
                                    }
                                    else if (result.IsPartial)
                                    {
                                        GeocodingMessage = "This address gives a partial match.";
                                    }
                                    else
                                    {
                                        GeocodingMessage = "This address gives many locations; using the first one.";
                                    }
                                }
                                else
                                {
                                    GeocodingMessage = string.Empty;
                                }
                            }
                            else
                            {
                                if (status == "ZERO_RESULTS")
                                {
                                    GeocodingMessage = $"Need more details for the address, like street, city and country.";
                                }
                                else
                                {
                                    GeocodingMessage = "The request has failed, see the console for details.";
                                    Debug.LogError($"Sun Study geocoding request error for '{CoordAddress}': {status} {errorMessage}");
                                }
                            }
                        }
                        else
                        {
                            GeocodingMessage = "The request has failed; see the console for details.";
                            Debug.LogError($"Sun Study geocoding request error for '{CoordAddress}': network error.");
                        }

                        webRequest.Dispose();
                        webRequest = null;

                        m_WaitingOnWebOperation = null;
                    }
                    else if (m_WaitingOnWebOperation != null && !m_WaitingOnWebOperation.isDone)
                    {
                        if (m_CurrentGeocodingAddressRequest != null && m_CurrentGeocodingAddressRequest != CoordAddress)
                        {
                            // Since the address changed, abort the current request (set it to done, then clean up).
                            m_WaitingOnWebOperation.webRequest.Abort();
                        }
                    }
                    else if (string.IsNullOrEmpty(m_CurrentGeocodingAddressRequest) && !string.IsNullOrWhiteSpace(CoordAddress))
                    {
                        float cachedLatitude  = 0f;
                        float cachedLongitude = 0f;
                        bool isCached = SunStudyGeocoding.TryGetAddressLatLong(CoordAddress, out cachedLatitude, out cachedLongitude);

                        if (!isCached)
                        {
                            m_CurrentGeocodingAddressRequest = CoordAddress;

                            bool errorFlag = false;
                            m_WaitingOnWebOperation = SunStudyGeocoding.ConvertAddressToLatLong(CoordAddress, out errorFlag);

                            if (errorFlag)
                            {
                                if (m_WaitingOnWebOperation != null)
                                {
                                    if (m_WaitingOnWebOperation.webRequest != null)
                                    {
                                        m_WaitingOnWebOperation.webRequest.Dispose();
                                    }
                                    m_WaitingOnWebOperation = null;
                                }

                                m_CurrentGeocodingAddressRequest = null;
                                GeocodingMessage = "Make sure the Geocoding API Key is valid.";
                            }
                        }
                        else
                        {
                            CoordLatitude  = cachedLatitude;
                            CoordLongitude = cachedLongitude;
                        }
                    }
                    else if (string.IsNullOrWhiteSpace(CoordAddress))
                    {
                        m_CurrentGeocodingAddressRequest = null;
                        GeocodingMessage = string.Empty;
                    }
                }
                else if (CoordinateMode == SunCoordinateMode.DegreesMinutesSeconds)
                {
                    CoordLatitude = (CoordNSDeg + CoordNSMin / 60f + CoordNSSec / 3600f);
                    CoordLatitude *= (CoordNS == SunCoordinateNS.South) ? -1f : +1f;

                    CoordLongitude = (CoordEWDeg + CoordEWMin / 60f + CoordEWSec / 3600f);
                    CoordLongitude *= (CoordEW == SunCoordinateEW.West) ? -1f : +1f;
                }

                int utcOffsetHour   = (int)UtcOffset;
                int utcOffsetMinute = (int)((UtcOffset - utcOffsetHour) * 60f);
                var dateTime = new DateTimeOffset(Year, Month, Day, Hour, Minute, 0, new TimeSpan(utcOffsetHour, utcOffsetMinute, 0));
                (Azimuth, Altitude) = CalculateSunPosition(dateTime, CoordLatitude, CoordLongitude);

                if (ApplyDimming)
                {
                    Dimming = CalculateSolarDimming(dateTime, CoordLatitude, CoordLongitude, Altitude);
                }
            }
        }

        public static (float azimuth, float altitude) CalculateSunPosition(DateTimeOffset dateTime, double latitude, double longitude)
        {
            DateTimeOffset dateTimeUtc = dateTime.ToUniversalTime();  // convert to UTC

            // Number of days from Julian 2000.
            // To convert from Julian day to Julian 2000, subtract 2451545.0 from Julian day.
            // In the original Julian day formula, the term '+ 1721013.5' becomes '- 730531.5'.
            double julianDate      = 367 * dateTimeUtc.Year - (int)((7.0 / 4.0) * (dateTimeUtc.Year + (int)((dateTimeUtc.Month + 9.0) / 12.0))) + (int)((275.0 * dateTimeUtc.Month) / 9.0) + dateTimeUtc.Day - 730531.5;
            double julianCenturies = julianDate / 36525.0;

            // Sidereal time: https://en.wikibooks.org/wiki/Astrodynamics/Time
            double siderealTimeHours = 6.697375 + (0.06570982441908 * 36525.0) * julianCenturies;
            double siderealTimeUT    = siderealTimeHours + (366.2422 / 365.2422) * (double)dateTimeUtc.TimeOfDay.TotalHours;
            double siderealTime      = siderealTimeUT * 15.0 + longitude;

            // Refine to number of days (fractional) to specific time.  
            julianDate     += (double)dateTimeUtc.TimeOfDay.TotalHours / 24.0;
            julianCenturies = julianDate / 36525.0;

            // Solar coordinates: https://en.wikipedia.org/wiki/Position_of_the_Sun
            double meanLongitude     = CorrectAngle(Mathf.Deg2Rad * (280.460 + (0.9856474 * 36525.0) * julianCenturies));
            double eclipticLongitude = CalculateEclipticLongitude(meanLongitude, julianCenturies);
            (double rightAscension, double declination) = CalculateSolarRightAscensionAndDeclination(eclipticLongitude, julianCenturies);

            double hourAngle = CorrectAngle(siderealTime * Mathf.Deg2Rad) - rightAscension;
            if (hourAngle > Math.PI)
            {
                hourAngle -= 2 * Math.PI;
            }
            
            // Altitude: matches formula in US Naval Observatory website.
            double altitude = Math.Asin(Math.Sin(latitude * Mathf.Deg2Rad) * Math.Sin(declination) + Math.Cos(latitude * Mathf.Deg2Rad) * Math.Cos(declination) * Math.Cos(hourAngle));

            // Numerator and denominator for calculating azimuth angle are needed to test which quadrant the angle is in.  
            double numerator   = -Math.Sin(hourAngle);
            double denominator = Math.Tan(declination) * Math.Cos(latitude * Mathf.Deg2Rad) - Math.Sin(latitude * Mathf.Deg2Rad) * Math.Cos(hourAngle);

            // Azimuth: matches formula in US Naval Observatory website.
            double azimuth = Math.Atan(numerator / denominator);
            if (denominator < 0) // in 2nd or 3rd quadrant  
            {
                azimuth += Math.PI;
            }
            else if (numerator < 0) // in 4th quadrant  
            {
                azimuth += 2 * Math.PI;
            }

            return ((float)(azimuth * Mathf.Rad2Deg), (float)(altitude * Mathf.Rad2Deg));
        }

        public static double CalculateEclipticLongitude(double meanLongitude, double julianCenturies)
        {
            // Mean solar anomaly: values from the US Naval Observatory website.
            double meanAnomaly      = CorrectAngle(Mathf.Deg2Rad * (357.529 + (0.98560028 * 36525.0) * julianCenturies));
            double equationOfCenter = Mathf.Deg2Rad * 1.915 * Math.Sin(meanAnomaly) + Mathf.Deg2Rad * 0.02 * Math.Sin(2 * meanAnomaly);

            return CorrectAngle(meanLongitude + equationOfCenter);
        }

        public static (double rightAscension, double declination) CalculateSolarRightAscensionAndDeclination(double eclipticLongitude, double julianCenturies)
        {
            double obliquity = (23.439 - (0.00000036 * 36525.0) * julianCenturies) * Mathf.Deg2Rad;

            // Right ascension : matches formula in US Naval Observatory website.
            double rightAscension = Math.Atan2(Math.Cos(obliquity) * Math.Sin(eclipticLongitude), Math.Cos(eclipticLongitude));
            double declination    = Math.Asin(Math.Sin(eclipticLongitude) * Math.Sin(obliquity));

            return (rightAscension, declination);
        }

        public static float CalculateSolarDimming(DateTimeOffset dateTime, double latitude, double longitude, double altitude)
        {
            const double twilightAltitudeLowerBound = -18.0;
            if (altitude < twilightAltitudeLowerBound)
            {
                return 0f; // under twilight
            }

            DateTimeOffset dateTimeUtc = dateTime.ToUniversalTime();  // convert to UTC

            // Use local time but with UTC day.
            // Take this DateTime as local but with new UTC hours/minutes and transfer it back to local time. Since this may change the day, pass the UTC day.
            var dateTimeOffsetLocalTime = new DateTimeOffset(dateTimeUtc.Year, dateTimeUtc.Month, dateTimeUtc.Day, dateTime.Hour, dateTime.Minute, 0, new TimeSpan(dateTime.Offset.Hours, dateTime.Offset.Minutes, 0));

            double julianDate      = 367 * dateTimeUtc.Year - (int)((7.0 / 4.0) * (dateTimeUtc.Year + (int)((dateTimeUtc.Month + 9.0) / 12.0))) + (int)((275.0 * dateTimeUtc.Month) / 9.0) + dateTimeUtc.Day + 1721013.5 - k_JulianDateYear2000;
            double julianDateDay   = julianDate;
            double julianCenturies = julianDate / 36525.0;

            // Refine to number of days (fractional) to specific time.  
            julianDate     += (double)dateTime.TimeOfDay.TotalHours / 24.0;
            julianCenturies = julianDate / 36525.0;

            // Solar coordinates: https://en.wikipedia.org/wiki/Position_of_the_Sun
            double meanLongitude = CorrectAngle(Mathf.Deg2Rad * (280.460 + (0.9856474 * 36525.0) * julianCenturies));

            double eclipticLongitude = CalculateEclipticLongitude(meanLongitude, julianCenturies);
            (double rightAscensionUnused, double declination) = CalculateSolarRightAscensionAndDeclination(eclipticLongitude, julianCenturies);

            declination = declination * Mathf.Rad2Deg;

            // The check below uses approximate dates as an early test and then apply a better formula.
            // The season approximation is only to tell if it will be day or night for 24h.
            // Latitudes are positive in the Northern hemisphere and negative in the Southern hemisphere.
            if (latitude > 60.0)
            {
                var winterSeasonStart = new DateTimeOffset(dateTime.Month >= 1 && dateTime.Month <= 8 ? dateTime.Year - 1 : dateTime.Year, 9, 01, 0, 0, 0, dateTime.Offset);
                var winterSeasonEnd   = new DateTimeOffset(dateTime.Month >= 9 ? dateTime.Year + 1 : dateTime.Year, 01, 21, 23, 59, 59, dateTime.Offset);

                var summerSeasonStart = new DateTimeOffset(dateTime.Year, 03, 21, 0, 0, 0, dateTime.Offset);
                var summerSeasonEnd   = new DateTimeOffset(dateTime.Year, 09, 21, 23, 59, 59, dateTime.Offset);

                if (dateTime >= winterSeasonStart && dateTime <= winterSeasonEnd)
                {
                    // In Northern hemisphere, in winter, the declination is negative.
                    if (latitude >= 90.0 + declination)
                    {
                        return 0f; // winter in Northern hemisphere with 24 hour night
                    }
                }
                else if (dateTime >= summerSeasonStart && dateTime <= summerSeasonEnd)
                {
                    // In Northern hemisphere, in summer, the declination is positive.
                    if (declination + -90 >= latitude || latitude >= 90 - declination)
                    {
                        return 1f; // summer in Northern hemisphere with 24 hour day
                    }
                }
            }
            else if (latitude < -60.0)
            {
                var winterSeasonStart = new DateTimeOffset(dateTime.Year, 05, 1, 0, 0, 0, dateTime.Offset);
                var winterSeasonEnd   = new DateTimeOffset(dateTime.Year, 08, 1, 23, 59, 59, dateTime.Offset);

                var summerSeasonStart = new DateTimeOffset(dateTime.Month >= 1 && dateTime.Month <= 7 ? dateTime.Year - 1 : dateTime.Year, 08, 21, 0, 0, 0, dateTime.Offset);
                var summerSeasonEnd   = new DateTimeOffset(dateTime.Month >= 8 ? dateTime.Year + 1 : dateTime.Year, 03, 21, 23, 59, 59, dateTime.Offset);

                if (dateTime >= winterSeasonStart && dateTime <= winterSeasonEnd)
                {
                    // In Southern hemisphere, in winter, declination is negative.
                    if (latitude <= -90 - declination || declination + -90 >= latitude)
                    {
                        return 0f; // winter in Southern hemisphere with 24 hour night
                    }
                }
                else if (dateTime >= summerSeasonStart && dateTime <= summerSeasonEnd)
                {
                    // In Southern hemisphere, in summer, declination is positive.
                    if (latitude <= -90 + declination || latitude <= -90 - declination)
                    {
                        return 1f; // summer in Southern hemisphere with 24 hour day
                    }
                }
            }
                       
            (double julianSunrise, double julianSunset, double julianTransit) = GetJulianDatesForSunriseAndSunset(dateTime, latitude, longitude);
            if (double.IsNaN(julianSunrise) || double.IsNaN(julianSunset) || double.IsNaN(julianTransit))
            {
                // To cover all edge cases that pass when they should not.
                // For example, a difference of less than one degree makes the test (24 hour day/night) pass even when it should not really.
                return 1f;
            }

            double adjustedJulianDate     = julianDate + k_JulianDateYear2000;
            double adjustedStartJulianDay = julianDateDay + k_JulianDateYear2000;

            DateTimeOffset solarSunriseTime = MakeDateTimeOffsetWithJulianDateTimeFraction(julianSunrise, dateTimeOffsetLocalTime);
            DateTimeOffset solarSunsetTime  = MakeDateTimeOffsetWithJulianDateTimeFraction(julianSunset, dateTimeOffsetLocalTime);

            (float azRise, float altRise) = CalculateSunPosition(solarSunriseTime, latitude, longitude);
            (float azSet, float altSet)   = CalculateSunPosition(solarSunsetTime, latitude, longitude);

            float ratio = 0f;
            if ((adjustedJulianDate < julianTransit && altitude > altRise) || (adjustedJulianDate > julianTransit && altitude > altSet))
            {
                ratio = 1f; // daylight
            }
            else if (adjustedJulianDate < julianTransit && altitude < altRise)
            {
                double numerator   = altitude + Math.Abs(twilightAltitudeLowerBound);
                double denominator = altRise + Math.Abs(twilightAltitudeLowerBound);
                double t = numerator / denominator;

                ratio = Mathf.Lerp(0, 1, (float)t);
            }
            else if (adjustedJulianDate > julianTransit && altitude < altSet)
            {
                double numerator = altitude + Math.Abs(twilightAltitudeLowerBound);
                double denominator = altSet + Math.Abs(twilightAltitudeLowerBound);
                double t = numerator / denominator;

                ratio = Mathf.Lerp(0, 1, (float)t);
            }

            return ratio;
        }

        static double CorrectAngle(double angleInRadians)
        {
            if (angleInRadians < 0)
            {
                return 2 * Math.PI - (Math.Abs(angleInRadians) % (2 * Math.PI));
            }
            else if (angleInRadians > 2 * Math.PI)
            {
                return angleInRadians % (2 * Math.PI);
            }
            else
            {
                return angleInRadians;
            }
        }

        public static (double sunrise, double sunset, double transit) GetJulianDatesForSunriseAndSunset(DateTimeOffset localDateTime, double latitude, double longitude)
        {
            DateTimeOffset dateTimeUtc = localDateTime.ToUniversalTime(); // convert to UTC

            double julianDate      = 367 * dateTimeUtc.Year - (int)((7.0 / 4.0) * (dateTimeUtc.Year + (int)((dateTimeUtc.Month + 9.0) / 12.0))) + (int)((275.0 * dateTimeUtc.Month) / 9.0) + dateTimeUtc.Day + 1721013.5 - k_JulianDateYear2000;
            double julianDateDay   = julianDate;
            double julianCenturies = julianDate / 36525.0;

            // Refine to number of days (fractional) to specific time.  
            julianDate     += (double)dateTimeUtc.TimeOfDay.TotalHours / 24.0;
            julianCenturies = julianDate / 36525.0;

            // Solar coordinates: https://en.wikipedia.org/wiki/Position_of_the_Sun
            double meanLongitude     = CorrectAngle(Mathf.Deg2Rad * (280.460 + (0.9856474 * 36525.0) * julianCenturies));
            double eclipticLongitude = CalculateEclipticLongitude(meanLongitude, julianCenturies);
            (double rightAscension, double declination) = CalculateSolarRightAscensionAndDeclination(eclipticLongitude, julianCenturies);
            rightAscension = CorrectAngle(rightAscension);

            double rightAscensionHours = (Mathf.Rad2Deg * rightAscension / 15.0);
            double endOfTransit = (Mathf.Rad2Deg * meanLongitude / 15.0) - rightAscensionHours;

            // Intensity code.
            double n = julianDateDay + 0.5; // 0.5 for noon to match the formula of https://en.wikipedia.org/wiki/Sunrise_equation
            double meanSolarNoon = n - (longitude / 360.0);
            double meanSolarWikiAnom = CorrectAngle(Mathf.Deg2Rad * (357.5291 + (0.98560028 * meanSolarNoon)));

            // The algorithm expects degrees for julianTransit.
            // endOfTransit in hours but represents day minutes and needs to be converted to a day fraction since it will be added to a Julian date fraction.
            double julianTransit = 2451545.0 + meanSolarNoon + (endOfTransit / 24.0 / 60.0);

            // -0.83 degree is the correction for astronomical refraction and solar disc diameter: https://en.wikipedia.org/wiki/Sunrise_equation
            double hourAngleNumerator   = Math.Sin(-0.83 * Mathf.Deg2Rad) - Math.Sin(latitude * Mathf.Deg2Rad) * Math.Sin(declination);
            double hourAngleDenominator = Math.Cos(latitude * Mathf.Deg2Rad) * Math.Cos(declination);
            double hourAngleDiv         = hourAngleNumerator / hourAngleDenominator;
            double hourAngle            = CorrectAngle(Math.Acos(hourAngleDiv));
            double hourAngleDegrees     = hourAngle * Mathf.Rad2Deg;

            // The algorithm expects degrees for sunrise and sunset.
            double julianRise = julianTransit - hourAngleDegrees / 360f;
            double julianSet  = julianTransit + hourAngleDegrees / 360f;

            return (julianRise, julianSet, julianTransit);
        }

        public static DateTimeOffset MakeDateTimeOffsetWithJulianDateTimeFraction(double julianDateTimeFraction, DateTimeOffset utcDateLocalTimeOffset)
        {
            int julianDateInteger     = (int)julianDateTimeFraction;
            double julianTimeFraction = julianDateTimeFraction - julianDateInteger;
            TimeSpan hourAndMinutes = GetHourAndMinutesFromJulianDateFraction(julianTimeFraction);

            // Keep the proper date but convert the day fraction into proper time values.
            var            utcDateTime         = new DateTimeOffset(utcDateLocalTimeOffset.Year, utcDateLocalTimeOffset.Month, utcDateLocalTimeOffset.Day, hourAndMinutes.Hours, hourAndMinutes.Minutes, 0, new TimeSpan(0, 0, 0));
            DateTimeOffset localDateTime       = utcDateTime.Add(utcDateLocalTimeOffset.Offset);
            var            localDateTimeOffset = new DateTimeOffset(localDateTime.Year, localDateTime.Month, localDateTime.Day, localDateTime.Hour, localDateTime.Minute, 0, utcDateLocalTimeOffset.Offset);

            return localDateTimeOffset;
        }

        static TimeSpan GetHourAndMinutesFromJulianDateFraction(double fraction)
        {
            double timeHourAndRest = 24.0 * fraction + 12; // Julian date fraction at 0 is noon: adding 12 to convert correctly
            int    timeHour        = timeHourAndRest > 24.0 ? (int)(timeHourAndRest - 24.0) : (int)timeHourAndRest;

            double timeMinuteAndRestFraction = (timeHourAndRest % 24.0 - timeHour) * 60.0;
            int    timeMinutes               = (int)timeMinuteAndRestFraction;

            return new TimeSpan(timeHour, timeMinutes, 0);
        }
    }
}

#endif
