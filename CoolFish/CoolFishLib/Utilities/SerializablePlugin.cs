﻿using System;

namespace CoolFishNS.Utilities
{
    /// <summary>
    ///     Class for saving info about loaded plugins
    /// </summary>
    [Serializable]
    public class SerializablePlugin
    {
        /// <summary>
        ///     true if the plugin is currently enabled; otherwise, false
        /// </summary>
        public bool? IsEnabled { get; set; }

        /// <summary>
        ///     File name of the plugin
        /// </summary>
        public string FileName { get; set; }
    }
}