﻿using UnityEngine;

namespace AlpacaIT.DynamicLighting
{
    /// <summary>
    /// Stores metadata for a dynamic light in the scene used by <see cref="DynamicLightManager"/>.
    /// This is a reverse approach to associate additional data with a scene object without
    /// modifying said object.
    /// </summary>
    [System.Serializable]
    internal class RaycastedDynamicLight
    {
        /// <summary>The reference dynamic light in the scene that this metadata is for.</summary>
        public DynamicLight light;

        /// <summary>The original position the immovable dynamic light was raycasted at.</summary>
        public Vector3 origin = Vector3.positiveInfinity;

        /// <summary>Gets or sets whether the <see cref="light"/> is a valid instance and enabled.</summary>
        public bool lightAvailable = true;

        /// <summary>The compression level for bounce lighting data used during raytracing.</summary>
        public DynamicBounceLightingCompressionMode bounceCompression = DynamicBounceLightingCompressionMode.Inherit;

        /// <summary>Creates a new instance for the given <see cref="DynamicLight"/>.</summary>
        /// <param name="dynamicLight">The <see cref="DynamicLight"/> to be referenced.</param>
        public RaycastedDynamicLight(DynamicLight dynamicLight, DynamicBounceLightingDefaultCompressionMode defaultCompressionMode)
        {
            light = dynamicLight;
            origin = dynamicLight.transform.position;

            // inherit the compression mode from the dynamic light manager if desired.
            var compressionMode = dynamicLight.lightBounceCompression;
            if (compressionMode == DynamicBounceLightingCompressionMode.Inherit)
                compressionMode = (DynamicBounceLightingCompressionMode)defaultCompressionMode;
            bounceCompression = compressionMode;
        }
    }
}