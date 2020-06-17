using System;
using System.Collections.Generic;
using Unity.SunStudy;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UnityEngine.Reflect
{
    public class SunStudyTopMenu : TopMenu
    {
        public SunStudy target;
        public GameObject defaultLightPrefab;

        public Slider DayOfYearSlider;
        public Slider MinuteOfDaySlider;
        public Slider UtcOffsetSlider;
        public Slider LatitudeSlider;
        public Slider LongitudeSlider;
        public Slider NorthSlider;

        public Text DayOfYearLabel;
        public Text MinuteOfDayLabel;
        public Text UtcOffsetLabel;
        public Text LatitudeLabel;
        public Text LongitudeLabel;
        public Text NorthLabel;

        public CanvasGroup WindowGroup;
        public float transparentModeAlpha = .1f;

        protected override void Start()
        {
            base.Start();

            if (target == null)
                target = FindOrCreateSunStudy();

            DayOfYearSlider.value = target.DayOfYear;
            MinuteOfDaySlider.value = target.MinuteOfDay;
            UtcOffsetSlider.value = target.UtcOffset;
            LatitudeSlider.value = target.CoordLatitude;
            LongitudeSlider.value = target.CoordLongitude;
            NorthSlider.value = target.NorthAngle;
            UpdateLabels();
        }

        SunStudy FindOrCreateSunStudy()
        {
            var existingSunStudyObjects = FindObjectsOfType<SunStudy>();

            if (existingSunStudyObjects.Length > 0)
                return existingSunStudyObjects[0];

            // Instantiate the default light prefab (to have the proper settings set)
            var gameObject = Instantiate(defaultLightPrefab);
            gameObject.transform.parent = transform;
            return gameObject.GetComponent<SunStudy>();
        }

        void UpdateLabels()
        {
            DayOfYearLabel.text = $"{NameOfMonth(target.Month)} {target.Day}";
            MinuteOfDayLabel.text = $"{target.Hour}:{target.Minute:00}";
            UtcOffsetLabel.text = NameOfUtcOffset(target.UtcOffset);
            LatitudeLabel.text = $"{target.CoordLatitude}";
            LongitudeLabel.text = $"{target.CoordLongitude}";
            NorthLabel.text = $"{target.NorthAngle}";
        }

        public void SetDayOfYear(float dayOfYear)
        {
            if (target != null)
            {
                target.DayOfYear = (int)dayOfYear;
                UpdateLabels();
            }
        }

        public void SetMinuteOfDay(float minuteOfDay)
        {
            if (target != null)
            {
                target.MinuteOfDay = (int)minuteOfDay;
                UpdateLabels();
            }
        }

        public void SetUtcOffset(float offset)
        {
            if (target != null)
            {
                offset -= (offset % 25); // only 15 minutes increments
                target.UtcOffset = offset / 100f;
                UpdateLabels();
            }
        }

        public void SetLatitude(float latitude)
        {
            if (target != null)
            {
                target.CoordLatitude = latitude;
                UpdateLabels();
            }
        }

        public void SetLongitude(float longitude)
        {
            if (target != null)
            {
                target.CoordLongitude = longitude;
                UpdateLabels();
            }
        }

        public void SetTrueNorth(float northAngle)
        {
            if (target != null)
            {
                target.NorthAngle = northAngle;
                UpdateLabels();
            }
        }

        public void StartTransparentMode()
        {
            SetWindowOpacity(transparentModeAlpha);
        }

        public void StopTransparentMode()
        {
            SetWindowOpacity(1);
        }

        void SetWindowOpacity(float alpha)
        {
            WindowGroup.alpha = alpha;
        }

        string NameOfMonth(int monthNb)
        {
            switch (monthNb)
            {
                case 1:
                    return "Jan";
                case 2:
                    return "Feb";
                case 3:
                    return "Mar";
                case 4:
                    return "Apr";
                case 5:
                    return "May";
                case 6:
                    return "Jun";
                case 7:
                    return "Jul";
                case 8:
                    return "Aug";
                case 9:
                    return "Sept";
                case 10:
                    return "Oct";
                case 11:
                    return "Nov";
                default:
                    return "Dec";
            }
        }

        string NameOfUtcOffset(float offset)
        {
            var hours = (int) offset;
            var minutes = (int) Math.Abs((60 * (offset - hours)));

            return $"{hours}:{minutes:00}";
        }
    }
}
