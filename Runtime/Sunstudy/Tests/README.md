# README for Tests in Unity Test Framework 'UTF' #

Copyright (c) 2019 Unity Technologies. All rights reserved.

The tests validate the accuracy of our conversion code to convert from latitude & longitude at a specific time to azimuth & altitude.
Tests are composed of reference data in JSON files that comes from [NREL's SPA](https://midcdmz.nrel.gov/solpos/spa.html).
Inside each JSON Test files is one set of coordinates (lat/long), a time offset from UTC and a list of values to validate.
Each test contains the following input
1. Date (year, month, day)
2. Time (hour, minute, seconds)

And the reference data:

1. Azimuth
2. Altitude

## Reference Data ##

The US National Renewable Energy Laboratory (NREL) provides a webpage to retrieve the position of the sun with many parameters. The webpage uses the [Solar Position Algorithm (SPA)](https://midcdmz.nrel.gov/solpos/spa.html) which is extremely precise.

> This algorithm calculates the solar zenith and azimuth angles in the period from the year -2000 to 6000, with uncertainties of +/- 0.0003 degrees based on the date, time, and location on Earth.

The [SPA webpage](https://midcdmz.nrel.gov/solpos/spa.html) can give many samples of the sun position for many days and at a custom *time interval in minutes*.
For the tests, the files currently include a single day, with each test being at a one hour interval: from midnight to 23h.

To generate new values from [SPA](https://midcdmz.nrel.gov/solpos/spa.html), modify the latitude, longitude and time zone. For the other site location info,
use the default values:

- Observer Elevation: 0 (not the default): We do not use the elevation in our implementation since it doesn't make a significant difference.
- Annual average local pressure: 835
- Annual average local temperature: 10

- (delta)UT1: 0
- (delta)T (Terrestrial Time): 64.797

**IMPORTANT**: Currently the JSON test files use the Terrestrial Time (TT) default value from SPA. However, since we do not use it in our implementation, it would make
sense to set the Terrestrial Time to 0 for all tests. If TT is set to 0, the test SPA values will change but the difference will be insignificant.

Optional input values:
- Surface Azimuth Rotation: **0** : The default is 180 but all tests were generated with 0. We use 0 because we do not use that value in our implementation.
- Surface slope: 0 (the default)
- Atmospheric Refraction: 0.5667 (the default)

## JSON ##
The output of the [SPA webpage](https://midcdmz.nrel.gov/solpos/spa.html) is a text table of comma separated values. This output needs to be formatted in the JSON fileinto a list of *values* which each value JSON object having the following:
- Date Year (test input)
- Date Month (test input)
- Date Day (test input)
- Time Hour (test input)
- Time Minute (test input)
- Time Seconds (test input)
- Local Sunrise Time (test validation)
- Local Sunset Time (test validation)
- Azimuth (test validation)
- Altitude (test validation)

At the root of the JSON, the following information is extremely important since it must exactly match the data that was input in the [SPA webpage](https://midcdmz.nrel.gov/solpos/spa.html):
- Latitude / Longtitude 
- UTC Offset (including DST for the selected Date)

Coordinates as well as the UTC Offset in the JSON are in float.

## Test Run ##

Each test loads a specific JSON file and retrieves the coordinates and UTC offset. For each test input object, it calls the function to calculate the sun's position with a DateTime object (including the offset) and the coordinates. It takes the returned azimuth and altitude and matches them with the SPA reference values include in the JSON. For the azimuth and altitude tests, the code asserts with a precision of 1 minute (1 / 60 degree). For the Sunrise and sunset tests, the precision is 1 hour.