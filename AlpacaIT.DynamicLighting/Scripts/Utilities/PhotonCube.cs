﻿using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace AlpacaIT.DynamicLighting
{
    /// <summary>
    /// Cubemap containing photon data of direct illumination used for a technique similar to photon mapping.
    /// </summary>
    internal class PhotonCube
    {
        /// <summary>One side of a <see cref="PhotonCube"/> containing pixel data.</summary>
        private class PhotonCubeFace
        {
            /// <summary>
            /// The distances to each pixel and world-space normals of each pixel on the cubemap face.
            /// <para><see cref="float4.x"/> contains the distance.</para>
            /// <para><see cref="float4.yzw"/> contains the world-space normal.</para>
            /// </summary>
            public readonly float4[] colors;

            /// <summary>The width and height of each face of the photon cube in pixels.</summary>
            public readonly int size;

            public PhotonCubeFace(float4[] colors, int size)
            {
                this.colors = colors;
                this.size = size;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private int Index(float2 position)
            {
                position.x = 1.0f - position.x;
                var x = (int)(position.x * size);
                var y = (int)(position.y * size);
                x = math.max(0, math.min(x, size - 1));
                y = math.max(0, math.min(y, size - 1));
                return y * size + x;
            }

            public float SampleDistance(float2 position)
            {
                return colors[Index(position)].x;
            }

            public float3 SampleNormal(float2 position)
            {
                return colors[Index(position)].yzw;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int Index(float2 position, int size)
            {
                position.x = 1.0f - position.x;
                var x = (int)(position.x * size);
                var y = (int)(position.y * size);
                x = math.max(0, math.min(x, size - 1));
                y = math.max(0, math.min(y, size - 1));
                return y * size + x;
            }
        }

        /// <summary>The six faces of the photon cube.</summary>
        private readonly PhotonCubeFace[] faces = new PhotonCubeFace[6];

        /// <summary>The width and height of each face of the photon cube in pixels.</summary>
        private readonly int size;

        /// <summary>
        /// Creates a new <see cref="PhotonCube"/> instance that copies the data from a cubemap <see
        /// cref="RenderTexture"/> into memory.
        /// </summary>
        /// <param name="cubemapRenderTexture">
        /// The cubemap <see cref="RenderTexture"/> with a <see cref="RenderTexture.volumeDepth"/>
        /// of 6 faces.
        /// </param>
        public PhotonCube(RenderTexture cubemapRenderTexture)
        {
            // validate the arguments to prevent any errors.
            if (cubemapRenderTexture == null) throw new System.ArgumentNullException(nameof(cubemapRenderTexture));
            if (cubemapRenderTexture.dimension != TextureDimension.Cube) throw new System.ArgumentException("The render texture must have the dimension set to cube.", nameof(cubemapRenderTexture));
            if (cubemapRenderTexture.volumeDepth != 6) throw new System.ArgumentException("The render texture for photon cubes must have 6 faces.", nameof(cubemapRenderTexture));

            // remember the size of the cubemap texture in pixels.
            size = cubemapRenderTexture.width;

#if UNITY_2021_3_OR_NEWER && !UNITY_2021_3_0 && !UNITY_2021_3_1 && !UNITY_2021_3_2 && !UNITY_2021_3_3 && !UNITY_2021_3_4 && !UNITY_2021_3_5 && !UNITY_2021_3_6 && !UNITY_2021_3_7 && !UNITY_2021_3_8 && !UNITY_2021_3_9 && !UNITY_2021_3_10 && !UNITY_2021_3_11 && !UNITY_2021_3_12 && !UNITY_2021_3_13 && !UNITY_2021_3_14 && !UNITY_2021_3_15 && !UNITY_2021_3_16 && !UNITY_2021_3_17 && !UNITY_2021_3_18 && !UNITY_2021_3_19 && !UNITY_2021_3_20 && !UNITY_2021_3_21 && !UNITY_2021_3_22 && !UNITY_2021_3_23 && !UNITY_2021_3_24 && !UNITY_2021_3_25 && !UNITY_2021_3_26 && !UNITY_2021_3_27
            var photonCameraRenderTextureDescriptor = new RenderTextureDescriptor(size, size, RenderTextureFormat.ARGBFloat, 16, 0, RenderTextureReadWrite.Linear);
#else
            var photonCameraRenderTextureDescriptor = new RenderTextureDescriptor(size, size, RenderTextureFormat.ARGBFloat, 16, 0);
#endif
            photonCameraRenderTextureDescriptor.autoGenerateMips = false;

            // extract the 6 sides of the cubemap:
            var rt = RenderTexture.GetTemporary(photonCameraRenderTextureDescriptor);
            rt.filterMode = FilterMode.Point;
            var readableTexture = new Texture2D(size, size, TextureFormat.RGBAFloat, false, true);
            readableTexture.filterMode = FilterMode.Point;
            for (int face = 0; face < 6; face++)
            {
                Graphics.CopyTexture(cubemapRenderTexture, face, 0, rt, 0, 0);
                RenderTexture.active = rt;
                readableTexture.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                readableTexture.Apply();
                float4[] pixels = new float4[size * size];
                readableTexture.GetPixelData<float4>(0).CopyTo(pixels);
                faces[face] = new PhotonCubeFace(pixels, size);
                RenderTexture.active = null;
            }
            Object.DestroyImmediate(readableTexture);
            RenderTexture.ReleaseTemporary(rt);
        }

        /// <summary>
        /// Samples a cubemap like a shader would and returns the face index to sample with the coordinates.
        /// <para>https://www.gamedev.net/forums/topic/687535-implementing-a-cube-map-lookup-function/</para>
        /// </summary>
        /// <param name="direction">The direction to sample the cubemap in.</param>
        /// <param name="face">The face index to be sampled from.</param>
        /// <returns>The UV coordinates to sample the cubemap at.</returns>
        public static unsafe float2 GetFaceUvByDirection(float3 direction, out int face)
        {
            float3 vAbs = direction;
            UMath.Abs(&vAbs);
            float ma;
            float2 uv; _ = &uv;
            if (vAbs.z >= vAbs.x && vAbs.z >= vAbs.y)
            {
                face = direction.z < 0.0f ? 5 : 4;
                ma = 0.5f / vAbs.z;
                uv.x = direction.z < 0.0f ? -direction.x : direction.x;
                uv.y = -direction.y;
            }
            else if (vAbs.y >= vAbs.x)
            {
                face = direction.y < 0.0f ? 3 : 2;
                ma = 0.5f / vAbs.y;
                uv.x = direction.x;
                uv.y = direction.y < 0.0f ? -direction.z : direction.z;
            }
            else
            {
                face = direction.x < 0.0f ? 1 : 0;
                ma = 0.5f / vAbs.x;
                uv.x = direction.x < 0.0f ? direction.z : -direction.z;
                uv.y = -direction.y;
            }
            UMath.Scale(&uv, ma);
            UMath.Add(&uv, 0.5f);
            return uv;//uv * ma + 0.5f;
        }

        /// <summary>
        /// Computes the direction vector from a given UV coordinate and face index of a cubemap.
        /// </summary>
        /// <param name="uv">The UV coordinates to sample the cubemap at.</param>
        /// <param name="face">The face index to be sampled from.</param>
        /// <returns>The direction vector corresponding to the UV and face index.</returns>
        public static float3 GetDirectionByFaceUv(float2 uv, int face)
        {
            // adjust uv coordinates from [0,1] to [-1,1].
            uv = 2.0f * uv - 1.0f;

            float3 direction = default;
            switch (face)
            {
                case 0: direction = new float3(1.0f, -uv.y, -uv.x); break;  // +X
                case 1: direction = new float3(-1.0f, -uv.y, uv.x); break;  // -X
                case 2: direction = new float3(uv.x, 1.0f, uv.y); break;    // +Y
                case 3: direction = new float3(uv.x, -1.0f, -uv.y); break;  // -Y
                case 4: direction = new float3(uv.x, -uv.y, 1.0f); break;   // +Z
                case 5: direction = new float3(-uv.x, -uv.y, -1.0f); break; // -Z
            }

            // normalize the direction vector to ensure it's a unit vector.
            return math.normalize(direction);
        }

        /// <summary>Does required prerequisite computation to access the fast-methods.</summary>
        /// <param name="direction">The direction to sample the cubemap in.</param>
        /// <param name="photonCubeFace">The face index of the cubemap.</param>
        /// <param name="photonCubeFaceIndex">The index into the cubemap face data arrays.</param>
        public void FastSamplePrerequisite(float3 direction, out int photonCubeFace, out int photonCubeFaceIndex)
        {
            var uv = GetFaceUvByDirection(direction, out photonCubeFace);
            photonCubeFaceIndex = PhotonCubeFace.Index(uv, size);
        }

        /// <summary>Gets the distance to the closest fragment in the given direction.</summary>
        /// <param name="direction">The direction to sample the cubemap in.</param>
        /// <returns>The distance to the fragment.</returns>
        public float SampleDistance(float3 direction)
        {
            var uv = GetFaceUvByDirection(direction, out var face);
            return faces[face].SampleDistance(uv);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float SampleDistanceFast(int photonCubeFace, int photonCubeFaceIndex)
        {
            return faces[photonCubeFace].colors[photonCubeFaceIndex].x;
        }

        /// <summary>Gets an approximate normal of the closest fragment in the given direction.</summary>
        /// <param name="direction">The direction to sample the cubemap in.</param>
        /// <returns>The normal of the fragment.</returns>
        public float3 SampleNormal(float3 direction)
        {
            var uv = GetFaceUvByDirection(direction, out var face);
            return faces[face].SampleNormal(uv);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float3 SampleNormalFast(int photonCubeFace, int photonCubeFaceIndex)
        {
            return faces[photonCubeFace].colors[photonCubeFaceIndex].yzw;
        }

        /// <summary>
        /// Gets an approximate world position of the closest fragment in the given direction.
        /// </summary>
        /// <param name="direction">The direction to sample the cubemap in.</param>
        /// <param name="lightPosition">The light position in world-space coordinates.</param>
        /// <returns>The world position of the fragment.</returns>
        public float3 SampleWorld(float3 direction, float3 lightPosition)
        {
            return lightPosition + direction * SampleDistance(direction);
        }

        public unsafe float3 SampleWorldFast(float3 direction, float3 lightPosition, int photonCubeFace, int photonCubeFaceIndex)
        {
            // return lightPosition + direction * SampleDistanceFast(photonCubeFace, photonCubeFaceIndex);
            // using 'direction' as scratch buffer.
            UMath.Scale(&direction, SampleDistanceFast(photonCubeFace, photonCubeFaceIndex));
            UMath.Add(&direction, &lightPosition);
            return direction;
        }

        /// <summary>
        /// Gets software rendered real-time shadows returning true when the given fragment world
        /// position is in the light else false.
        /// </summary>
        /// <param name="lightPosition">The light position in world-space coordinates.</param>
        /// <param name="worldPosition">The fragment position in world-space coordinates.</param>
        /// <param name="worldNormal">The normal of the fragment (e.g. triangle normal).</param>
        /// <returns>True when the fragment position is in light else false.</returns>
        public unsafe bool SampleShadow(float3 lightPosition, float3 worldPosition, float3 worldNormal)
        {
            // calculate the unnormalized direction between the light source and the fragment.
            //float3 light_direction = lightPosition - worldPosition;
            float3* light_direction = &lightPosition;
            UMath.Subtract(&lightPosition, &worldPosition);

            // calculate the square distance between the light source and the fragment.
            // distance(i.world, light.position); but squared to prevent a square root.
            float light_distanceSqr = UMath.SqrMagnitude(light_direction);

            // a simple dot product with the normal gives us diffusion.
            float NdotL = Mathf.Max(math.dot(worldNormal, *light_direction), 0);

            // magic bias function! it is amazing!
            float light_distance = math.sqrt(light_distanceSqr);
            float magic = 0.02f + 0.01f * (light_distanceSqr / light_distance);
            float autobias = magic * math.tan(math.acos(1.0f - NdotL));
            autobias = Mathf.Clamp(autobias, 0.0f, magic);

            // negative!
            UMath.Negate(light_direction);
            float shadow_mapping_distance = SampleDistance(*light_direction);

            // when the fragment is occluded we can early out here.
            if (light_distance - autobias > shadow_mapping_distance)
                return false;

            return true;
        }

        /// <summary>
        /// Gets software rendered real-time shadows returning true when the given fragment by
        /// direction and distance is in the light else false.
        /// </summary>
        /// <param name="lightDirection">The direction between the light source and the fragment.</param>
        /// <param name="lightDistanceToWorld">The distance between the light source and the fragment.</param>
        /// <param name="worldNormal">The normal of the fragment (e.g. triangle normal).</param>
        /// <returns>True when the fragment position is in light else false.</returns>
        public unsafe bool SampleShadow(float3 lightDirection, float lightDistanceToWorld, float3 worldNormal)
        {
            // calculate the square distance between the light source and the fragment.
            float light_distanceSqr = lightDistanceToWorld * lightDistanceToWorld;

            // a simple dot product with the normal gives us diffusion.
            float NdotL = Mathf.Max(math.dot(worldNormal, lightDirection), 0);

            // magic bias function! it is amazing!
            float light_distance = lightDistanceToWorld;
            float magic = 0.02f + 0.01f * (light_distanceSqr / light_distance);
            float autobias = magic * math.tan(math.acos(1.0f - NdotL));
            autobias = Mathf.Clamp(autobias, 0.0f, magic);

            // negative!
            UMath.Negate(&lightDirection);
            float shadow_mapping_distance = SampleDistance(lightDirection);

            // when the fragment is occluded we can early out here.
            if (light_distance - autobias > shadow_mapping_distance)
                return false;

            return true;
        }
    }
}