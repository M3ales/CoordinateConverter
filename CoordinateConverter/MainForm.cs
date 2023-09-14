﻿using CoordinateConverter.DCS.Aircraft;
using CoordinateConverter.DCS.Communication;
using CoordinateConverter.DCS.Tools;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace CoordinateConverter
{
    /// <summary>
    /// Main application
    /// </summary>
    /// <seealso cref="Form" />
    public partial class MainForm : Form
    {
        
        private readonly Color ERROR_COLOR = Color.Pink;
        private readonly Color DCS_ERROR_COLOR = Color.Yellow;
        private readonly Color DCS_OK_COLOR = Color.Green;

        private CoordinateDataEntry input = null;
        private Bullseye bulls = null;
        private List<CoordinateDataEntry> dataEntries = new List<CoordinateDataEntry>();
        private static readonly System.Globalization.CultureInfo CI = System.Globalization.CultureInfo.InvariantCulture;
        private static readonly Newtonsoft.Json.JsonSerializerSettings jsonSerializerSettings = new Newtonsoft.Json.JsonSerializerSettings()
        {
            Culture = CI,
            Formatting = Newtonsoft.Json.Formatting.Indented,
            TypeNameHandling = Newtonsoft.Json.TypeNameHandling.Objects
        };

        private string oldAltitudeUnit;

        private ToolStripItem selectedScreenMenuItem = null;
        private readonly FormReticle reticleForm = new FormReticle();

        private DCSAircraft selectedAircraft = null;

        /// <summary>
        /// The lock object for the progress bar, so that tmr250 doesn't set value before SendToDCS has set the maximum, causing exceptions during race conditions
        /// </summary>
        private readonly object lockObjProgressBar = new object();

        #region Input

        #region RegexConstants
        /// <summary>
        /// Regex for Latitude. Allows valid numbers 0-90, 0-59, 0-59 and coordinate units, optional decimal part for seconds
        /// </summary>
        private const string REGEX_LL_LAT = @"^(?:[0-8]\d|90)\s*°?\s*[0-5]\d\s*'?\s*[0-5]\d(?:\.\d+)?\s*(?:\""|'')?$";
        /// <summary>
        /// Regex for Longitude. Allows valid numbers 0-180, 0-59, 0-59 and coordinate units, optional decimal part for seconds
        /// </summary>
        private const string REGEX_LL_LON = @"^(?:0\d\d|1[0-7]\d|180)\s*°?\s*[0-5]\d\s*'?\s*[0-5]\d(?:\.\d+)?\s*(?:\""|'')?$";
        /// <summary>
        /// Regex for Latitude. Allows valid numbers 0-90, 0-59, 0-59 and coordinate units, optional decimal part for seconds
        /// </summary>
        private const string REGEX_LL_DECIMAL_LAT = @"^(?:[0-8]\d|90)\s*°?\s*[0-5]\d\s*(?:\.\d+)?'?$";
        /// <summary>
        /// Regex for Longitude. Allows valid numbers 0-180, 0-59, 0-59 and coordinate units, optional decimal part for seconds
        /// </summary>
        private const string REGEX_LL_DECIMAL_LON = @"^(?:0\d\d|1[0-7]\d|180)\s*°?\s*[0-5]\d\s*(?:\.\d+)?'?$";
        #endregion

        #region LL_DecimalSeconds      
        /// <summary>
        /// Checks the LL textboxes and marks them as error if format invalid.
        /// </summary>
        /// <returns>true if valid, otherwise false</returns>
        private bool CheckAndMarkLL_DecimalSeconds()
        {
            bool ok = true;
            // TB Lat
            if (Regex.IsMatch(TB_LL_DecimalSeconds_Latitude.Text, REGEX_LL_LAT))
            {
                TB_LL_DecimalSeconds_Latitude.BackColor = default;
            }
            else
            {
                ok = false;
                TB_LL_DecimalSeconds_Latitude.BackColor = ERROR_COLOR;
            }

            // TB Lon
            if (Regex.IsMatch(TB_LL_DecimalSeconds_Longitude.Text, REGEX_LL_LON))
            {
                TB_LL_DecimalSeconds_Longitude.BackColor = default;
            }
            else
            {
                ok = false;
                TB_LL_DecimalSeconds_Longitude.BackColor = ERROR_COLOR;
            }

            return ok;
        }
        /// <summary>
        /// Calculates the Coordinates from the LL textboxes.
        /// </summary>
        private void CalculatePositionFromLL_DecimalSeconds()
        {
            try
            {
                lbl_Error.Visible = false;

                if (CheckAndMarkLL_DecimalSeconds())
                {
                    double lat = 0.0;
                    double lon = 0.0;
                    // get Lat
                    {
                        string strLat = TB_LL_DecimalSeconds_Latitude.Text; // Lat = N/S
                        strLat = strLat.Replace("°", string.Empty).Replace("'", string.Empty).Replace("\"", string.Empty).Replace(" ", string.Empty).Trim();
                        double deg = int.Parse(strLat.Substring(0, 2), System.Globalization.CultureInfo.InvariantCulture);
                        double min = int.Parse(strLat.Substring(2, 2), System.Globalization.CultureInfo.InvariantCulture);
                        double sec = double.Parse(strLat.Substring(4), System.Globalization.CultureInfo.InvariantCulture);

                        lat = (RB_LL_DecimalSeconds_N.Checked ? 1 : -1) * (deg + (min / 60) + (sec / 3600));
                    }
                    // get Lon
                    {
                        string strLon = TB_LL_DecimalSeconds_Longitude.Text; // Lon = E/W
                        strLon = strLon.Replace("°", string.Empty).Replace("'", string.Empty).Replace("\"", string.Empty).Replace(" ", string.Empty).Trim();
                        double deg = int.Parse(strLon.Substring(0, 3), System.Globalization.CultureInfo.InvariantCulture);
                        double min = int.Parse(strLon.Substring(3, 2), System.Globalization.CultureInfo.InvariantCulture);
                        double sec = double.Parse(strLon.Substring(5), System.Globalization.CultureInfo.InvariantCulture);

                        lon = (RB_LL_DecimalSeconds_E.Checked ? 1 : -1) * (deg + (min / 60) + (sec / 3600));
                    }

                    CoordinateSharp.Coordinate coordinate = new CoordinateSharp.Coordinate(lat, lon);
                    input = new CoordinateDataEntry(dataEntries.Count, coordinate, GetAltitudeInM(), cb_AltitudeIsAGL.Checked, tb_Label.Text);
                    RefreshCoordinates(EUpdateType.OutputOnly);
                }
                else
                {
                    input = null;
                }
            }
            catch (Exception e)
            {
                lbl_Error.Visible = true;
                lbl_Error.Text = e.Message;
            }
        }

        private void TB_LL_DecimalSeconds_Latitude_TextChanged(object sender, EventArgs e)
        {
            CalculatePositionFromLL_DecimalSeconds();
        }

        private void TB_LL_DecimalSeconds_Longitude_TextChanged(object sender, EventArgs e)
        {
            CalculatePositionFromLL_DecimalSeconds();
        }

        private void RB_LL_DecimalSeconds_CheckedChanged(object sender, EventArgs e)
        {
            CalculatePositionFromLL_DecimalSeconds();
            RefreshCoordinates(EUpdateType.CoordinateInput);
        }

        private void TB_LL_DecimalSeconds_KeyPress(object sender, KeyPressEventArgs e)
        {
            char[] validSingleCharacters = { '°', '.', '"', '\'' };
            // Automatically switch focus
            switch (e.KeyChar)
            {
                case 'n':
                    RB_LL_DecimalSeconds_N.Checked = true;
                    TB_LL_DecimalSeconds_Latitude.Focus();
                    break;
                case 's':
                    RB_LL_DecimalSeconds_S.Checked = true;
                    TB_LL_DecimalSeconds_Latitude.Focus();
                    break;
                case 'e':
                    RB_LL_DecimalSeconds_E.Checked = true;
                    TB_LL_DecimalSeconds_Longitude.Focus();
                    break;
                case 'w':
                    RB_LL_DecimalSeconds_W.Checked = true;
                    TB_LL_DecimalSeconds_Longitude.Focus();
                    break;
            }
            // allow numbers or any of the validSingleCharacters as long as they aren't in the text already
            if (
                (e.KeyChar >= '0' && e.KeyChar <= '9') ||
                (e.KeyChar == ' ') ||
                (validSingleCharacters.Contains(e.KeyChar) && !((TextBox)sender).Text.Contains(e.KeyChar)) ||
                (e.KeyChar < 32)
            )
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            }
        }
        #endregion // LL

        #region LL_DecimalMinutes

        /// <summary>
        /// Checks the LL Decimal textboxes and marks them as error if format invalid.
        /// </summary>
        /// <returns>true if valid, otherwise false</returns>
        private bool CheckAndMarkLL_DecimalMinutes()
        {
            bool ok = true;
            // TB Lat
            if (Regex.IsMatch(TB_LL_DecimalMinutes_Latitude.Text, REGEX_LL_DECIMAL_LAT))
            {
                TB_LL_DecimalMinutes_Latitude.BackColor = default;
            }
            else
            {
                ok = false;
                TB_LL_DecimalMinutes_Latitude.BackColor = ERROR_COLOR;
            }
            // TB Lon
            if (Regex.IsMatch(TB_LL_DecimalMinutes_Longitude.Text, REGEX_LL_DECIMAL_LON))
            {
                TB_LL_DecimalMinutes_Longitude.BackColor = default;
            }
            else
            {
                ok = false;
                TB_LL_DecimalMinutes_Longitude.BackColor = ERROR_COLOR;
            }

            return ok;
        }

        /// <summary>
        /// Calculates the coordinates from the ll decimal textboxes.
        /// </summary>
        private void CalculatePositionFromLL_DecimalMinutes()
        {
            try
            {
                lbl_Error.Visible = false;

                if (CheckAndMarkLL_DecimalMinutes())
                {
                    double lat = 0.0;
                    double lon = 0.0;

                    {
                        string strLat = TB_LL_DecimalMinutes_Latitude.Text; // Lat = N/S
                        strLat = strLat.Replace("°", string.Empty).Replace("'", string.Empty).Replace("\"", string.Empty).Replace(" ", string.Empty).Trim();
                        double deg = int.Parse(strLat.Substring(0, 2), System.Globalization.CultureInfo.InvariantCulture);
                        double min = double.Parse(strLat.Substring(2), System.Globalization.CultureInfo.InvariantCulture);

                        lat = (RB_LL_DecimalMinutes_N.Checked ? 1 : -1) * (deg + (min / 60));
                    }

                    {
                        string strLon = TB_LL_DecimalMinutes_Longitude.Text; // Lon = E/W
                        strLon = strLon.Replace("°", string.Empty).Replace("'", string.Empty).Replace("\"", string.Empty).Replace(" ", string.Empty).Trim();
                        double deg = int.Parse(strLon.Substring(0, 3), System.Globalization.CultureInfo.InvariantCulture);
                        double min = double.Parse(strLon.Substring(3), System.Globalization.CultureInfo.InvariantCulture);

                        lon = (RB_LL_DecimalMinutes_E.Checked ? 1 : -1) * (deg + (min / 60));
                    }

                    CoordinateSharp.Coordinate coordinate = new CoordinateSharp.Coordinate(lat, lon);
                    input = new CoordinateDataEntry(dataEntries.Count, coordinate, GetAltitudeInM(), cb_AltitudeIsAGL.Checked, tb_Label.Text);
                    RefreshCoordinates(EUpdateType.OutputOnly);
                }
                else
                {
                    input = null;
                }
            }
            catch (Exception e)
            {
                lbl_Error.Visible = true;
                lbl_Error.Text = e.Message;
            }
        }

        private void TB_LL_DecimalMinutes_Latitude_TextChanged(object sender, EventArgs e)
        {
            CalculatePositionFromLL_DecimalMinutes();
        }

        private void TB_LL_DecimalMinutes_Longitude_TextChanged(object sender, EventArgs e)
        {
            CalculatePositionFromLL_DecimalMinutes();
        }

        private void RB_LL_DecimalMinutes_CheckedChanged(object sender, EventArgs e)
        {
            CalculatePositionFromLL_DecimalMinutes();
            RefreshCoordinates(EUpdateType.CoordinateInput);
        }

        private void TB_LL_DecimalMinutes_KeyPress(object sender, KeyPressEventArgs e)
        {
            char[] validSingleCharacters = { '°', '.', '\'' };
            // Automatically switch focus
            switch (e.KeyChar)
            {
                case 'n':
                    RB_LL_DecimalMinutes_N.Checked = true;
                    TB_LL_DecimalMinutes_Latitude.Focus();
                    break;
                case 's':
                    RB_LL_DecimalMinutes_S.Checked = true;
                    TB_LL_DecimalMinutes_Latitude.Focus();
                    break;
                case 'e':
                    RB_LL_DecimalMinutes_E.Checked = true;
                    TB_LL_DecimalMinutes_Longitude.Focus();
                    break;
                case 'w':
                    RB_LL_DecimalMinutes_W.Checked = true;
                    TB_LL_DecimalMinutes_Longitude.Focus();
                    break;
            }
            // allow numbers or any of the validSingleCharacters as long as they aren't in the text already
            e.Handled = !(
                (e.KeyChar >= '0' && e.KeyChar <= '9') ||
                (e.KeyChar == ' ') ||
                (validSingleCharacters.Contains(e.KeyChar) && !(((TextBox)sender).Text.Contains(e.KeyChar))) ||
                (e.KeyChar < 32)
            );
        }
        #endregion // LLDecimal

        #region UTM/MGRS GRID
        private void TB_UTM_MGRS_LongZone_KeyPress(object sender, KeyPressEventArgs e)
        {
            // only allow numbers and control characters (backspace + delete)
            e.Handled = !((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar < 0x20) || e.KeyChar == 0x7F);
        }

        private void TB_UTM_MGRS_LatZone_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.KeyChar = char.ToUpperInvariant(e.KeyChar);
            e.Handled = !((e.KeyChar >= 'A' && e.KeyChar <= 'Z' && e.KeyChar != 'O' && e.KeyChar != 'I') || (e.KeyChar < 32));
        }
        #endregion

        #region MGRS        
        /// <summary>
        /// Checks the MGRS textboxes and marks them as error if format invalid.
        /// </summary>
        /// <returns>true if valid, otherwise false</returns>
        private bool CheckAndMarkMGRS()
        {
            bool ok = true;
            // Check Latitude GridCoordinate
            string latitudeZone = TB_MGRS_LatZone.Text.ToUpperInvariant();
            if (!(latitudeZone.Length != 1 || latitudeZone[0] == 'I' || latitudeZone[0] == 'O')) // Must be single character which is not I or O
            {
                TB_MGRS_LatZone.BackColor = default;
            }
            else
            {
                ok = false;
                TB_MGRS_LatZone.BackColor = ERROR_COLOR;
            }

            // Check Longitude GridCoordinate
            if (int.TryParse(TB_MGRS_LongZone.Text, out int longitudeZone))
            {
                if (longitudeZone >= 1 && longitudeZone <= 60)
                {
                    TB_MGRS_LongZone.BackColor = default;
                }
                else
                {
                    ok = false;
                    TB_MGRS_LongZone.BackColor = ERROR_COLOR;
                }
            }
            else
            {
                ok = false;
                TB_MGRS_LongZone.BackColor = ERROR_COLOR;
            }

            // Check Digraph
            string digraph = TB_MGRS_Digraph.Text;
            if (digraph.Length == 2 &&
                digraph[0] >= 'A' && digraph[0] <= 'Z' && digraph[0] != 'I' && digraph[0] != 'O' &&
                digraph[1] >= 'A' && digraph[1] <= 'V' && digraph[1] != 'I' && digraph[1] != 'O')
            {
                TB_MGRS_Digraph.BackColor = default;
            }
            else
            {
                ok = false;
                TB_MGRS_Digraph.BackColor = ERROR_COLOR;
            }
            // Check Fraction
            string strFraction = TB_MGRS_Fraction.Text.Replace(" ", "");
            if (strFraction.Length % 2 != 0)
            {
                ok = false;
                TB_MGRS_Fraction.BackColor = ERROR_COLOR;
            }
            else
            {
                if (strFraction.Length == 0)
                {
                    TB_MGRS_Fraction.BackColor = default;
                }
                else
                {
                    string strEasting = strFraction.Substring(0, strFraction.Length / 2).PadRight(5, '0');
                    string strNorthing = strFraction.Substring(strFraction.Length / 2).PadRight(5, '0');
                    // Check Easting
                    if (double.TryParse(strEasting, out _))
                    {
                        TB_MGRS_Fraction.BackColor = default;
                    }
                    else
                    {
                        ok = false;
                        TB_MGRS_Fraction.BackColor = ERROR_COLOR;
                    }

                    // Check Northing
                    if (double.TryParse(strNorthing, out _))
                    {
                        TB_MGRS_Fraction.BackColor = default;
                    }
                    else
                    {
                        ok = false;
                        TB_MGRS_Fraction.BackColor = ERROR_COLOR;
                    }
                }
            }

            return ok;
        }

        /// <summary>
        /// Calculates the coordinates from the MGRS textboxes.
        /// </summary>
        private void CalculatePositionFromMGRS()
        {
            try
            {
                lbl_Error.Visible = false;

                if (CheckAndMarkMGRS())
                {
                    string latitudeZone = TB_MGRS_LatZone.Text.ToUpperInvariant();
                    int longitudeZone = int.Parse(TB_MGRS_LongZone.Text);
                    string digraph = TB_MGRS_Digraph.Text.ToUpperInvariant();
                    string fractionText = TB_MGRS_Fraction.Text.Replace(" ", "");
                    double easting = double.Parse(fractionText.Substring(0, fractionText.Length / 2).PadRight(5, '0'));
                    double northing = double.Parse(fractionText.Substring(fractionText.Length / 2).PadRight(5, '0'));

                    CoordinateSharp.MilitaryGridReferenceSystem mgrs = new CoordinateSharp.MilitaryGridReferenceSystem(latz: latitudeZone, longz: longitudeZone, d: digraph, e: easting, n: northing);
                    CoordinateSharp.Coordinate coordinate = CoordinateSharp.MilitaryGridReferenceSystem.MGRStoLatLong(mgrs);
                    input = new CoordinateDataEntry(dataEntries.Count, coordinate, GetAltitudeInM(), cb_AltitudeIsAGL.Checked, tb_Label.Text);
                    RefreshCoordinates(EUpdateType.OutputOnly);
                }
                else
                {
                    input = null;
                }
            }
            catch (Exception e)
            {
                lbl_Error.Visible = true;
                lbl_Error.Text = e.Message;
            }
        }

        private void InputMGRSChanged(object objSender, EventArgs e)
        {
            TextBox sender = objSender as TextBox;

            // If the text is complete, switch to the next box
            if (sender.Text.Length == sender.MaxLength)
            {
                TextBox nextBox = null;

                if (sender.Name == TB_MGRS_LongZone.Name)
                {
                    nextBox = TB_MGRS_LatZone;
                }
                else if (sender.Name == TB_MGRS_LatZone.Name)
                {
                    nextBox = TB_MGRS_Digraph;
                }
                else if (sender.Name == TB_MGRS_Digraph.Name)
                {
                    nextBox = TB_MGRS_Fraction;
                }
                else if (sender.Name == TB_MGRS_Fraction.Name)
                {
                    // Nothing to do in this case
                }

                nextBox?.Focus();
            }

            CalculatePositionFromMGRS();
        }

        private void TB_MGRS_Fraction_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = !((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar < 32));
        }

        private void TB_MGRS_Digraph_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.KeyChar = char.ToUpperInvariant(e.KeyChar);
            e.Handled = !((e.KeyChar >= 'A' && e.KeyChar <= 'Z') || (e.KeyChar < 0x20));
        }

        private void TB_MGRS_Fraction_Enter(object sender, EventArgs e)
        {
            // remove space in the middle
            TB_MGRS_Fraction.Text = TB_MGRS_Fraction.Text.Replace(" ", "");
            TB_MGRS_Fraction.MaxLength = 10;
            // Do other input focus stuff
            TB_Input_Enter(sender, e);
        }

        private void TB_MGRS_Fraction_Leave(object sender, EventArgs e)
        {
            // add space in the middle
            RefreshCoordinates(EUpdateType.CoordinateInput);

            TB_MGRS_Fraction.MaxLength = 11;
            if (TB_MGRS_Fraction.Text.Length > 0 && TB_MGRS_Fraction.Text.Length % 2 == 0)
            {
                TB_MGRS_Fraction.Text = TB_MGRS_Fraction.Text.Insert(TB_MGRS_Fraction.Text.Length / 2, " ");
            }
            TB_MGRS_Fraction.Text = TB_MGRS_Fraction.Text;
        }

        #endregion // MGRS

        #region UTM
        private bool CheckAndMarkUTM()
        {
            bool ok = true;

            // Check Latitude GridCoordinate
            string latitudeZone = TB_UTM_LatZone.Text.ToUpperInvariant();
            if (!(latitudeZone.Length != 1 || latitudeZone[0] == 'I' || latitudeZone[0] == 'O')) // Must be single character which is not I or O
            {
                TB_UTM_LatZone.BackColor = default;
            }
            else
            {
                ok = false;
                TB_UTM_LatZone.BackColor = ERROR_COLOR;
            }

            // Check Longitude GridCoordinate
            if (int.TryParse(TB_UTM_LongZone.Text, out int longitudeZone))
            {
                if (longitudeZone >= 1 && longitudeZone <= 60)
                {
                    TB_UTM_LongZone.BackColor = default;
                }
                else
                {
                    ok = false;
                    TB_UTM_LongZone.BackColor = ERROR_COLOR;
                }
            }
            else
            {
                ok = false;
                TB_UTM_LongZone.BackColor = ERROR_COLOR;
            }
            // Check Easting
            if (double.TryParse(TB_UTM_Easting.Text, System.Globalization.NumberStyles.AllowDecimalPoint, CI, out _))
            {
                TB_UTM_Easting.BackColor = default;
            }
            else
            {
                ok = false;
                TB_UTM_Easting.BackColor = ERROR_COLOR;
            }

            // Check Northing
            if (double.TryParse(TB_UTM_Northing.Text, System.Globalization.NumberStyles.AllowDecimalPoint, CI, out _))
            {
                TB_UTM_Northing.BackColor = default;
            }
            else
            {
                ok = false;
                TB_UTM_Northing.BackColor = ERROR_COLOR;
            }

            return ok;
        }

        private void CalculatePositionFromUTM()
        {
            try
            {
                lbl_Error.Visible = false;

                if (CheckAndMarkUTM())
                {
                    string latitudeZone = TB_UTM_LatZone.Text.ToUpperInvariant();
                    int longitudeZone = int.Parse(TB_UTM_LongZone.Text, CI);
                    double easting = double.Parse(TB_UTM_Easting.Text, System.Globalization.NumberStyles.AllowDecimalPoint, CI);
                    double northing = double.Parse(TB_UTM_Northing.Text, System.Globalization.NumberStyles.AllowDecimalPoint, CI);

                    CoordinateSharp.UniversalTransverseMercator utm = new CoordinateSharp.UniversalTransverseMercator(latz: latitudeZone, longz: longitudeZone, est: easting, nrt: northing);
                    CoordinateSharp.Coordinate coordinate = CoordinateSharp.UniversalTransverseMercator.ConvertUTMtoLatLong(utm);
                    input = new CoordinateDataEntry(dataEntries.Count, coordinate, GetAltitudeInM(), cb_AltitudeIsAGL.Checked, tb_Label.Text);
                    RefreshCoordinates(EUpdateType.OutputOnly);
                }
                else
                {
                    input = null;
                }
            }
            catch (Exception e)
            {
                lbl_Error.Visible = true;
                lbl_Error.Text = e.Message;
            }
        }

        private void RB_UTM_Northing_Easting_KeyPress(object sender, KeyPressEventArgs e)
        {
            bool tbHasDecimalPoint = ((TextBox)sender).Text.Contains(".");
            e.Handled = !(
                (e.KeyChar >= '0' && e.KeyChar <= '9') ||
                (e.KeyChar == '.' && !tbHasDecimalPoint) ||
                (e.KeyChar < 32)
            );
        }

        private void InputUTMChanged(object objSender, EventArgs e)
        {
            TextBox sender = objSender as TextBox;
            if (sender.Text.Length == sender.MaxLength)
            {
                TextBox nextBox = null;
                if (sender.Name == TB_UTM_LongZone.Name)
                {
                    nextBox = TB_UTM_LatZone;
                }
                else if (sender.Name == TB_UTM_LatZone.Name)
                {
                    nextBox = TB_UTM_Easting;
                }
                else if (sender.Name == TB_UTM_Easting.Name)
                {
                    // nothing to do here
                }
                else if (sender.Name == TB_UTM_Northing.Name)
                {
                    // nothing to do here
                }

                nextBox?.Focus();
            }
            CalculatePositionFromUTM();
        }

        #endregion

        #region BULLS
        private bool CheckAndMarkBulls()
        {
            if (bulls == null)
            {
                throw new Exception("Bullseye not set.");
            }
            else
            {
                bool ok = true;
                // check bearing
                if (double.TryParse(tb_Bullseye_Bearing.Text, System.Globalization.NumberStyles.AllowDecimalPoint, CI, out _))
                {
                    tb_Bullseye_Bearing.BackColor = default;
                }
                else
                {
                    ok = false;
                    tb_Bullseye_Bearing.BackColor = ERROR_COLOR;
                }
                // check range
                if (double.TryParse(tb_Bullseye_Range.Text, System.Globalization.NumberStyles.AllowDecimalPoint, CI, out _))
                {
                    tb_Bullseye_Range.BackColor = default;
                }
                else
                {
                    ok = false;
                    tb_Bullseye_Range.BackColor = ERROR_COLOR;
                }

                return ok;
            }
        }

        private void CalculatePositionFromBullseye()
        {
            try
            {
                lbl_Error.Visible = false;

                if (CheckAndMarkBulls())
                {
                    double bearing = double.Parse(tb_Bullseye_Bearing.Text, System.Globalization.NumberStyles.AllowDecimalPoint, CI);
                    double range = double.Parse(tb_Bullseye_Range.Text, System.Globalization.NumberStyles.AllowDecimalPoint, CI);

                    CoordinateSharp.Coordinate coordinate = bulls.GetOffsetPosition(new BRA(bearing: bearing, range: range));
                    input = new CoordinateDataEntry(dataEntries.Count, coordinate, GetAltitudeInM(), cb_AltitudeIsAGL.Checked, tb_Label.Text);
                    RefreshCoordinates(EUpdateType.OutputOnly);
                }
                else
                {
                    input = null;
                }
            }
            catch (Exception e)
            {
                lbl_Error.Visible = true;
                lbl_Error.Text = e.Message;
            }
        }

        private void Tb_Bullseye_Bearing_TextChanged(object sender, EventArgs e)
        {
            CalculatePositionFromBullseye();
        }

        private void Tb_Bullseye_Bearing_KeyPress(object sender, KeyPressEventArgs e)
        {
            bool tbHasDecimalPoint = ((TextBox)sender).Text.Contains(".");
            // allow any number and a single decimal point
            e.Handled = !(
                (e.KeyChar >= '0' && e.KeyChar <= '9') ||
                (!tbHasDecimalPoint && e.KeyChar == '.') ||
                (e.KeyChar < 0x20) ||
                (e.KeyChar == 0x7F)
            );
        }

        private void Tb_Bullseye_Range_TextChanged(object sender, EventArgs e)
        {
            CalculatePositionFromBullseye();
        }

        private void Tb_Bullseye_Range_KeyPress(object sender, KeyPressEventArgs e)
        {
            bool tbHasDecimalPoint = ((TextBox)sender).Text.Contains(".");
            // allow any number and a single decimal point
            e.Handled = !(
                (e.KeyChar >= '0' && e.KeyChar <= '9') ||
                (!tbHasDecimalPoint && e.KeyChar == '.') ||
                (e.KeyChar < 0x20) ||
                (e.KeyChar == 0x7F)
            );
        }

        private void Btn_SetBE_Click(object sender, EventArgs e)
        {
            lbl_Error.Visible = false;
            if (input == null)
            {
                lbl_Error.Text = "Input invalid";
                lbl_Error.Visible = true;
                return;
            }
            bulls = new Bullseye(input.Coordinate);

            RefreshBullseyeFormat();

            RefreshCoordinates(EUpdateType.CoordinateInput);
            RefreshDataGrid();
        }

        private void RefreshBullseyeFormat()
        {
            if (bulls == null)
            {
                lbl_BEPosition.Text = "Not set";
                return;
            }

            if (GetSelectedFormat() == ECoordinateFormat.Bullseye)
            {
                lbl_BEPosition.Text = new CoordinateDataEntry(0, bulls.GetBullseye()).GetCoordinateStrLLDecSec();
            }
            else
            {
                lbl_BEPosition.Text = GetEntryCoordinateStr(new CoordinateDataEntry(0, bulls.GetBullseye()));
            }
        }

        #endregion

        #region MiscInput

        private void Tb_Altitude_KeyPress(object sender, KeyPressEventArgs e)
        {
            // only allow numbers and control characters (backspace + delete)
            e.Handled = !((e.KeyChar >= '0' && e.KeyChar <= '9') || e.KeyChar < 0x20 || e.KeyChar == 0x7F);
        }

        private void Tb_Label_KeyPress(object sender, KeyPressEventArgs e)
        {
            // only allow numbers, letters, space and control characters (backspace + delete)
            e.Handled = !(
                (e.KeyChar >= '0' && e.KeyChar <= '9') ||
                (e.KeyChar >= 'a' && e.KeyChar <= 'z') ||
                (e.KeyChar >= 'A' && e.KeyChar <= 'Z') ||
                e.KeyChar <= 0x20 ||
                e.KeyChar == 0x7F
            );
        }
        private void Tb_Altitude_TextChanged(object sender, EventArgs e)
        {
            if (tb_Altitude.Text == string.Empty || !int.TryParse(tb_Altitude.Text, out int altitude))
            {
                tb_Altitude.Text = "0";
                altitude = 0;
            }

            if (input == null)
            {
                return;
            }

            if (cb_AltitudeUnit.Text == "ft")
            {
                input.AltitudeInFt = altitude;
            }
            else if (cb_AltitudeUnit.Text == "m")
            {
                input.AltitudeInM = altitude;
            }
            else
            {
                throw new ArgumentException("Altitude unit not valid");
            }
        }

        /// <summary>
        /// Gets the altitude in m from the input text box.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ArgumentException">Altitude unit not implemented.</exception>
        private double GetAltitudeInM()
        {
            lbl_Error.Visible = false;

            if (!int.TryParse(tb_Altitude.Text, out int altitude))
            {
                lbl_Error.Visible = true;
                lbl_Error.Text = "Could not parse altitude as a whole number.";
                return 0.0;
            }

            if (cb_AltitudeUnit.Text == "m")
            {
                return altitude;
            }
            if (cb_AltitudeUnit.Text == "ft")
            {
                return altitude / CoordinateDataEntry.FT_PER_M;
            }
            throw new ArgumentException("Altitude unit not valid.");
        }

        private void Cb_AltitudeUnit_SelectedIndexChanged(object sender, EventArgs e)
        {
            lbl_Error.Visible = false;

            if (input != null)
            {
                if (cb_AltitudeUnit.Text == "ft")
                {
                    tb_Altitude.Text = ((int)Math.Round(input.AltitudeInFt)).ToString();
                }
                else if (cb_AltitudeUnit.Text == "m")
                {
                    tb_Altitude.Text = ((int)Math.Round(input.AltitudeInM)).ToString();
                }
                else
                {
                    throw new ArgumentException("Altitude unit not valid");
                }
            }
            else
            {
                if (!int.TryParse(tb_Altitude.Text, out int oldAltitudeValue))
                {
                    lbl_Error.Visible = true;
                    lbl_Error.Text = "Could not parse altitude as a whole number.";
                    return;
                }

                if (cb_AltitudeUnit.Text == "ft" && oldAltitudeUnit == "m")
                {
                    tb_Altitude.Text = ((int)Math.Round(oldAltitudeValue * CoordinateDataEntry.FT_PER_M)).ToString();
                }
                else if (cb_AltitudeUnit.Text == "m")
                {
                    tb_Altitude.Text = ((int)Math.Round(oldAltitudeValue / CoordinateDataEntry.FT_PER_M)).ToString();
                }
                else
                {
                    throw new ArgumentException("Altitude unit not valid");
                }
            }

            oldAltitudeUnit = cb_AltitudeUnit.Text;
            RefreshDataGrid();
        }

        private void Tb_Label_TextChanged(object sender, EventArgs e)
        {
            if (input != null)
            {
                input.Name = tb_Label.Text;
            }
        }

        private void TB_Input_Enter(object sender, EventArgs e)
        {
            (sender as TextBox).SelectAll();
        }

        private void TB_Input_Leave(object sender, EventArgs e)
        {
            RefreshCoordinates(EUpdateType.CoordinateInput);
        }

        #endregion

        #endregion

        #region Output
        private enum EUpdateType
        {
            OutputOnly,
            CoordinateInput,
            Everything
        }

        private void RefreshCoordinates(EUpdateType updateType)
        {
            if (input != null)
            {
                tb_Out_LL_DecimalSeconds.Text = input.GetCoordinateStrLLDecSec();
                tb_Out_LL_DecimalMinutes.Text = input.GetCoordinateStrLLDecMin();
                tb_Out_MGRS.Text = input.GetCoordinateStrMGRS((int)nud_MGRS_Precision.Value);
                tb_Out_UTM.Text = input.GetCoordinateStrUTM();
                tb_Out_Bullseye.Text = input.GetCoordinateStrBullseye(bulls);

                if (updateType == EUpdateType.OutputOnly)
                {
                    return;
                }

                // altitude
                tb_Altitude.TextChanged -= Tb_Altitude_TextChanged;
                cb_AltitudeIsAGL.CheckedChanged -= Cb_AltitudeIsAGL_CheckedChanged;

                cb_AltitudeIsAGL.Checked = input.AltitudeIsAGL;
                tb_Altitude.Text = Math.Round(cb_AltitudeUnit.Text == "ft" ? input.AltitudeInFt : input.AltitudeInM).ToString();

                tb_Label.Text = input.Name;

                tb_Altitude.TextChanged += Tb_Altitude_TextChanged;
                cb_AltitudeIsAGL.CheckedChanged += Cb_AltitudeIsAGL_CheckedChanged;

                // point type & point option
                if (updateType == EUpdateType.Everything)
                {
                    cb_PointType.SelectedIndexChanged -= Cb_pointType_SelectedIndexChanged;
                    cb_PointOption.SelectedIndexChanged -= Cb_PointOption_SelectedIndexChanged;

                    if (selectedAircraft == null || !input.AircraftSpecificData.ContainsKey(selectedAircraft.GetType()))
                    {
                        cb_PointType.SelectedIndex = 0;
                        Cb_pointType_SelectedIndexChanged(cb_PointType, null);
                        if (cb_PointOption.Items.Count > 0)
                        {
                            cb_PointOption.SelectedIndex = 0;
                            Cb_PointOption_SelectedIndexChanged(cb_PointOption, null);
                        }
                    }
                    else if (selectedAircraft.GetType() == typeof(AH64))
                    {
                        AH64SpecificData extraData = input.AircraftSpecificData[selectedAircraft.GetType()] as AH64SpecificData;
                        cb_PointType.SelectedIndex = ComboItem<AH64.EPointType>.FindValue(cb_PointType, extraData.PointType) ?? 0;
                        // select the correct point type and option
                        Cb_pointType_SelectedIndexChanged(cb_PointType, null); // needed to repopulate the point option combo box
                        cb_PointOption.SelectedIndex = ComboItem<AH64.EPointIdent>.FindValue(cb_PointOption, extraData.Ident) ?? 0;
                        Cb_PointOption_SelectedIndexChanged(cb_PointOption, null);
                    }
                    else if (selectedAircraft.GetType() == typeof(F18C))
                    {
                        cb_PointType.SelectedIndex = 0;
                        if (!(input.AircraftSpecificData[selectedAircraft.GetType()] is F18CSpecificData extraData) || extraData.WeaponType == null)
                        {
                            Cb_pointType_SelectedIndexChanged(cb_PointType, null);
                            Cb_PointOption_SelectedIndexChanged(cb_PointOption, null);
                        }
                        else if (extraData.WeaponType != null)
                        {
                            // not a waypoint, waypoint handled by default above (extraData == null)
                            if (!extraData.PreplanPointIdx.HasValue)
                            {
                                // SLAM-ER STP
                                cb_PointType.SelectedIndex = ComboItem<string>.FindValue(cb_PointType, F18C.SLAMER_STP_STR) ?? 0;
                                Cb_pointType_SelectedIndexChanged(cb_PointType, null);
                                Cb_PointOption_SelectedIndexChanged(cb_PointOption, null);
                            }
                            else
                            {
                                // PP
                                cb_PointType.SelectedIndex = ComboItem<string>.FindValue(cb_PointType, F18C.GetPointTypePPStrForWeaponType(extraData.WeaponType.Value)) ?? 0;
                                Cb_pointType_SelectedIndexChanged(cb_PointType, null);
                                cb_PointOption.SelectedIndex = ComboItem<string>.FindValue(cb_PointOption, string.Format("PP {0} - {1}", extraData.PreplanPointIdx.Value, extraData.StationSetting.ToString())) ?? 0;
                                Cb_PointOption_SelectedIndexChanged(cb_PointOption, null);
                            }
                        }
                    }
                    else if (selectedAircraft.GetType() == typeof(KA50))
                    {
                        KA50SpecificData extraData = input.AircraftSpecificData[selectedAircraft.GetType()] as KA50SpecificData;
                        cb_PointType.SelectedIndex = ComboItem<KA50.EPointType>.FindValue(cb_PointType, extraData.PointType) ?? 0;
                        Cb_PointOption_SelectedIndexChanged(cb_PointOption, null);
                    }
                    else if (selectedAircraft.GetType() == typeof(JF17))
                    {
                        JF17SpecificData extraData = input.AircraftSpecificData[selectedAircraft.GetType()] as JF17SpecificData;
                        cb_PointType.SelectedIndex = ComboItem<JF17.EPointType>.FindValue(cb_PointType, extraData.PointType) ?? 0;
                        Cb_PointOption_SelectedIndexChanged(cb_PointOption, null);
                    }

                    cb_PointType.SelectedIndexChanged += Cb_pointType_SelectedIndexChanged;
                    cb_PointOption.SelectedIndexChanged += Cb_PointOption_SelectedIndexChanged;
                }
                else
                {
                    // Update point type
                    int pointOptionIdx = cb_PointOption.SelectedIndex;
                    Cb_pointType_SelectedIndexChanged(cb_PointType, null);
                    // update point option
                    cb_PointOption.SelectedIndex = pointOptionIdx;
                }
                
                // coordinates

                CoordinateSharp.CoordinatePart lat = input.Coordinate.Latitude;
                CoordinateSharp.CoordinatePart lon = input.Coordinate.Longitude;
                CoordinateSharp.UniversalTransverseMercator utm = input.Coordinate.UTM;

                // LL
                TB_LL_DecimalSeconds_Latitude.TextChanged -= TB_LL_DecimalSeconds_Latitude_TextChanged;
                TB_LL_DecimalSeconds_Latitude.Text = lat.Degrees.ToString(CI).PadLeft(2, '0') + "°" + lat.Minutes.ToString(CI).PadLeft(2, '0') + "'" + Math.Round(lat.Seconds, 2).ToString(CI).PadLeft(2, '0') + "\"";
                TB_LL_DecimalSeconds_Latitude.TextChanged += TB_LL_DecimalSeconds_Latitude_TextChanged;

                TB_LL_DecimalSeconds_Longitude.TextChanged -= TB_LL_DecimalSeconds_Longitude_TextChanged;
                TB_LL_DecimalSeconds_Longitude.Text = lon.Degrees.ToString(CI).PadLeft(3, '0') + "°" + lon.Minutes.ToString(CI).PadLeft(2, '0') + "'" + Math.Round(lon.Seconds, 2).ToString(CI).PadLeft(2, '0') + "\"";
                TB_LL_DecimalSeconds_Longitude.TextChanged += TB_LL_DecimalSeconds_Longitude_TextChanged;

                List<Control> controls = new List<Control>() { RB_LL_DecimalSeconds_N, RB_LL_DecimalSeconds_S, RB_LL_DecimalSeconds_E, RB_LL_DecimalSeconds_W };
                foreach (RadioButton rb in controls.Cast<RadioButton>())
                {
                    rb.CheckedChanged -= RB_LL_DecimalSeconds_CheckedChanged;
                }
                RB_LL_DecimalSeconds_N.Checked = lat.Position == CoordinateSharp.CoordinatesPosition.N;
                RB_LL_DecimalSeconds_S.Checked = lat.Position == CoordinateSharp.CoordinatesPosition.S;
                RB_LL_DecimalSeconds_E.Checked = lon.Position == CoordinateSharp.CoordinatesPosition.E;
                RB_LL_DecimalSeconds_W.Checked = lon.Position == CoordinateSharp.CoordinatesPosition.W;
                foreach (RadioButton rb in controls.Cast<RadioButton>())
                {
                    rb.CheckedChanged += RB_LL_DecimalSeconds_CheckedChanged;
                }

                // LL Dec
                TB_LL_DecimalMinutes_Latitude.TextChanged -= TB_LL_DecimalMinutes_Latitude_TextChanged;
                TB_LL_DecimalMinutes_Latitude.Text = lat.Degrees.ToString(CI).PadLeft(2, '0') + "°" + Math.Round(lat.DecimalMinute, 4).ToString(CI).PadLeft(2, '0');
                TB_LL_DecimalMinutes_Latitude.TextChanged += TB_LL_DecimalMinutes_Latitude_TextChanged;

                TB_LL_DecimalMinutes_Longitude.TextChanged -= TB_LL_DecimalMinutes_Longitude_TextChanged;
                TB_LL_DecimalMinutes_Longitude.Text = lon.Degrees.ToString(CI).PadLeft(3, '0') + "°" + Math.Round(lon.DecimalMinute, 4).ToString(CI).PadLeft(2, '0');
                TB_LL_DecimalMinutes_Longitude.TextChanged += TB_LL_DecimalMinutes_Longitude_TextChanged;

                controls = new List<Control>() { RB_LL_DecimalMinutes_N, RB_LL_DecimalMinutes_S, RB_LL_DecimalMinutes_E, RB_LL_DecimalMinutes_W };
                foreach (RadioButton rb in controls.Cast<RadioButton>())
                {
                    rb.CheckedChanged -= RB_LL_DecimalMinutes_CheckedChanged;
                }
                RB_LL_DecimalMinutes_N.Checked = lat.Position == CoordinateSharp.CoordinatesPosition.N;
                RB_LL_DecimalMinutes_S.Checked = lat.Position == CoordinateSharp.CoordinatesPosition.S;
                RB_LL_DecimalMinutes_E.Checked = lon.Position == CoordinateSharp.CoordinatesPosition.E;
                RB_LL_DecimalMinutes_W.Checked = lon.Position == CoordinateSharp.CoordinatesPosition.W;
                foreach (RadioButton rb in controls.Cast<RadioButton>())
                {
                    rb.CheckedChanged += RB_LL_DecimalMinutes_CheckedChanged;
                }

                // MGRS
                controls = new List<Control>() { TB_MGRS_LongZone, TB_MGRS_LatZone, TB_MGRS_Digraph, TB_MGRS_Fraction };
                foreach (TextBox tb in controls.Cast<TextBox>())
                {
                    tb.TextChanged -= InputMGRSChanged;
                }
                string mgrsText = input.GetCoordinateStrMGRS();
                TB_MGRS_LongZone.Text = mgrsText.Substring(0, 2);
                TB_MGRS_LatZone.Text = mgrsText.Substring(2, 1); // one space after this
                TB_MGRS_Digraph.Text = mgrsText.Substring(4, 2); // one space after this
                TB_MGRS_Fraction.Text = mgrsText.Substring(7).Remove(5, 1); // remove center space
                foreach (TextBox tb in controls.Cast<TextBox>())
                {
                    tb.TextChanged += InputMGRSChanged;
                }

                // UTM
                controls = new List<Control>() { TB_UTM_LongZone, TB_UTM_LatZone, TB_UTM_Easting, TB_UTM_Northing };
                foreach (TextBox tb in controls.Cast<TextBox>())
                {
                    tb.TextChanged -= InputUTMChanged;
                }
                TB_UTM_LongZone.Text = utm.LongZone.ToString().PadLeft(2, '0');
                TB_UTM_LatZone.Text = utm.LatZone;
                TB_UTM_Easting.Text = Math.Round(utm.Easting, 3).ToString(CI);
                TB_UTM_Northing.Text = Math.Round(utm.Northing, 3).ToString(CI);
                foreach (TextBox tb in controls.Cast<TextBox>())
                {
                    tb.TextChanged += InputUTMChanged;
                }

                if (bulls != null)
                {
                    BRA bra = bulls.GetBRA(input.Coordinate);

                    tb_Bullseye_Bearing.TextChanged -= Tb_Bullseye_Bearing_TextChanged;
                    tb_Bullseye_Bearing.Text = Math.Round(bra.Bearing, 1).ToString(CI);
                    tb_Bullseye_Bearing.TextChanged += Tb_Bullseye_Bearing_TextChanged;

                    tb_Bullseye_Range.TextChanged -= Tb_Bullseye_Range_TextChanged;
                    tb_Bullseye_Range.Text = Math.Round(bra.Range, 2).ToString(CI);
                    tb_Bullseye_Range.TextChanged += Tb_Bullseye_Range_TextChanged;
                }
            }
        }

        private void Nud_MGRS_Precision_ValueChanged(object sender, EventArgs e)
        {
            RefreshCoordinates(EUpdateType.OutputOnly);
            if (GetSelectedFormat() == ECoordinateFormat.MGRS)
            {
                RefreshDataGrid();
                RefreshBullseyeFormat();
            }
        }

        #region GridView

        private void RefreshDataGrid()
        {
            var result = dataEntries.Select(
                entry => new {
                    ID = entry.Id,
                    Name = entry.GetUserFriendlyString(selectedAircraft?.GetType()),
                    CoordinateStr = GetEntryCoordinateStr(entry),
                    Altitude = entry.GetAltitudeString(cb_AltitudeUnit.Text == "ft"),
                    XFER = entry.XFer
                }
            ).OrderBy(x => x.ID).ToList();

            dgv_CoordinateList.DataSource = result;

            // Deselect all cells and all rows
            foreach (DataGridViewRow row in dgv_CoordinateList.Rows)
            {
                foreach (DataGridViewCell cell in row.Cells)
                {
                    cell.Selected = false;
                }
                row.Selected = false;
            }
        }

        private void Dgv_CoordinateList_KeyDown(object objSender, KeyEventArgs e)
        {
            DataGridView sender = objSender as DataGridView;
            var selectedRowIds = GetSelectedRowIndices();
            if (selectedRowIds.Count == 0)
            {
                return;
            }
            if (e.KeyData == Keys.Delete)
            {
                // Remove them all
                foreach (int rowIdx in selectedRowIds.OrderBy(x => -x))
                {
                    dataEntries.RemoveAt(rowIdx);
                }
                // Update the grid
                RefreshDataGrid();
            }
            else if (e.KeyCode == Keys.Space)
            {
                // Check if they are all on or if some or all are off
                bool allAreOn = true;
                foreach (int rowIdx in selectedRowIds)
                {
                    DataGridViewRow row = sender.Rows[rowIdx];
                    DataGridViewCheckBoxCell cell = row.Cells[5] as DataGridViewCheckBoxCell;
                    if (!(cell.Value as bool? ?? false))
                    {
                        allAreOn = false;
                        break;
                    }
                }

                // Set them all to on/off depending on the result
                foreach (int rowIdx in selectedRowIds)
                {
                    dataEntries[rowIdx].XFer = !allAreOn;
                }

                // Update the grid
                ResetIDs();
                RefreshDataGrid();

                // Reselect all the rows
                foreach (int rowIdx in selectedRowIds)
                {
                    dgv_CoordinateList.Rows[rowIdx].Selected = true;
                }
            }
        }

        private void Dgv_CoordinateList_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                input = dataEntries.ElementAt(e.RowIndex).Clone(dataEntries.Count);
                RefreshCoordinates(EUpdateType.Everything);
            }
        }

        private void Dgv_CoordinateList_CellContentClick(object objSender, DataGridViewCellEventArgs e)
        {
            const int DELETE_BUTTON_COLID = 0;
            const int XFER_CB_COLID = 5;

            DataGridView sender = objSender as DataGridView;

            if (sender.Columns[e.ColumnIndex] is DataGridViewButtonColumn && e.RowIndex >= 0)
            {
                // A button cell was clicked
                if (e.ColumnIndex == DELETE_BUTTON_COLID) // delete
                {
                    dataEntries.RemoveAt(e.RowIndex);
                    ResetIDs();
                    RefreshDataGrid();
                    return;
                }
            }
            if (sender.Columns[e.ColumnIndex] is DataGridViewButtonColumn && e.RowIndex < 0)
            {
                // a button column was clicked
                if (e.ColumnIndex == DELETE_BUTTON_COLID)
                {
                    DialogResult answer = MessageBox.Show("Delete all points?", "Delete?", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (answer != DialogResult.Yes)
                    {
                        return;
                    }
                    dataEntries.Clear();
                    RefreshDataGrid();
                    return;
                }
            }
            if (sender.Columns[e.ColumnIndex] is DataGridViewCheckBoxColumn && e.RowIndex >= 0)
            {
                // a checkbox was clicked
                if (e.ColumnIndex == XFER_CB_COLID)
                {
                    DataGridViewCheckBoxCell cell = sender.Rows[e.RowIndex].Cells[e.ColumnIndex] as DataGridViewCheckBoxCell;
                    dataEntries[e.RowIndex].XFer = !(cell.Value as bool? ?? false); // invert current selection
                    RefreshDataGrid();
                    return;
                }
            }
            if (sender.Columns[e.ColumnIndex] is DataGridViewCheckBoxColumn && e.RowIndex < 0)
            {
                // a checkbox header was clicked
                if (e.ColumnIndex == XFER_CB_COLID)
                {
                    bool allAreOn = dataEntries.Find(elem => !(elem.XFer)) == null;

                    foreach (CoordinateDataEntry entry in dataEntries)
                    {
                        entry.XFer = !allAreOn;
                    }

                    RefreshDataGrid();
                    return;
                }
            }
            return;
        }

        private void ResetIDs()
        {
            int currentCount = dataEntries.Count;
            for (int idx = 0; idx < currentCount; idx++)
            {
                CoordinateDataEntry entry = dataEntries.ElementAt(idx).Clone(idx);
                dataEntries.Add(entry);
            }
            dataEntries.RemoveRange(0, currentCount);
        }

        enum ECoordinateFormat
        {
            LL_DMSs,
            LL_DMm,
            LL_Dd,
            MGRS,
            UTM,
            Bullseye
        }

        private ECoordinateFormat GetSelectedFormat()
        {
            if (rb_Format_LL_DecimalSeconds.Checked)
            {
                return ECoordinateFormat.LL_DMSs;
            }
            if (rb_Format_LL_DecimalMinutes.Checked)
            {
                return ECoordinateFormat.LL_DMm;
            }
            if (rb_Format_MGRS.Checked)
            {
                return ECoordinateFormat.MGRS;
            }
            if (rb_Format_UTM.Checked)
            {
                return ECoordinateFormat.UTM;
            }
            if (rb_Format_Bullseye.Checked)
            {
                return ECoordinateFormat.Bullseye;
            }
            throw new Exception("Selected coordinate format is invalid");
        }

        private string GetEntryCoordinateStr(CoordinateDataEntry entry)
        {
            switch (GetSelectedFormat())
            {
                case ECoordinateFormat.LL_DMSs:
                    return entry.GetCoordinateStrLLDecSec();
                case ECoordinateFormat.LL_DMm:
                    return entry.GetCoordinateStrLLDecMin();
                case ECoordinateFormat.LL_Dd:
                    return entry.GetCoordinateStrLLDecDeg();
                case ECoordinateFormat.MGRS:
                    return entry.GetCoordinateStrMGRS((int)nud_MGRS_Precision.Value);
                case ECoordinateFormat.UTM:
                    return entry.GetCoordinateStrUTM();
                case ECoordinateFormat.Bullseye:
                    return entry.GetCoordinateStrBullseye(bulls);
                default:
                    throw new ArgumentException("Couldn't format coordinate to string.");
            }
        }

        /// <summary>
        /// Handles the Click event of the btn_Add control.
        /// Adds the current output into the <see cref="dgv_CoordinateList"/>
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void Btn_Add_Click(object sender, EventArgs e)
        {
            lbl_Error.Visible = false;
            if (input == null)
            {
                lbl_Error.Visible = true;
                lbl_Error.Text = "Input invalid";
                return;
            }
            dataEntries.Add(input.Clone(dataEntries.Count));
            RefreshDataGrid();
        }

        private void Btn_Replace_Click(object sender, EventArgs e)
        {
            lbl_Error.Visible = false;
            var rowIds = GetSelectedRowIndices();
            int? rowIdx = rowIds.Count == 1 ? rowIds.First() : (int?)null;
            if (!rowIdx.HasValue)
            {
                lbl_Error.Visible = true;
                lbl_Error.Text = "Only one row may be selected.";
                return;
            }

            dataEntries[rowIdx.Value] = input.Clone(rowIdx.Value);
            RefreshDataGrid();
            dgv_CoordinateList.Rows[rowIdx.Value].Selected = true;
        }

        private List<int> GetSelectedRowIndices()
        {
            // Get all row IDs that are selected
            List<int> selectedRowIds = new List<int>();
            foreach (DataGridViewCell cell in dgv_CoordinateList.SelectedCells)
            {
                if (!selectedRowIds.Contains(cell.RowIndex))
                {
                    selectedRowIds.Add(cell.RowIndex);
                }
            }
            foreach (DataGridViewRow row in dgv_CoordinateList.SelectedRows)
            {
                if (!selectedRowIds.Contains(row.Index))
                {
                    selectedRowIds.Add(row.Index);
                }
            }

            return selectedRowIds;
        }

        private void Btn_MoveUp_Click(object sender, EventArgs e)
        {
            lbl_Error.Visible = false;

            var rowIds = GetSelectedRowIndices();
            int? idx = rowIds.Count == 1 ? rowIds.First() : (int?)null;

            if (idx == null)
            {
                lbl_Error.Visible = true;
                lbl_Error.Text = "Only one row may be selected.";
                return;
            }

            int tgt = idx.Value - 1;
            if (SwapRows(idx.Value, tgt))
            {
                dgv_CoordinateList.Rows[tgt].Selected = true;
            }
        }

        private void Btn_MoveDown_Click(object sender, EventArgs e)
        {
            lbl_Error.Visible = false;

            var rowIds = GetSelectedRowIndices();
            int? idx = rowIds.Count == 1 ? rowIds.First() : (int?)null;

            if (idx == null)
            {
                lbl_Error.Visible = true;
                lbl_Error.Text = "Only one row may be selected.";
                return;
            }

            int tgt = idx.Value + 1;
            if (SwapRows(idx.Value, tgt))
            {
                dgv_CoordinateList.Rows[tgt].Selected = true;
            }
        }

        /// <summary>
        /// Swaps the two rows in the data grid
        /// </summary>
        /// <param name="idx">The index of the first row.</param>
        /// <param name="targetIdx">Index of row where it supposed to go.</param>
        /// <returns>true if a swap occurred, false if <paramref name="targetIdx"/> is invalid</returns>
        /// <exception cref="ArgumentOutOfRangeException">idx is not a valid index in the data grid</exception>
        private bool SwapRows(int idx, int targetIdx)
        {
            lbl_Error.Visible = false;
            if (idx < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(idx) + " < 0");
            }
            if (idx >= dgv_CoordinateList.RowCount)
            {
                throw new ArgumentOutOfRangeException(nameof(idx) + " > Row count");
            }
            if (targetIdx < 0 || targetIdx >= dgv_CoordinateList.RowCount)
            {
                return false; // just ignore button presses when already at top/bottom
            }
            CoordinateDataEntry entry = dataEntries[idx];
            CoordinateDataEntry other = dataEntries[targetIdx];
            entry.SwapIds(other);
            dataEntries.Sort((a, b) => a.Id.CompareTo(b.Id));
            ResetIDs();
            RefreshDataGrid();
            return true;
        }

        private void Rb_Format_CheckedChanged(object sender, EventArgs e)
        {
            if (((RadioButton)sender).Checked)
            {
                RefreshDataGrid();
            }
            RefreshBullseyeFormat();
        }
        #endregion

        #endregion

        #region File management

        private readonly OpenFileDialog ofd = new OpenFileDialog()
        {
            Title = "Open Coordinate List",
            AddExtension = true,
            DefaultExt = "json",
            Filter = "JSON files (*.json)|*.json|Text files (*.txt)|*.txt|All files (*.*)|*.*",
            FileName = "coordinates.json",
            Multiselect = false,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Personal),
            ShowReadOnly = false
        };

        private void SaveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            lbl_Error.Visible = false;
            ofd.CheckFileExists = false;

            DialogResult result = ofd.ShowDialog();
            if (result != DialogResult.OK)
            {
                return;
            }
            string filePath = ofd.FileName;
            FileInfo fi = new FileInfo(filePath);
            if (fi.Exists)
            {
                result = MessageBox.Show("You are about to overwrite this file.", "Overwrite file?", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                if (result != DialogResult.OK)
                {
                    return;
                }
            }

            try
            {
                using (FileStream fileHandle = fi.Open(FileMode.Create, FileAccess.Write))
                {
                    string jsonData = Newtonsoft.Json.JsonConvert.SerializeObject(dataEntries, jsonSerializerSettings);
                    byte[] data = new UTF8Encoding(true).GetBytes(jsonData);
                    fileHandle.Write(data, 0, data.Length);
                }
            }
            catch (Exception ex)
            {
                lbl_Error.Visible = true;
                lbl_Error.Text = ex.Message;
            }
        }

        private void LoadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            lbl_Error.Visible = false;

            ofd.CheckFileExists = true;
            DialogResult result = ofd.ShowDialog();
            if (result != DialogResult.OK)
            {
                return;
            }
            string filePath = ofd.FileName;
            FileInfo fi = new FileInfo(filePath);
            if (!fi.Exists)
            {
                lbl_Error.Visible = true;
                lbl_Error.Text = "File does not exist";
                return;
            }

            try
            {
                using (FileStream fileHandle = fi.Open(FileMode.Open, FileAccess.Read))
                {
                    using (StreamReader sr = new StreamReader(fileHandle, System.Text.Encoding.UTF8))
                    {
                        string data = sr.ReadToEnd();
                        dataEntries = Newtonsoft.Json.JsonConvert.DeserializeObject<List<CoordinateDataEntry>>(data, jsonSerializerSettings);
                        ResetIDs();
                        RefreshDataGrid();
                    }
                }
            }
            catch (Exception ex)
            {
                lbl_Error.Visible = true;
                lbl_Error.Text = ex.Message;
            }
        }


        #endregion

        #region DCS

        private void Cb_AltitudeIsAGL_CheckedChanged(object objSender, EventArgs e)
        {
            CheckBox sender = objSender as CheckBox;

            if (input == null)
            {
                return;
            }

            input.AltitudeIsAGL = sender.Checked;
        }

        private List<ToolStripMenuItem> AircraftSelectionMenuStripItems {
            get => new List<ToolStripMenuItem>()
            {
                tsmi_A10C,
                tsmi_AH64_CPG,
                tsmi_AH64_PLT,
                tsmi_AV8B,
                tsmi_F15E_Pilot,
                tsmi_F15E_WSO,
                tsmi_F16,
                tsmi_JF17,
                tsmi_F18,
                tsmi_KA50,
                tsmi_M2000
            };
        }

        private void Tsmi_Aircraft_Auto_Click(object objSender, EventArgs e)
        {
            lbl_Error.Visible = false;

            ToolStripMenuItem sender = objSender as ToolStripMenuItem;

            sender.Checked = !sender.Checked;

            if (sender.Checked)
            {
                foreach (ToolStripMenuItem menuItem in AircraftSelectionMenuStripItems)
                {
                    menuItem.Enabled = false;
                    menuItem.Checked = false;
                }
                selectedAircraft = null;
            }
            else
            {
                // auto was deactivated
                foreach (ToolStripMenuItem menuItem in AircraftSelectionMenuStripItems)
                {
                    menuItem.Enabled = true;
                }
                tsmi_AH64_ClearPoints.Enabled = true;
                tsmi_F16_SetStartFirstPoint.Enabled = true;
                tsmi_JF17_SetFirstPoint.Enabled = true;
            }
        }

        private void AutoSelectAircraft(string model)
        {
            foreach (ToolStripMenuItem mi in AircraftSelectionMenuStripItems)
            {
                mi.Enabled = false;
            }
            tsmi_AH64_ClearPoints.Enabled = false;
            tsmi_F16_SetStartFirstPoint.Enabled = false;
            tsmi_JF17_SetFirstPoint.Enabled = false;
            tsmi_A10C_UseMGRS.Enabled = false;

            if (string.IsNullOrEmpty(model) || model == "null")
            {
                selectedAircraft = null;
            }
            else
            {
                // Switch aircraft. Ask user here which version of the cockpit they are in. (AH64, F15E)
                switch (model)
                {
                    case "AH-64D_BLK_II":
                        tsmi_AH64_PLT.Enabled = true;
                        tsmi_AH64_CPG.Enabled = true;
                        tsmi_AH64_ClearPoints.Enabled = true;
                        if (selectedAircraft != null && selectedAircraft.GetType() == typeof(AH64))
                        {
                            break;
                        }
                        bool isPlt = DialogResult.Yes == MessageBox.Show("Are you pilot?", "PLT/CPG?", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        Tsmi_AircraftSelection_Click(isPlt ? tsmi_AH64_PLT : tsmi_AH64_CPG, null);
                        break;
                    case "FA-18C_hornet":
                        tsmi_F18.Enabled = true;
                        if (selectedAircraft != null && selectedAircraft.GetType() == typeof(F18C))
                        {
                            break;
                        }
                        Tsmi_AircraftSelection_Click(tsmi_F18, null);
                        break;
                    case "F-16C_50":
                        tsmi_F16.Enabled = true;
                        tsmi_F16_SetStartFirstPoint.Enabled = true;
                        if (selectedAircraft != null && selectedAircraft.GetType() == typeof(F16C))
                        {
                            break;
                        }
                        Tsmi_AircraftSelection_Click(tsmi_F16, null);
                        break;
                    case "Ka-50":
                    case "Ka-50_3":
                        tsmi_KA50.Enabled = true;
                        if (selectedAircraft != null && selectedAircraft.GetType() == typeof(KA50))
                        {
                            break;
                        }
                        Tsmi_AircraftSelection_Click(tsmi_KA50, null);
                        break;
                    case "A-10C":
                    case "A-10C_2":
                        tsmi_A10C_UseMGRS.Enabled = true;
                        tsmi_A10C.Enabled = true;
                        if (selectedAircraft != null && selectedAircraft.GetType() == typeof(A10C))
                        {
                            break;
                        }
                        Tsmi_AircraftSelection_Click(tsmi_A10C, null);
                        break;
                    case "JF-17":
                        tsmi_JF17.Enabled = true;
                        tsmi_JF17_SetFirstPoint.Enabled = true;
                        if (selectedAircraft != null && selectedAircraft.GetType() == typeof(JF17))
                        {
                            break;
                        }
                        Tsmi_AircraftSelection_Click(tsmi_JF17, null);
                        break;
                    default:
                        lbl_DCS_Status.Text = "Unknown aircraft: \"" + model + "\"";
                        lbl_DCS_Status.BackColor = DCS_ERROR_COLOR;
                        break;
                }
            }
        }

        private void Tsmi_AircraftSelection_Click(object objSender, EventArgs e)
        {
            lbl_Error.Visible = false;

            // Select the clicked option
            ToolStripMenuItem sender = objSender as ToolStripMenuItem;

            foreach (ToolStripMenuItem mi in AircraftSelectionMenuStripItems)
            {
                mi.Checked = mi.Name == sender.Name;
            }

            // Remind user here: "Transfer uses MGRS instead of L/L if MGRS selected, cockpit must match"
            if (sender.Name == tsmi_AH64_PLT.Name)
            {
                selectedAircraft = new AH64(true);
            }
            else if (sender.Name == tsmi_AH64_CPG.Name)
            {
                selectedAircraft = new AH64(false);
            }
            else if (sender.Name == tsmi_F18.Name)
            {
                selectedAircraft = new F18C();
                MessageBox.Show("Make sure PRECISE mode is selected in HSI->Data.\n" +
                    "Make sure waypoint sequence is not selected before putting in waypoints.\n" +
                    "The next waypoint number and up from the currently selected one will be overwritten\n" +
                    "Make sure aircraft is in L/L Decimal mode (default). Check in HSI -> Data -> Aircraft -> Bottom right\n" +
                    "Make sure no weapon is selected prior to entering weapon data\n" +
                    "Maximum number SLAM-ER of steer points is 5.", "Reminder", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (sender.Name == tsmi_F16.Name)
            {
                Tsmi_F16_SetFirstPoint_Click(tsmi_F16_SetStartFirstPoint, null);
            }
            else if (sender.Name == tsmi_A10C.Name)
            {
                // Ask if user wants to use MGRS or LL
                string questionText = "Do you wish to use MGRS/UTM or L/L?\n" +
                    "The correct setting must be set in the CDU before points are entered.";
                FormAskBinaryQuestion mgrsQuestion = new FormAskBinaryQuestion("Use MGRS or L/L?", "Use L/L", "Use MGRS/UTM", questionText);
                mgrsQuestion.ShowDialog();
                bool useLL = mgrsQuestion.Result;

                // Set checkmark
                tsmi_A10C_UseMGRS.Checked = !useLL;

                // Select aircraft
                selectedAircraft = new A10C(!useLL);
            }
            else if (sender.Name == tsmi_JF17.Name)
            {
                Tsmi_JF17_SetFirstPoint_Click(tsmi_JF17_SetFirstPoint, null);
            }
            else if (sender.Name == tsmi_KA50.Name)
            {
                selectedAircraft = new KA50();
            }
            else if (sender.Name == tsmi_A10C.Name)
            {
                Tsmi_A10C_UseMGRS_Click(tsmi_A10C_UseMGRS, null);
            }
            else
            {
                // Unsupported aircraft
                selectedAircraft = null;
                lbl_Error.Visible = true;
                lbl_Error.Text = "Currently this aircraft is not implemented";

                cb_PointType.Items.Clear();
                cb_PointType.Items.Add(new ComboItem<string>("Waypoint", "Waypoint"));
                cb_PointType.Enabled = false;
                cb_PointType.SelectedIndex = 0;
                RefreshDataGrid();
                return;
            }

            // Update point types for aircraft that have them
            cb_PointType.Items.Clear();
            if (selectedAircraft.GetType() == typeof(AH64))
            {
                cb_PointType.Items.AddRange(selectedAircraft.GetPointTypes().Select(
                    x =>
                    {
                        AH64.EPointType pt = (AH64.EPointType)Enum.Parse(typeof(AH64.EPointType), x);
                        return new ComboItem<AH64.EPointType>(x, pt);
                    }
                ).ToArray());
            }
            else if (selectedAircraft.GetType() == typeof(KA50))
            {
                cb_PointType.Items.AddRange(selectedAircraft.GetPointTypes().Select(
                    x =>
                    {
                        KA50.EPointType pt = (KA50.EPointType)Enum.Parse(typeof(KA50.EPointType), x);
                        return new ComboItem<KA50.EPointType>(x, pt);
                    }
                ).ToArray());
            }
            else if (selectedAircraft.GetType() == typeof(JF17))
            {
                cb_PointType.Items.AddRange(selectedAircraft.GetPointTypes().Select(
                    x =>
                    {
                        JF17.EPointType pt = (JF17.EPointType)Enum.Parse(typeof(JF17.EPointType), x);
                        return new ComboItem<JF17.EPointType>(x, pt);
                    }
                ).ToArray());
            }
            else
            {
                cb_PointType.Items.AddRange(selectedAircraft.GetPointTypes().Select(x => new ComboItem<string>(x, x)).ToArray());
            }
            cb_PointType.Enabled = cb_PointType.Items.Count > 1;
            

            // Add aircraft specific data to the input if input is valid (exists)
            if (selectedAircraft.GetType() == typeof(AH64))
            {
                // if the point has AH64 data, we load it.
                if (input != null && input.AircraftSpecificData.ContainsKey(selectedAircraft.GetType()))
                {
                    AH64SpecificData extraData = input.AircraftSpecificData[selectedAircraft.GetType()] as AH64SpecificData;
                    cb_PointType.SelectedIndex = ComboItem<AH64.EPointType>.FindValue(cb_PointType, extraData.PointType) ?? 0;
                    cb_PointOption.SelectedValue = extraData.Ident;
                }
                else if (input != null) // otherwise we add it.
                {
                    AH64SpecificData extraData = new AH64SpecificData();
                    input.AircraftSpecificData.Add(selectedAircraft.GetType(), extraData);
                    cb_PointType.SelectedIndex = 0;
                }
                else
                {
                    cb_PointType.SelectedIndex = 0;
                }
            }
            else if (selectedAircraft.GetType() == typeof(F18C))
            {
                // if the point has F18C data, we load it.
                if (input != null && input.AircraftSpecificData.ContainsKey(selectedAircraft.GetType()))
                {
                    F18CSpecificData extraData = input.AircraftSpecificData[selectedAircraft.GetType()] as F18CSpecificData;
                    if (extraData.WeaponType.HasValue) // if no value, is a standard waypoint
                    {
                        F18C.EWeaponType pwt = extraData.WeaponType.Value;
                        if (extraData.PreplanPointIdx.HasValue)
                        {
                            // GPS PP Target
                            cb_PointType.SelectedIndex = ComboItem<string>.FindValue(cb_PointType, F18C.GetPointTypePPStrForWeaponType(pwt)) ?? 0;
                            cb_PointOption.SelectedIndex = extraData.PreplanPointIdx.Value;
                        }
                        else
                        {
                            // SLAM-ER STP
                            cb_PointType.SelectedIndex = cb_PointType.Items.Count - 1;
                        }
                    }
                    else
                    {
                        // Standard waypoint
                        cb_PointType.SelectedIndex = 0;
                    }
                }
                else if (input != null) // otherwise we add it.
                {
                    F18CSpecificData extraData = new F18CSpecificData();
                    input.AircraftSpecificData.Add(selectedAircraft.GetType(), extraData);
                    cb_PointType.SelectedIndex = 0;
                }
                else
                {
                    cb_PointType.SelectedIndex = 0;
                }
            }
            else if (selectedAircraft.GetType() == typeof(KA50))
            {
                // if the point has F18C data, we load it.
                if (input != null && input.AircraftSpecificData.ContainsKey(selectedAircraft.GetType()))
                {
                    KA50.EPointType pt = (input.AircraftSpecificData[selectedAircraft.GetType()] as KA50SpecificData).PointType;
                    cb_PointType.SelectedIndex = ComboItem<KA50.EPointType>.FindValue(cb_PointType, pt) ?? 0;
                }
                else if (input != null) // otherwise we add it.
                {
                    KA50SpecificData extraData = new KA50SpecificData(KA50.EPointType.Waypoint);
                    input.AircraftSpecificData.Add(selectedAircraft.GetType(), extraData);
                    cb_PointType.SelectedIndex = 0;
                }
                else
                {
                    cb_PointType.SelectedIndex = 0;
                }
            }
            else if (selectedAircraft.GetType() == typeof(JF17))
            {
                // if the point has F18C data, we load it.
                if (input != null && input.AircraftSpecificData.ContainsKey(selectedAircraft.GetType()))
                {
                    JF17.EPointType pt = (input.AircraftSpecificData[selectedAircraft.GetType()] as JF17SpecificData).PointType;
                    cb_PointType.SelectedIndex = ComboItem<JF17.EPointType>.FindValue(cb_PointType, pt) ?? 0;
                }
                else if (input != null) // otherwise we add it.
                {
                    JF17SpecificData extraData = new JF17SpecificData(JF17.EPointType.Waypoint);
                    input.AircraftSpecificData.Add(selectedAircraft.GetType(), extraData);
                    cb_PointType.SelectedIndex = 0;
                }
                else
                {
                    cb_PointType.SelectedIndex = 0;
                }
            }
            else
            {
                cb_PointType.SelectedIndex = 0;
            }

            RefreshDataGrid();
        }

        private void Cb_pointType_SelectedIndexChanged(object objSender, EventArgs e)
        {
            ComboBox sender = objSender as ComboBox;
            cb_PointOption.Items.Clear();

            if (selectedAircraft == null)
            {
                cb_PointOption.Items.Add(new ComboItem<string>("Waypoint", "Waypoint"));
                cb_PointOption.SelectedIndex = 0;
                cb_PointOption.Enabled = false;
                return;
            }

            if (selectedAircraft.GetType() == typeof(AH64))
            {
                // add all the options for the AH64
                AH64.EPointType ePointType = ComboItem<AH64.EPointType>.GetSelectedValue(cb_PointType);
                object[] items;
                
                switch (ePointType)
                {
                    case AH64.EPointType.Waypoint:
                        items = AH64.EWPOptionDescriptions.Select(x => (object)(new ComboItem<AH64.EPointIdent>(x.Value, x.Key))).ToArray();
                        cb_PointOption.Items.AddRange(items);
                        break;
                    case AH64.EPointType.Hazard:
                        items = AH64.EHZOptionDescriptions.Select(x => (object)(new ComboItem<AH64.EPointIdent>(x.Value, x.Key))).ToArray();
                        cb_PointOption.Items.AddRange(items);
                        break;
                    case AH64.EPointType.ControlMeasure:
                        items = AH64.ECMOptionDescriptions.Select(x => (object)(new ComboItem<AH64.EPointIdent>(x.Value, x.Key))).ToArray();
                        cb_PointOption.Items.AddRange(items);
                        break;
                    case AH64.EPointType.Target:
                        items = AH64.ETGOptionDescriptions.Select(x => (object)(new ComboItem<AH64.EPointIdent>(x.Value, x.Key))).ToArray();
                        cb_PointOption.Items.AddRange(items);
                        break;
                    default:
                        throw new Exception("Bad point type.");
                }

                if (input != null)
                {
                    // Set input to the relevant value
                    if (!input.AircraftSpecificData.ContainsKey(typeof(AH64)))
                    {
                        input.AircraftSpecificData.Add(selectedAircraft.GetType(), new AH64SpecificData());
                    }
                    AH64SpecificData extraData = input.AircraftSpecificData[selectedAircraft.GetType()] as AH64SpecificData;
                    extraData.PointType = ComboItem<AH64.EPointType>.GetSelectedValue(sender);
                    input.AircraftSpecificData[selectedAircraft.GetType()] = extraData;
                }
            }
            else if (selectedAircraft.GetType() == typeof(KA50))
            {
                KA50.EPointType pt = ComboItem<KA50.EPointType>.GetSelectedValue(cb_PointType);
                cb_PointOption.Items.AddRange(selectedAircraft.GetPointOptionsForType(pt.ToString()).Select(x => new ComboItem<string>(x, x)).ToArray());
                if (input != null)
                {
                    if (!input.AircraftSpecificData.ContainsKey(typeof(KA50)))
                    {
                        input.AircraftSpecificData.Add(selectedAircraft.GetType(), new KA50SpecificData(pt));
                    }
                    else
                    {
                        input.AircraftSpecificData[selectedAircraft.GetType()] = new KA50SpecificData(pt);
                    }
                }                
            }
            else if (selectedAircraft.GetType() == typeof(JF17))
            {
                JF17.EPointType pt = ComboItem<JF17.EPointType>.GetSelectedValue(cb_PointType);
                cb_PointOption.Items.AddRange(selectedAircraft.GetPointOptionsForType(pt.ToString()).Select(x => new ComboItem<string>(x, x)).ToArray());
                if (input != null)
                {
                    if (!input.AircraftSpecificData.ContainsKey(typeof(JF17)))
                    {
                        input.AircraftSpecificData.Add(selectedAircraft.GetType(), new JF17SpecificData(pt));
                    }
                    else
                    {
                        input.AircraftSpecificData[selectedAircraft.GetType()] = new JF17SpecificData(pt);
                    }
                }
            }
            else
            {
                string pointTypeStr = ComboItem<string>.GetSelectedValue(cb_PointType);
                cb_PointOption.Items.AddRange(selectedAircraft.GetPointOptionsForType(pointTypeStr).Select(x => new ComboItem<string>(x, x)).ToArray());
            }
            cb_PointOption.SelectedIndex = 0;
            cb_PointOption.Enabled = cb_PointOption.Items.Count > 1;
        }

        private void Cb_PointOption_SelectedIndexChanged(object objSender, EventArgs e)
        {
            if (selectedAircraft == null)
            {
                return;
            }

            ComboBox sender = objSender as ComboBox;
            if (sender.SelectedIndex < 0)
            {
                // newly populated
                return;
            }

            if (input == null)
            {
                return;
            }

            if (selectedAircraft.GetType() == typeof(AH64))
            {
                if (!input.AircraftSpecificData.ContainsKey(typeof(AH64)))
                {
                    input.AircraftSpecificData.Add(selectedAircraft.GetType(), new AH64SpecificData());
                }
                AH64SpecificData extraData = input.AircraftSpecificData[selectedAircraft.GetType()] as AH64SpecificData;
                extraData.Ident = (sender.SelectedItem as ComboItem<AH64.EPointIdent>).Value;
                input.AircraftSpecificData[selectedAircraft.GetType()] = extraData;
            }
            else if (selectedAircraft.GetType() == typeof(F18C))
            {
                if (!input.AircraftSpecificData.ContainsKey(typeof(F18C)))
                {
                    input.AircraftSpecificData.Add(selectedAircraft.GetType(), null);
                }
                
                string pointType = ComboItem<string>.GetSelectedValue(cb_PointType);
                if (pointType == F18C.WAYPOINT_STR)
                {
                    input.AircraftSpecificData[selectedAircraft.GetType()] = new F18CSpecificData();
                }
                else if (pointType == F18C.SLAMER_STP_STR)
                {
                    // SLAM-ER Steer point
                    input.AircraftSpecificData[selectedAircraft.GetType()] = new F18CSpecificData(true);
                }
                else
                {
                    // PrePlanned Target
                    F18C.EWeaponType pwt = (F18C.EWeaponType)Enum.Parse(typeof(F18C.EWeaponType), pointType.Split(' ').First());
                    string pointOption = ComboItem<string>.GetSelectedValue(cb_PointOption);
                    int ppIdx = int.Parse(pointOption.Substring("PP ".Length, 1));
                    F18CSpecificData.EStationSetting stationSetting = (F18CSpecificData.EStationSetting)Enum.Parse(typeof(F18CSpecificData.EStationSetting), pointOption.Substring("PP # - ".Length));
                    input.AircraftSpecificData[selectedAircraft.GetType()] = new F18CSpecificData(pwt, ppIdx, stationSetting);
                }
            }
        }

        private void TransferControl_Click(object sender, EventArgs e)
        {
            lbl_Error.Visible = false;
            if (selectedAircraft == null)
            {
                lbl_Error.Visible = true;
                lbl_Error.Text = "Need to select aircraft type.";
                return;
            }
            try
            {
                lock (lockObjProgressBar)
                {
                    int totalCommands = selectedAircraft.SendToDCS(dataEntries);
                    pb_Transfer.Maximum = totalCommands;
                }
            }
            catch (InvalidOperationException ex)
            {
                lbl_DCS_Status.BackColor = DCS_ERROR_COLOR;
                lbl_DCS_Status.Text = ex.Message;
            }
        }

        private void StopTransferControl_Click(object sender, EventArgs e)
        {
            DCSMessage message = new DCSMessage()
            {
                Stop = true
            };
            DCSConnection.sendRequest(message);
        }

        private void Tsmi_AH64_ClearPoints_Click(object sender, EventArgs e)
        {
            FormAH64PointDeleter pointDeleter = new FormAH64PointDeleter(selectedAircraft as AH64);
            pointDeleter.ShowDialog(this);
            if (pointDeleter.NumberOfCommands > 0)
            {
                lock (lockObjProgressBar)
                {
                    pb_Transfer.Maximum = pointDeleter.NumberOfCommands;
                    pb_Transfer.Value = 0;
                }
            }
        }

        private void Tsmi_F16_SetFirstPoint_Click(object sender, EventArgs e)
        {
            FormStartingWaypoint startingWaypointForm = new FormStartingWaypoint(1, 699, 200);
            startingWaypointForm.ShowDialog();
            int startingWaypoint = startingWaypointForm.StartingWaypoint;
            selectedAircraft = new F16C(startingWaypoint);
        }

        private void Tsmi_JF17_SetFirstPoint_Click(object sender, EventArgs e)
        {
            FormStartingWaypoint startingWaypointForm = new FormStartingWaypoint(1, 29, 10);
            startingWaypointForm.ShowDialog();
            int startingWaypoint = startingWaypointForm.StartingWaypoint;
            selectedAircraft = new JF17(startingWaypoint);
        }

        private void Tsmi_A10C_UseMGRS_Click(object sender, EventArgs e)
        {
            if (selectedAircraft == null || selectedAircraft.GetType() != typeof(A10C))
            {
                tsmi_A10C_UseMGRS.Checked = false;
                return;
            }
            bool useMGRS = !(selectedAircraft as A10C).UsingMGRS;
            selectedAircraft = new A10C(usingMGRS: useMGRS);
            tsmi_A10C_UseMGRS.Checked = useMGRS;
        }

        private void FetchCoordinatesControl_Click(object sender, EventArgs e)
        {
            input = dcsCoordinate;
            RefreshCoordinates(EUpdateType.CoordinateInput);
        }

        private void ImportUnitsControl_Click(object sender, EventArgs e)
        {
            try
            {
                // prevent messages from being sent to DCS
                tmr250ms.Stop();

                // Open the form and get the data
                FormUnitImporter fui = new FormUnitImporter(dataEntries)
                {
                    TopMost = TopMost
                };
                fui.ShowDialog(this);
                if (fui.Coordinates != null)
                {
                    dataEntries.AddRange(fui.Coordinates);
                    RefreshDataGrid();
                }
            }
            finally
            {
                tmr250ms.Start();
            }
        }

        private CoordinateDataEntry dcsCoordinate = null;
        private bool wasConnected = false;
        private DateTime lastDCSErrorTime = DateTime.MinValue;
        private void Tmr250ms_Tick(object sender, EventArgs e)
        {
            tmr250ms.Stop(); // only run one timer at a time
            try
            {
                DCSMessage message = new DCSMessage()
                {
                    FetchCameraPosition = true,
                    FetchAircraftType = tsmi_Auto.Checked,
                    FetchWeaponStations = selectedAircraft != null && selectedAircraft.GetType() == typeof(F18C)
                };
                message = DCSConnection.sendRequest(message);

                if (message == null)
                {
                    lbl_DCS_Status.Text = "Not connected";
                    lbl_DCS_Status.BackColor = DCS_ERROR_COLOR;
                    wasConnected = false;
                    if (eReticleSetting == EReticleSetting.WhenF10)
                    {
                        reticleForm.Hide();
                    }
                    return;
                }

                if (!wasConnected)
                {
                    // update AGL values
                    wasConnected = true;
                    RefreshDataGrid();
                }

                if (message.ServerErrors != null && message.ServerErrors.Count > 0)
                {
                    lbl_DCS_Status.Text = message.ServerErrors.First();
                    lbl_DCS_Status.BackColor = DCS_ERROR_COLOR;
                    return;
                }

                if (tsmi_Auto.Checked)
                {
                    AutoSelectAircraft(message.AircraftType);
                }

                if (message.CameraPosition == null)
                {
                    lbl_DCS_Status.Text = "Connected, but no coordinates";
                    lbl_DCS_Status.BackColor = DCS_ERROR_COLOR;
                    return;
                }

                if (message.CurrentCommandIndex.HasValue)
                {
                    if (message.CurrentCommandIndex.Value <= pb_Transfer.Maximum)
                    {
                        lock (lockObjProgressBar)
                        {
                            pb_Transfer.Value = message.CurrentCommandIndex.Value;
                            pb_Transfer.Visible = true;
                        }
                    }
                }
                else
                {
                    pb_Transfer.Visible = false;
                }

                if (eReticleSetting == EReticleSetting.WhenF10)
                {
                    if (message.IsF10View ?? false)
                    {
                        reticleForm.Show();
                        Tsmi_Screen_Click(selectedScreenMenuItem, null); // set to screen center
                    }
                    else
                    {
                        reticleForm.Hide();
                    }
                }

                if (selectedAircraft != null && selectedAircraft.GetType() == typeof(F18C) && message.WeaponStations != null)
                {
                    (selectedAircraft as F18C).UpdateWeaponStations(message.WeaponStations);
                }

                // Update display
                if ((DateTime.Now - lastDCSErrorTime) < TimeSpan.FromSeconds(10))
                {
                    return;
                }

                var coordinate = new CoordinateSharp.Coordinate(message.CameraPosition.Lat, message.CameraPosition.Lon);
                var altitudeInM = cameraPosMode == ECameraPosMode.TerrainElevation ? 0 : message.CameraPosition.Alt ?? 0;
                bool altitudeIsAGL = cameraPosMode == ECameraPosMode.TerrainElevation;
                dcsCoordinate = new CoordinateDataEntry(-1, coordinate, altitudeInM, altitudeIsAGL)
                {
                    GroundElevationInM = message.CameraPosition.Elevation,
                    XFer = true,
                    Name = String.Empty
                };

                string coordinateText = GetEntryCoordinateStr(dcsCoordinate) + " | " + dcsCoordinate.GetAltitudeString(cb_AltitudeUnit.Text == "ft");
                

                lbl_DCS_Status.Text = coordinateText;
                lbl_DCS_Status.BackColor = DCS_OK_COLOR;
            }
            finally
            {
                // restart timer for next time
                tmr250ms.Start();
            }
        }

        private void Lbl_DCS_Status_BackColorChanged(object objSender, EventArgs e)
        {
            ToolStripStatusLabel sender = objSender as ToolStripStatusLabel;
            if (sender.BackColor == DCS_ERROR_COLOR)
            {
                lastDCSErrorTime = DateTime.Now;
            }
            else if (sender.BackColor == DCS_OK_COLOR)
            {
                lastDCSErrorTime = DateTime.MinValue;
            }
            else
            {
                throw new ArgumentException("Color should be DCS_ERROR or DCS_OK");
            }
        }

        #endregion

        #region Settings

        private enum ECameraPosMode
        {
            CameraAltitude,
            TerrainElevation
        }

        private ECameraPosMode cameraPosMode;
        private void Tsmi_TerrainElevationUnderCamera_Click(object sender, EventArgs e)
        {
            cameraPosMode = ECameraPosMode.TerrainElevation;
            tsmi_TerrainElevationUnderCamera.Checked = true;
            tsmi_CameraAltitude.Checked = false;
        }

        private void Tsmi_CameraAltitude_Click(object sender, EventArgs e)
        {
            cameraPosMode = ECameraPosMode.CameraAltitude;
            tsmi_TerrainElevationUnderCamera.Checked = false;
            tsmi_CameraAltitude.Checked = true;
        }

        #region ReticleSettings
        enum EReticleSetting
        {
            Never,
            Always,
            WhenF10
        }

        private EReticleSetting eReticleSetting = EReticleSetting.WhenF10;

        private void Tsmi_Screen_Click(object objSender, EventArgs e)
        {
            // Gets the screen associated with the menu item and sets the reticle to the center of that screen
            ToolStripMenuItem sender = objSender as ToolStripMenuItem;
            selectedScreenMenuItem = sender;
            int idx = int.Parse(sender.Name.Split('_').Last());
            Rectangle screen = Screen.AllScreens[idx].Bounds;
            Point screenCenter = new Point(screen.X + (screen.Width / 2), screen.Y + (screen.Height / 2));

            reticleForm.Location = new Point(screenCenter.X - (reticleForm.Width / 2), screenCenter.Y - (reticleForm.Height / 2));

            // Unsets all checkboxes except the one clicked
            foreach (ToolStripMenuItem mi in tsmi_DCSMainScreenMenu.DropDownItems)
            {
                mi.Checked = mi.Name == sender.Name;
            }
        }
        private void Tsmi_Reticle_WhenInF10Map_Click(object sender, EventArgs e)
        {
            eReticleSetting = EReticleSetting.WhenF10;
            SetReticleSettingsCheckmarks();
        }

        private void Tsmi_Reticle_Always_Click(object sender, EventArgs e)
        {
            eReticleSetting = EReticleSetting.Always;
            SetReticleSettingsCheckmarks();
        }

        private void Tsmi_Reticle_Never_Click(object sender, EventArgs e)
        {
            eReticleSetting = EReticleSetting.Never;
            SetReticleSettingsCheckmarks();
        }

        private void SetReticleSettingsCheckmarks()
        {
            tsmi_Reticle_WhenInF10Map.Checked = eReticleSetting == EReticleSetting.WhenF10;
            tsmi_Reticle_Always.Checked = eReticleSetting == EReticleSetting.Always;
            tsmi_Reticle_Never.Checked = eReticleSetting == EReticleSetting.Never;

            if (eReticleSetting == EReticleSetting.Always)
            {
                reticleForm.Show();
            }
            else
            {
                reticleForm.Hide();
            }
        }
        #endregion
        
        #region VisibilitySettings
        private void Tsmi_Opaque_Click(object sender, EventArgs e)
        {
            this.Opacity = 1.0;
            tsmi_opaque.Checked = true;
            tsmi_Opacity75.Checked = false;
            tsmi_Opacity50.Checked = false;
            tsmi_Opacity25.Checked = false;
        }
        private void Tsmi_Opacity50_Click(object sender, EventArgs e)
        {
            this.Opacity = 0.5;
            tsmi_opaque.Checked = false;
            tsmi_Opacity75.Checked = false;
            tsmi_Opacity50.Checked = true;
            tsmi_Opacity25.Checked = false;

        }
        private void Tsmi_Opacity25_Click(object sender, EventArgs e)
        {
            this.Opacity = 0.25;
            tsmi_opaque.Checked = false;
            tsmi_Opacity75.Checked = false;
            tsmi_Opacity50.Checked = false;
            tsmi_Opacity25.Checked = true;
        }
        private void Tsmi_Opacity75_Click(object sender, EventArgs e)
        {
            this.Opacity = 0.75;
            tsmi_opaque.Checked = false;
            tsmi_Opacity75.Checked = true;
            tsmi_Opacity50.Checked = false;
            tsmi_Opacity25.Checked = false;
        }
        private void Control_AlwaysOnTop_Click(object sender, EventArgs e)
        {
            this.TopMost = !this.TopMost;
            tsmi_AlwaysOnTop.Checked = this.TopMost;
            btn_AlwaysOnTop.BackColor = this.TopMost ? Color.FromArgb(0x66, 0x99, 0x00) : DefaultBackColor;
        }
        #endregion
        #endregion

        /// <summary>
        /// CTOR
        /// </summary>
        public MainForm()
        {
            InitializeComponent();
            cb_PointType.ValueMember = "Value";
            cb_PointType.DisplayMember = "Text";
            cb_PointOption.ValueMember = "Value";
            cb_PointOption.DisplayMember = "Text";
            SetReticleSettingsCheckmarks();
            tmr250ms.Start();

            // Create screen selection menu
            int idx = 0;
            foreach(Screen screen in Screen.AllScreens)
            {
                Rectangle bounds = screen.Bounds;
                ToolStripMenuItem screenMenuItem = new ToolStripMenuItem()
                {
                    Text = idx.ToString() + ": " + screen.DeviceName + " [" + bounds.Width + "x" + bounds.Height + "]",
                    Checked = screen.Primary,
                    Name = string.Format("ScreenToolStripMenuItem_{0}", idx),
                };
                screenMenuItem.Click += Tsmi_Screen_Click;
                tsmi_DCSMainScreenMenu.DropDownItems.Add(screenMenuItem);
                if (screenMenuItem.Checked)
                {
                    selectedScreenMenuItem = screenMenuItem;
                    Tsmi_Screen_Click(screenMenuItem, null);
                }
                idx++;
            }

            cameraPosMode = tsmi_TerrainElevationUnderCamera.Checked ? ECameraPosMode.TerrainElevation : ECameraPosMode.CameraAltitude;

            dele = new WinEventDelegate(WinEventProc);
            IntPtr m_hhook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, dele, 0, 0, WINEVENT_OUTOFCONTEXT);
        }

        #region MagicForSingleClickButtons
        #region Mouse        
        /// <summary>
        /// Causes a system callback to simulate a mouse event.
        /// </summary>
        /// <param name="dwFlags">The dw flags.</param>
        /// <param name="dx">The dx.</param>
        /// <param name="dy">The dy.</param>
        /// <param name="cButtons">The c buttons.</param>
        /// <param name="dwExtraInfo">The dw extra information.</param>
        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, uint dwExtraInfo);
        private enum MouseEvents
        {
            LeftDown = 0x02,
            LeftUp = 0x04,
            RightDown = 0x08,
            RightUp = 0x10
    }
        

        /// <summary>
        /// Does the mouse click.
        /// </summary>
        public void DoMouseClick()
        {
            //Call the imported function with the cursor's current position
            uint X = (uint)Cursor.Position.X;
            uint Y = (uint)Cursor.Position.Y;
            mouse_event((int)MouseEvents.LeftDown | (int)MouseEvents.LeftUp, X, Y, 0, 0);
        }
        #endregion
        #region WindowsCallbackOnActiveWindowChange
        readonly WinEventDelegate dele = null;

        delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        private const uint WINEVENT_OUTOFCONTEXT = 0;
        private const uint EVENT_SYSTEM_FOREGROUND = 3;

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        private string GetActiveWindowTitle()
        {
            const int nChars = 256;
            StringBuilder Buff = new StringBuilder(nChars);
            IntPtr handle = GetForegroundWindow();

            if (GetWindowText(handle, Buff, nChars) > 0)
            {
                return Buff.ToString();
            }
            return null;
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            MouseButtons mb = Control.MouseButtons;
            if (GetActiveWindowTitle() != this.Text)
            {
                return;
            }
            // this form was just put into focus
            if ((mb & MouseButtons.Left) == MouseButtons.Left)
            {
                // mouse button is down
                DoMouseClick();
            }
        }
        #endregion

        #endregion
    }
}
