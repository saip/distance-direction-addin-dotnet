﻿// Copyright 2016 Esri 
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using DistanceAndDirectionLibrary;
using DistanceAndDirectionLibrary.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProAppDistanceAndDirectionModule.ViewModels
{
    public class ProCircleViewModel : ProTabBaseViewModel
    {
        public ProCircleViewModel()
        {
            ActivateToolCommand = new ArcGIS.Desktop.Framework.RelayCommand(async () =>
            {
                await FrameworkApplication.SetCurrentToolAsync("ProAppDistanceAndDirectionModule_SketchTool");
                Mediator.NotifyColleagues("SET_SKETCH_TOOL_TYPE", ArcGIS.Desktop.Mapping.SketchGeometryType.Circle);
            });

            // we may need this in the future
            //Mediator.Register("SKETCH_COMPLETE", OnSketchComplete);

            //properties
            CircleType = CircleFromTypes.Radius;
        }

        // future use
        //private void OnSketchComplete(object obj)
        //{
        //    AddGraphicToMap(obj as ArcGIS.Core.Geometry.Geometry);
        //}

        public ArcGIS.Desktop.Framework.RelayCommand ActivateToolCommand { get; set; }

        #region Properties

        private double DistanceLimit = 20000000;
        private Boolean EndsWithDecimal = false;
        private String decimalSeparator = Thread.CurrentThread.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        CircleFromTypes circleType = CircleFromTypes.Radius;
        /// <summary>
        /// Type of circle property
        /// </summary>
        public CircleFromTypes CircleType
        {

            get { return circleType; }
            set
            {
                if (circleType == value)
                    return;

                circleType = value;

                double distanceInMeters = TravelRateInSeconds * TravelTimeInSeconds;
                if (RateUnit != DistanceTypes.Meters)
                {
                    // Prevent graphical glitches from excessively high inputs
                    distanceInMeters = ConvertFromTo(RateUnit, DistanceTypes.Meters, TravelRateInSeconds * TravelTimeInSeconds);
                }

                if (IsDistanceCalcExpanded)
                {
                    UpdateDistance(TravelRateInSeconds * TravelTimeInSeconds, RateUnit, (distanceInMeters < DistanceLimit));
                }
                else
                {
                    if (value == CircleFromTypes.Diameter)
                        DistanceString = (base.Distance * 2.0).ToString("0.##");
                }

                // reset distance
                RaisePropertyChanged(() => DistanceString);
                //RaisePropertyChanged(() => Distance);

                UpdateFeedback();
            }
        }

        TimeUnits timeUnit = TimeUnits.Minutes;
        /// <summary>
        /// Type of time units
        /// </summary>
        public TimeUnits TimeUnit
        {
            get
            {
                return timeUnit;
            }
            set
            {
                if (timeUnit == value)
                {
                    return;
                }
                timeUnit = value;

                double distanceInMeters = TravelRateInSeconds * TravelTimeInSeconds;
                if (RateUnit != DistanceTypes.Meters)
                {
                    // Prevent graphical glitches from excessively high inputs
                    distanceInMeters = ConvertFromTo(RateUnit, DistanceTypes.Meters, TravelRateInSeconds * TravelTimeInSeconds);
                }

                if (distanceInMeters > DistanceLimit)
                {
                    RaisePropertyChanged(() => TravelTimeString);
                    UpdateDistance(TravelRateInSeconds * TravelTimeInSeconds, RateUnit, false);
                    ClearTempGraphics();
                    if (HasPoint1)
                        // Re-add the point as it was cleared by ClearTempGraphics() but we still want to see it
                        AddGraphicToMap(Point1, ColorFactory.Instance.GreenRGB, null, true, 5.0);
                    throw new ArgumentException(DistanceAndDirectionLibrary.Properties.Resources.AEInvalidInput);
                }

                UpdateDistance(TravelTimeInSeconds * TravelRateInSeconds, RateUnit, true);

                // Trigger validation to clear error messages as necessary
                RaisePropertyChanged(() => RateTimeUnit);
                RaisePropertyChanged(() => TimeUnit);
                RaisePropertyChanged(() => TravelRateString);
                RaisePropertyChanged(() => TravelTimeString);
            }
        }

        /// <summary>
        /// Property for travel time in seconds
        /// </summary>
        private double TravelTimeInSeconds
        {
            get
            {
                switch (TimeUnit)
                {
                    case TimeUnits.Seconds:
                        {
                            return travelTime;
                        }
                    case TimeUnits.Minutes:
                        {
                            return travelTime * 60.0;
                        }
                    case TimeUnits.Hours:
                        {
                            return travelTime * 3600.0;
                        }
                    default:
                        return travelTime;
                }
            }
        }

        /// <summary>
        /// Property for travel rate in seconds
        /// </summary>
        private double TravelRateInSeconds
        {
            get
            {
                switch (RateTimeUnit)
                {
                    case RateTimeTypes.FeetHour:
                    case RateTimeTypes.KilometersHour:
                    case RateTimeTypes.MetersHour:
                    case RateTimeTypes.MilesHour:
                    case RateTimeTypes.NauticalMilesHour:
                        return TravelRate / 3600.0;
                    default:
                        return TravelRate;
                }
            }
        }

        /// <summary>
        /// String of time display
        /// </summary>
        private string travelTimeString = string.Empty;
        public string TravelTimeString
        {
            get
            {
                if (string.IsNullOrWhiteSpace(travelTimeString))
                    return TravelTime.ToString("0.##");

                return travelTimeString;
            }
            set
            {
                travelTimeString = string.Empty;
                // divide the manual input by 2
                double t = 0.0;
                if (double.TryParse(value, out t))
                {
                    if (t == 0.0)
                    {
                        // Don't set property(Distance) because this will overwrite the string if user entering zeros "00000"
                        travelTimeString = value;
                        return;
                    }
                    TravelTime = t;
                }
                else
                {
                    ClearTempGraphics();
                    if (HasPoint1)
                        // Re-add the point as it was cleared by ClearTempGraphics() but we still want to see it
                        AddGraphicToMap(Point1, ColorFactory.Instance.GreenRGB, null, true, 5.0);
                    throw new ArgumentException(DistanceAndDirectionLibrary.Properties.Resources.AEInvalidInput);
                }
            }
        }

        double travelTime = 0.0;
        /// <summary>
        /// Property for time display
        /// </summary>
        public double TravelTime
        {
            get
            {
                return travelTime;
            }
            set
            {
                if (value < 0.0)
                {
                    UpdateFeedbackWithGeoCircle();
                    ClearTempGraphics();
                    if (HasPoint1)
                        // Re-add the point as it was cleared by ClearTempGraphics() but we still want to see it
                        AddGraphicToMap(Point1, ColorFactory.Instance.GreenRGB, null, true, 5.0);
                    throw new ArgumentException(DistanceAndDirectionLibrary.Properties.Resources.AEMustBePositive);
                }

                travelTime = value;

                double distanceInMeters = TravelRateInSeconds * TravelTimeInSeconds;
                if (RateUnit != DistanceTypes.Meters)
                {
                    // Prevent graphical glitches from excessively high inputs
                    distanceInMeters = ConvertFromTo(RateUnit, DistanceTypes.Meters, TravelRateInSeconds * TravelTimeInSeconds);
                }
                if (distanceInMeters > DistanceLimit)
                {
                    RaisePropertyChanged(() => TravelTimeString);
                    UpdateDistance(TravelRateInSeconds * TravelTimeInSeconds, RateUnit, false);
                    ClearTempGraphics();
                    if (HasPoint1)
                        // Re-add the point as it was cleared by ClearTempGraphics() but we still want to see it
                        AddGraphicToMap(Point1, ColorFactory.Instance.GreenRGB, null, true, 5.0);
                    throw new ArgumentException(DistanceAndDirectionLibrary.Properties.Resources.AEInvalidInput);
                }

                // we need to make sure we are in the same units as the Distance property before setting
                UpdateDistance(TravelRateInSeconds * TravelTimeInSeconds, RateUnit, true);

                // Trigger validation to clear error messages as necessary
                RaisePropertyChanged(() => RateTimeUnit);
                RaisePropertyChanged(() => TimeUnit);
                RaisePropertyChanged(() => TravelRateString);
                RaisePropertyChanged(() => TravelTimeString);
            }

        }

        private void UpdateDistance(double distance, DistanceTypes fromDistanceType, bool belowLimit)
        {
            Distance = ConvertFromTo(fromDistanceType, LineDistanceType, distance);

            if (belowLimit)
            {
                UpdateFeedbackWithGeoCircle();
            }
        }

        /// <summary>
        /// String of rate display
        /// </summary>
        private string travelRateString = string.Empty;
        public string TravelRateString
        {
            get
            {
                if (string.IsNullOrWhiteSpace(travelRateString))
                    return TravelRate.ToString("0.##");

                return travelRateString;

            }
            set
            {
                travelRateString = string.Empty;
                // divide the manual input by 2
                double t = 0.0;
                if (double.TryParse(value, out t))
                {
                    if (t == 0.0)
                    {
                        // Don't set property(Distance) because this will overwrite the string if user entering zeros "00000"
                        travelRateString = value;
                        return;
                    }

                    TravelRate = t;
                }
                else
                {
                    ClearTempGraphics();
                    if (HasPoint1)
                        // Re-add the point as it was cleared by ClearTempGraphics() but we still want to see it
                        AddGraphicToMap(Point1, ColorFactory.Instance.GreenRGB, null, true, 5.0);
                    throw new ArgumentException(DistanceAndDirectionLibrary.Properties.Resources.AEInvalidInput);
                }
            }
        }

        double travelRate = 0.0;
        /// <summary>
        /// Property of rate display
        /// </summary>
        public double TravelRate
        {
            get
            {
                return travelRate;
            }
            set
            {
                if (value < 0.0)
                    throw new ArgumentException(DistanceAndDirectionLibrary.Properties.Resources.AEMustBePositive);

                travelRate = value;

                double distanceInMeters = TravelRateInSeconds * TravelTimeInSeconds;
                if (RateUnit != DistanceTypes.Meters)
                {
                    // Prevent graphical glitches from excessively high inputs
                    distanceInMeters = ConvertFromTo(RateUnit, DistanceTypes.Meters, TravelRateInSeconds * TravelTimeInSeconds);
                }
                if (distanceInMeters > DistanceLimit)
                {
                    UpdateDistance(TravelRateInSeconds * TravelTimeInSeconds, RateUnit, false);
                    RaisePropertyChanged(() => TravelRateString);
                    ClearTempGraphics();
                    if (HasPoint1)
                        // Re-add the point as it was cleared by ClearTempGraphics() but we still want to see it
                        AddGraphicToMap(Point1, ColorFactory.Instance.GreenRGB, null, true, 5.0);
                    throw new ArgumentException(DistanceAndDirectionLibrary.Properties.Resources.AEInvalidInput);
                }

                UpdateDistance(TravelRateInSeconds * TravelTimeInSeconds, RateUnit, true);
                RaisePropertyChanged(() => TravelRateString);

                // Trigger validation to clear error messages as necessary
                RaisePropertyChanged(() => TravelTimeString);
                RaisePropertyChanged(() => RateTimeUnit);
                RaisePropertyChanged(() => TimeUnit);
            }
        }

        public override DistanceTypes LineDistanceType
        {
            get
            {
                return base.LineDistanceType;
            }
            set
            {
                if (IsDistanceCalcExpanded)
                {
                    var before = base.LineDistanceType;
                    var temp = ConvertFromTo(before, value, Distance);
                    if (CircleType == CircleFromTypes.Diameter)
                        Distance = temp * 2.0;
                    else
                        Distance = temp;
                }

                base.LineDistanceType = value;

                double distanceInMeters = Distance;
                if (value != DistanceTypes.Meters)
                {
                    distanceInMeters = ConvertFromTo(value, DistanceTypes.Meters, Distance);
                }
                if (distanceInMeters > DistanceLimit)
                {
                    ClearTempGraphics();
                    if (HasPoint1)
                        // Re-add the point as it was cleared by ClearTempGraphics() but we still want to see it
                        AddGraphicToMap(Point1, ColorFactory.Instance.GreenRGB, null, true, 5.0);
                    throw new ArgumentException(DistanceAndDirectionLibrary.Properties.Resources.AEInvalidInput);
                }
            }
        }


        DistanceTypes rateUnit = DistanceTypes.Meters;
        public DistanceTypes RateUnit
        {
            get
            {
                switch (RateTimeUnit)
                {
                    case RateTimeTypes.FeetHour:
                    case RateTimeTypes.FeetSec:
                        return DistanceTypes.Feet;
                    case RateTimeTypes.KilometersHour:
                    case RateTimeTypes.KilometersSec:
                        return DistanceTypes.Kilometers;
                    case RateTimeTypes.MetersHour:
                    case RateTimeTypes.MetersSec:
                        return DistanceTypes.Meters;
                    case RateTimeTypes.MilesHour:
                    case RateTimeTypes.MilesSec:
                        return DistanceTypes.Miles;
                    case RateTimeTypes.NauticalMilesHour:
                    case RateTimeTypes.NauticalMilesSec:
                        return DistanceTypes.NauticalMiles;
                    default:
                        return DistanceTypes.Meters;
                }
            }
            set
            {
                if (rateUnit == value)
                {
                    return;
                }

                rateUnit = value;

                double distanceInMeters = TravelRateInSeconds * TravelTimeInSeconds;
                if (rateUnit != DistanceTypes.Meters)
                {
                    // Prevent graphical glitches from excessively high inputs
                    distanceInMeters = ConvertFromTo(rateUnit, DistanceTypes.Meters, TravelRateInSeconds * TravelTimeInSeconds);
                }
                if (distanceInMeters > DistanceLimit)
                {
                    RaisePropertyChanged(() => TravelTimeString);
                    UpdateDistance(TravelRateInSeconds * TravelTimeInSeconds, RateUnit, false);
                    ClearTempGraphics();
                    if (HasPoint1)
                        // Re-add the point as it was cleared by ClearTempGraphics() but we still want to see it
                        AddGraphicToMap(Point1, ColorFactory.Instance.GreenRGB, null, true, 5.0);
                    throw new ArgumentException(DistanceAndDirectionLibrary.Properties.Resources.AEInvalidInput);
                }

                UpdateDistance(TravelTimeInSeconds * TravelRateInSeconds, RateUnit, (distanceInMeters < DistanceLimit));

                RaisePropertyChanged(() => RateUnit);
            }
        }

        RateTimeTypes rateTimeUnit = RateTimeTypes.MilesHour;
        public RateTimeTypes RateTimeUnit
        {
            get
            {
                return rateTimeUnit;
            }
            set
            {
                if (rateTimeUnit == value)
                {
                    return;
                }
                rateTimeUnit = value;

                double distanceInMeters = TravelRateInSeconds * TravelTimeInSeconds;
                if (RateUnit != DistanceTypes.Meters)
                {
                    // Prevent graphical glitches from excessively high inputs
                    distanceInMeters = ConvertFromTo(RateUnit, DistanceTypes.Meters, TravelRateInSeconds * TravelTimeInSeconds);
                }
                if (distanceInMeters > DistanceLimit)
                {
                    RaisePropertyChanged(() => TravelTimeString);
                    UpdateDistance(TravelRateInSeconds * TravelTimeInSeconds, RateUnit, false);
                    ClearTempGraphics();
                    if (HasPoint1)
                        // Re-add the point as it was cleared by ClearTempGraphics() but we still want to see it
                        AddGraphicToMap(Point1, ColorFactory.Instance.GreenRGB, null, true, 5.0);
                    throw new ArgumentException(DistanceAndDirectionLibrary.Properties.Resources.AEInvalidInput);
                }

                UpdateDistance(TravelTimeInSeconds * TravelRateInSeconds, RateUnit, true);

                // Trigger validation to clear error messages as necessary
                RaisePropertyChanged(() => RateTimeUnit);
                RaisePropertyChanged(() => TravelTimeString);
                RaisePropertyChanged(() => TravelRateString);
            }
        }

        bool isDistanceCalcExpanded = false;
        public bool IsDistanceCalcExpanded
        {
            get { return isDistanceCalcExpanded; }
            set
            {
                isDistanceCalcExpanded = value;
                if (value == true)
                {
                    TravelRate = 0.0;
                    TravelTime = 0.0;
                    Distance = 0.0;
                    ResetFeedback();
                }
                else
                {
                    Reset(false);
                }

                ClearTempGraphics();
                if (HasPoint1)
                    AddGraphicToMap(Point1, ColorFactory.Instance.GreenRGB, null, true, 5.0);

                RaisePropertyChanged(() => IsDistanceCalcExpanded);
            }
        }

        /// <summary>
        /// Distance is always the radius
        /// Update DistanceString for user
        /// Do nothing for Radius mode, double the radius for Diameter mode
        /// </summary>
        public override string DistanceString
        {
            get
            {
                if (Distance == 0.0)
                    return base.DistanceString;

                String dString = "";
                if (CircleType == CircleFromTypes.Diameter)
                    dString = (Distance * 2.0).ToString("0.##");
                else
                    dString = (Distance).ToString("0.##");

                if (EndsWithDecimal)
                {
                    int indexOfDecimal = dString.IndexOf(decimalSeparator);
                    bool containsDecimal = indexOfDecimal >= 0;

                    if (containsDecimal)
                        return dString.Substring(0, indexOfDecimal + 1);
                    else
                        return dString + decimalSeparator;
                }

                base.distanceString = dString;

                return dString;
            }
            set
            {
                // lets avoid an infinite loop here
                if (string.Equals(base.DistanceString, value))
                    return;

                double d = 0.0;
                bool isValidValue = double.TryParse(value, out d);
                EndsWithDecimal = value.EndsWith(decimalSeparator);

                if (isValidValue)
                {
                    if ((d == 0.0) || EndsWithDecimal)
                    {
                        // Stop here, don't replace Distance value while user still entering for these cases 
                        base.DistanceString = value;
                        return;
                    }

                    if (Distance == d)
                        return;

                    // divide the manual input by 2
                    double dist = 0.0;
                    if (CircleType == CircleFromTypes.Diameter && isManualRadiusDiameterEntered)
                        dist = d / 2.0;
                    else
                        dist = d;

                    Distance = dist;

                    double distanceInMeters = dist;
                    if (LineDistanceType != DistanceTypes.Meters)
                    {
                        distanceInMeters = ConvertFromTo(LineDistanceType, DistanceTypes.Meters, Distance);
                    }
                    if (distanceInMeters > DistanceLimit)
                    {
                        ClearTempGraphics();
                        if (HasPoint1)
                            // Re-add the point as it was cleared by ClearTempGraphics() but we still want to see it
                            AddGraphicToMap(Point1, ColorFactory.Instance.GreenRGB, null, true, 5.0);
                        throw new ArgumentException(DistanceAndDirectionLibrary.Properties.Resources.AEInvalidInput);
                    }

                    UpdateFeedbackWithGeoCircle();
                }
                else
                {
                    ClearTempGraphics();
                    if (HasPoint1)
                        // Re-add the point as it was cleared by ClearTempGraphics() but we still want to see it
                        AddGraphicToMap(Point1, ColorFactory.Instance.GreenRGB, null, true, 5.0);
                    throw new ArgumentException(DistanceAndDirectionLibrary.Properties.Resources.AEInvalidInput);
                }

                // Trigger update to clear exception highlighting if necessary
                RaisePropertyChanged(() => LineDistanceType);
            }
        }

        #endregion

        #region Commands

        // when someone hits the enter key, create geodetic graphic
        internal override void OnEnterKeyCommand(object obj)
        {
            if (Distance == 0 || Point1 == null)
            {
                return;
            }
            isManualRadiusDiameterEntered = false;
            base.OnEnterKeyCommand(obj);
        }

        #endregion

        #region override events

        internal override void OnNewMapPointEvent(object obj)
        {
            var point = obj as MapPoint;
            if (point == null)
                return;

            if (IsDistanceCalcExpanded)
            {
                HasPoint1 = false;
            }

            base.OnNewMapPointEvent(obj);

            double distanceInMeters = TravelRateInSeconds * TravelTimeInSeconds;
            if (RateUnit != DistanceTypes.Meters)
            {
                // Prevent graphical glitches from excessively high inputs
                distanceInMeters = ConvertFromTo(RateUnit, DistanceTypes.Meters, TravelRateInSeconds * TravelTimeInSeconds);
            }

            if (IsDistanceCalcExpanded)
            {
                UpdateDistance(TravelTimeInSeconds * TravelRateInSeconds, RateUnit, (distanceInMeters < DistanceLimit));
            }
        }

        internal override void OnMouseMoveEvent(object obj)
        {
            if (!IsActiveTab)
                return;

            var point = obj as MapPoint;

            if (point == null)
                return;

            // dynamically update start point if not set yet
            if (!HasPoint1)
            {
                Point1 = point;
            }
            else if (HasPoint1 && !HasPoint2 && !IsDistanceCalcExpanded)
            {
                Point2Formatted = string.Empty;
                Distance = GetGeodesicDistance(Point1, point);
            }

            // update feedback
            if (HasPoint1 && !HasPoint2 && !IsDistanceCalcExpanded)
            {
                UpdateFeedbackWithGeoCircle();
            }
        }

        internal override void UpdateFeedback()
        {
            UpdateFeedbackWithGeoCircle();
        }

        private void UpdateFeedbackWithGeoCircle()
        {
            if (Point1 == null || Distance <= 0.0)
                return;

            CreateCircle(true);
        }

        #endregion

        #region Private Functions

        /// <summary>
        /// Overrides TabBaseViewModel CreateMapElement
        /// </summary>
        internal override Geometry CreateMapElement()
        {
            base.CreateMapElement();
            var geom = CreateCircle(false);
            Reset(false);

            return geom;
        }

        public override bool CanCreateElement
        {
            get
            {
                return (HasPoint1 && Distance > 0.0);
            }
        }

        internal override void Reset(bool toolReset)
        {
            base.Reset(toolReset);
            TravelTime = 0.0;
            TravelRate = 0.0;
        }

        /// <summary>
        /// Create geodetic circle
        /// </summary>
        private Geometry CreateCircle(bool isFeedback)
        {
            if (Point1 == null || double.IsNaN(Distance) || Distance <= 0.0)
            {
                return null;
            }

            var param = new GeodesicEllipseParameter();

            param.Center = new Coordinate2D(Point1);
            param.AxisDirection = 0.0;
            param.LinearUnit = GetLinearUnit(LineDistanceType);
            param.OutGeometryType = GeometryType.Polygon;
            if (isFeedback)
                param.OutGeometryType = GeometryType.Polyline;
            param.SemiAxis1Length = Distance;
            param.SemiAxis2Length = Distance;
            param.VertexCount = VertexCount;

            var geom = GeometryEngine.Instance.GeodesicEllipse(param, MapView.Active.Map.SpatialReference);

            CIMColor color = new CIMRGBColor() { R = 255, B = 0, G = 0, Alpha = 25 };
            if (isFeedback)
            {
                color = ColorFactory.Instance.GreyRGB;
                ClearTempGraphics();
                AddGraphicToMap(Point1, ColorFactory.Instance.GreenRGB, null, true, 5.0);
            }

            // Hold onto the attributes in case user saves graphics to file later
            //CircleAttributes circleAttributes = new CircleAttributes(Point1, Distance, CircleType);
            double dist = 0.0;
            DistanceTypes distunit;
            if (CircleType == CircleFromTypes.Diameter)
                dist = Distance * 2;
            else
                dist = Distance;

            if (IsDistanceCalcExpanded)
            {
                dist = ConvertFromTo(LineDistanceType, RateUnit, Distance);
                distunit = RateUnit;
            }
            else
            {
                distunit = LineDistanceType;
            }

            CircleAttributes circleAttributes = new CircleAttributes() { mapPoint = Point1, distance = dist, circleFromTypes = CircleType, circletype = CircleType.ToString(), centerx = Point1.X, centery = Point1.Y, distanceunit = distunit.ToString() };

            if (isFeedback)
                AddGraphicToMap(geom, color, (ProGraphicAttributes)circleAttributes, IsTempGraphic: isFeedback);
            else
                CreateCircleFeature(geom, circleAttributes);

            return (Geometry)geom;
        }

        #endregion

        public override string GetLayerName()
        {
            return "Circles";
        }

        private async void CreateCircleFeature(Geometry geom, CircleAttributes circleAttributes)
        {
            string message = string.Empty;
            await QueuedTask.Run(async () =>
                message = await AddFeatureToLayer(geom, circleAttributes));

            RaisePropertyChanged(() => HasMapGraphics);

            if (!string.IsNullOrEmpty(message))
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(message,
                    DistanceAndDirectionLibrary.Properties.Resources.ErrorFeatureCreateTitle);
        }

        private async Task<string> AddFeatureToLayer(Geometry geom, CircleAttributes attributes)
        {
            string message = String.Empty;

            if (attributes == null)
            {
                message = "Attributes are Empty"; // For debug does not need to be resource
                return message;
            }

            FeatureClass circleFeatureClass = await GetFeatureClass(addToMapIfNotPresent : true); 
            if (circleFeatureClass == null)
            {
                message = DistanceAndDirectionLibrary.Properties.Resources.ErrorFeatureClassNotFound + this.GetLayerName();
                return message;
            }

            bool creationResult = false;

            FeatureClassDefinition circleDefinition = circleFeatureClass.GetDefinition();

            EditOperation editOperation = new EditOperation();
            editOperation.Name = "Circular Feature Insert";
            editOperation.Callback(context =>
            {
                try
                {
                    RowBuffer rowBuffer = circleFeatureClass.CreateRowBuffer();

                    double distance = attributes.distance;
                    if (IsDistanceCalcExpanded && (CircleType == CircleFromTypes.Diameter))
                        distance *= 2.0;

                    if (circleDefinition.FindField("Distance") >= 0)
                        rowBuffer["Distance"] = distance;     // Double

                    if (circleDefinition.FindField("DistUnit") >= 0)
                        rowBuffer["DistUnit"] = attributes.distanceunit; // Text

                    if (circleDefinition.FindField("DistType") >= 0)
                        rowBuffer["DistType"] = attributes.circletype;   // Text

                    if (circleDefinition.FindField("CenterX") >= 0)
                        rowBuffer["CenterX"] = attributes.centerx;       // Double

                    if (circleDefinition.FindField("CenterY") >= 0)
                        rowBuffer["CenterY"] = attributes.centery;       // Double

                    rowBuffer["Shape"] = GeometryEngine.Instance.Project(geom, circleDefinition.GetSpatialReference());

                    Feature feature = circleFeatureClass.CreateRow(rowBuffer);
                    feature.Store();

                    //To Indicate that the attribute table has to be updated
                    context.Invalidate(feature);
                }
                catch (GeodatabaseException geodatabaseException)
                {
                    message = geodatabaseException.Message;
                }
                catch (Exception ex)
                {
                    message = ex.Message;
                }
            }, circleFeatureClass);

            await QueuedTask.Run(async () =>
            {
                creationResult = await editOperation.ExecuteAsync();
            });

            if (!creationResult)
            {
                message = editOperation.ErrorMessage;
            }

            return message;
        }

    }
}
