# README for Sun Study Tests in Unity Test Framework #

These tests validate the accuracy of the conversion code to convert from latitude and longitude at a specific time to azimuth and altitude.
Tests are composed of reference data in JSON files that comes from [NREL's SPA](https://midcdmz.nrel.gov/solpos/spa.html).

Inside each JSON file is one set of coordinates (latitude and longitude), a time offset from UTC and a list of values to validate.

Each test contains the following input:
- Date (year, month, day)
- Time (hour, minute, second)

And the reference data:
- Azimuth
- Altitude

## Reference Data ##

The US National Renewable Energy Laboratory (NREL) provides a webpage to retrieve the position of the sun with many parameters.
The webpage uses the [Solar Position Algorithm (SPA)](https://midcdmz.nrel.gov/solpos/spa.html) which is extremely precise.

> This algorithm calculates the solar zenith and azimuth angles in the period from year -2000 to year 6000, with uncertainties of +/- 0.0003 degrees based on the date, time and location on earth.

SPA can give many samples of the sun position for many days and at a custom *time interval in minutes*.
For the tests, the JSON files currently include a single day, with each test being at a one hour interval, from midnight to 23:00.

To generate new values from SPA, modify the latitude, longitude and time zone. For the other site location info, use the default values:

- Observer elevation: **0** (not the default: the elevation is not used in this implementation since it does not make a significant difference)
- Annual average local pressure: 835
- Annual average local temperature: 10
- (delta)UT1: 0.0
- (delta)T: 64.797

Currently, the JSON files use the default (delta)T value from SPA.
However, since it is not used in this implementation, it would make sense to set it to 0 for all tests.
If set to 0, the test SPA values will change but the difference will be insignificant.

Optional input values:
- Surface azimuth rotation: **0** (the default is 180, but all tests were generated with 0 since this value is not used in this implementation)
- Surface slope: 0
- Atmospheric refraction: 0.5667

## JSON ##

The output of SPA is a text table of comma separated values.
This output needs to be formatted in the JSON file into a list of JSON object *values* having the following data:

For test input:
- Date year
- Date month
- Date day
- Time hour
- Time minute
- Time second

And for test validation:
- Local sunrise time
- Local sunset time
- Azimuth
- Altitude

At the root of the JSON, the following float values are extremely important since they must exactly match the data that was input in SPA:
- Latitude
- Longitude
- UTC offset (including DST for the selected date)

## Test Run ##

Each test loads a specific JSON file and retrieves the latitude, longitude and UTC offset.

For each JSON object *value*, the test calls a function to calculate the sun position with the latitude, longitude and a DateTime object (including the UTC offset).

The test takes each returned azimuth and altitude and compares them with the SPA reference values included in the JSON:
- For azimuth and altitude, the test asserts with a precision of 1 minute (1/60 degree).
- For sunrise and sunset, the test asserts with a precision of 1 hour.
