using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Unity.SunStudy
{
    // Background information for these classes: https://forum.unity.com/threads/how-parse-json-data-in-unity-in-c.383804/
    // WARNING: The data members in these classes have to be public fields, not properties, and have the exact spelling used in the JSON.
    namespace SunStudyGeocodingJson
    {
        [System.Serializable]
        public class RootObject
        {
            public string   error_message;
            public Result[] results;
            public string   status;
        }

        [System.Serializable]
        public class Result
        {
            public AddressComponents[] address_components;
            public string              formatted_address;
            public Geometry            geometry;
            public bool                partial_match;
            public string              place_id;
            public string[]            types;
        }

        [System.Serializable]
        public class Geometry
        {
            public Bounds                 bounds;
            public LatitudeLongitudeCoord location;
            public string                 location_type;
            public Viewport               viewport;
        }

        [System.Serializable]
        public class Bounds
        {
            public LatitudeLongitudeCoord northeast;
            public LatitudeLongitudeCoord southwest;
        }

        [System.Serializable]
        public class LatitudeLongitudeCoord
        {
            public float lat;
            public float lng;
        }

        [System.Serializable]
        public class Viewport
        {
            public LatitudeLongitudeCoord northeast;
            public LatitudeLongitudeCoord southwest;
        }

        [System.Serializable]
        public class AddressComponents
        {
            public string   long_name;
            public string   short_name;
            public string[] types;
        }
    }

    public class GeocodingResult
    {
        public string FormattedAddress { get; set; }
        public float  Latitude { get; set; }
        public float  Longitude { get; set; }
        public bool   IsPartial { get; set; }
        public bool   IsApproximate { get; set; }
    }

    public class SunStudyGeocoding
    {
        const string k_GoogleGeocodingApiUrl = "https://maps.googleapis.com/maps/api/geocode/";

        static Dictionary<string, List<GeocodingResult>> s_ResultCache = new Dictionary<string, List<GeocodingResult>>();

        public static bool TryGetAddressLatLong(string address, out float latitude, out float longitude)
        {
            if (s_ResultCache.ContainsKey(address))
            {
                var entry = s_ResultCache[address][0];
                (latitude, longitude) = (entry.Latitude, entry.Longitude);

                return true;
            }
            else
            {
                latitude  = 0f;
                longitude = 0f;

                return false;
            }
        }

        public static UnityWebRequestAsyncOperation ConvertAddressToLatLong(string address, out bool errorFlag)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                errorFlag = true;
                return null;
            }

            string googleGeocodingApiKey = PlayerPrefs.GetString(SunStudy.k_PlayerPrefGeocodingApiKey);
            if (string.IsNullOrWhiteSpace(googleGeocodingApiKey))
            {
                errorFlag = true;
                return null;
            }

            // UTF-8 is the way to go for asian languages as of 2017 per https://en.wikipedia.org/wiki/Japanese_language_and_computers but still controversial in some aspects.
            byte[] queryParamBytes = UnityWebRequest.SerializeSimpleForm(new Dictionary<string, string> { { "address", address }, { "key", googleGeocodingApiKey } });
            string finalUrl        = k_GoogleGeocodingApiUrl + "json?" + Encoding.UTF8.GetString(queryParamBytes);
            UnityWebRequest request = UnityWebRequest.Get(finalUrl);
            request.timeout = 5; // seconds

            errorFlag = false;

            return request.SendWebRequest();
        }

        public static List<GeocodingResult> ParseLatLongFromOKResponse(string address, string response, out string status, out string errorMessage)
        {
            var results = new List<GeocodingResult>();

            // Need to box the struct to use a ref instead of copy value; see: https://docs.unity3d.com/ScriptReference/EditorJsonUtility.FromJsonOverwrite.html
            var root = new SunStudyGeocodingJson.RootObject();
            object boxedRoot = root;
            JsonUtility.FromJsonOverwrite(response, boxedRoot);
            root = (SunStudyGeocodingJson.RootObject)boxedRoot;

            if (root != null)
            {
                status = root.status;

                if (root.results != null && root.results.Length > 0 && root.results[0] != null)
                {
                    errorMessage = null;

                    foreach (var jsonResult in root.results)
                    {
                        if (jsonResult != null)
                        {
                            var result = new GeocodingResult();

                            result.FormattedAddress = jsonResult.formatted_address;
                            result.Latitude         = jsonResult.geometry.location.lat;
                            result.Longitude        = jsonResult.geometry.location.lng;
                            result.IsPartial        = jsonResult.partial_match;
                            result.IsApproximate    = jsonResult.geometry.location_type == "APPROXIMATE" || jsonResult.geometry.location_type == "RANGE_INTERPOLATED";

                            results.Add(result);
                        }
                    }

                    s_ResultCache.Add(address, results);
                }
                else
                {
                    if (root.status == "ZERO_RESULTS")
                    {
                        // The error message is empty for this type of error.
                        errorMessage = $"No results found for this address.";
                    }
                    else
                    {
                        errorMessage = root.error_message;
                    }
                }
            }
            else
            {
                status       = "unknown";
                errorMessage = "Google geocoding response object is null.";
            }

            return results;
        }
    }
}
