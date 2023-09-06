﻿using Newtonsoft.Json;
using System;

namespace CoordinateConverter
{
    /// <summary>
    /// Is a command to be interpreted by the DCS lua script as a cockpit action.
    /// To get the numbers for any specific action, export controls as HTML and analyze the mangled names in the last column
    /// </summary>
    public class DCSCommand : DCSMessage
    {
        /// <summary>
        /// Empty default constructor for newtonsoft
        /// </summary>
        [JsonConstructor]
        public DCSCommand()
        {
            // Empty
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="device">Device ID</param>
        /// <param name="code">Device button ID</param>
        /// <param name="delay">Delay before depressing the button and/or pressing the next button</param>
        /// <param name="activate">Either 1 or -1. Actually a double in DCS, but only for axis commands.</param>
        /// <param name="addDepress">Adds the button depress action right after</param>
        public DCSCommand(int device, int code, int delay, int activate, bool addDepress)
        {
            Device = device;
            Code = code;
            Delay = delay;
            Activate = activate;
            AddDepress = addDepress;
        }
    
        /// <summary>
        /// Device ID in cockpit
        /// </summary>
        [JsonProperty("device")]
        public int Device { get; set; }
        
        /// <summary>
        /// Button ID on the device in the cockpit
        /// </summary>
        [JsonProperty("code")]
        public int Code { get; set; }

        /// <summary>
        /// Delay before depressing the button and/or pressing the next button
        /// </summary>
        [JsonProperty("delay")]
        public int Delay { get; set; }

        /// <summary>
        /// For hard buttons this is 1 or -1
        /// </summary>
        [JsonProperty("activate")]
        public int Activate { get; set; }

        /// <summary>
        /// For serialization only
        /// </summary>
        [JsonProperty("addDepress")]
        public string AddDepressStr
        {
            get
            {
                return AddDepress.ToString();
            }
            set
            {
                if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
                {
                    AddDepress = false;
                }
                else if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
                {
                    AddDepress = true;
                }
                else
                {
                    throw new ArgumentException("Bad value");
                }
            }
        }

        /// <summary>
        /// If true, also adds the depress action
        /// </summary>
        [JsonIgnore]
        public bool AddDepress { get; set; }

        /// <summary>
        /// Converts to string.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return string.Format("D:{0}, C:{1}, Dly: {2}, Ac:{3}, Dp: {4}", Device, Code, Delay, Activate, AddDepress ? 1 : 0);
        }
    }
}
