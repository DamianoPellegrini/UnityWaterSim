using System;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;
using Water;
using Water.Spectrum;
using Water.Wakes;

namespace NN
{
    /// <summary>
    /// Generates CSV data based on water simulation results for neural network training.
    /// </summary>
    public class NNDataGenerator : IDisposable
    {
        private SpectrumCascade cascade;
        private FastFourierTransform fft;

        /// <summary>
        /// Initializes the data generator for neural network inputs, creating instances of SpectrumCascade and FastFourierTransform.
        /// </summary>
        /// <param name="size">Resolution for the spectrum cascade.</param>
        /// <param name="scaleCutoffs">Vector containing the length scale (x) and cutoffs (y,z) for the spectrum cascade.</param>
        public NNDataGenerator(int size, Vector3 scaleCutoffs)
        {
            var settings = GraphicsSettings.GetRenderPipelineSettings<WaterSimulationSettings>();

            cascade = new SpectrumCascade($"{nameof(NNDataGenerator)}", size, scaleCutoffs.x, scaleCutoffs.y, scaleCutoffs.z);
            fft = new FastFourierTransform(size, settings.FFTShader);
        }

        /// <summary>
        /// Releases resources held by the cascade and FFT objects.
        /// </summary>
        public void Dispose()
        {
            cascade.Dispose();
            fft.Dispose();
        }

        /// <summary>
        /// Simulates a water surface frame, generating displacement, derivative, and turbulence data and writing it to a CSV format.
        /// </summary>
        /// <param name="stream">Stream where the generated CSV data is written.</param>
        /// <param name="spectrum">Water frequency spectrum used to generate data.</param>
        /// <param name="time">Elapsed time for the simulation step.</param>
        /// <param name="deltaTime">Time difference between simulation frames.</param>
        /// <param name="noise">Texture for Gaussian noise, ideally large enough to avoid repetition.</param>
        public void Generate(Stream stream, WaterFrequencySpectrum spectrum, float time, float deltaTime, Texture noise)
        {
            spectrum.SampleSpectrum(cascade);
            spectrum.CalculateInitials(cascade, noise);
            spectrum.Evolve(cascade, time);
            spectrum.CalculateDisplacement(cascade, fft, deltaTime);

            using (var sw = new StreamWriter(stream, Encoding.ASCII, 4096, true))
            {
                var displacement = ToTexture2D(cascade.displacement);
                var derivative = ToTexture2D(cascade.derivatives);
                var turbulence = ToTexture2D(cascade.turbulence);
                var displacements = displacement.GetPixels();
                var derivatives = derivative.GetPixels();
                var turbulences = turbulence.GetPixels();

                for (int x = 0; x < displacement.width; x++)
                {
                    for (int z = 0; z < displacement.height; z++)
                    {
                        int index = x * displacement.width + z;
                        sw.WriteLine(ToCSVEntry(
                            new Vector2(x, z),
                            spectrum,
                            new Vector3(cascade.lengthScale, cascade.cutoffLow, cascade.cutoffHigh),
                            time, deltaTime,
                            new Vector3(displacements[index].r, displacements[index].g, displacements[index].b),
                            derivatives[index],
                            turbulences[index].r
                        ));
                    }
                }
            }
        }

        /// <summary>
        /// Provides a CSV header row for generated data based on the water frequency spectrum.
        /// </summary>
        /// <param name="spectrum">Water frequency spectrum for formatting.</param>
        /// <returns>A CSV header row string.</returns>
        public static string CSVFormat(WaterFrequencySpectrum spectrum)
        {
            return $"x,z,{spectrum.CSVFormat()},scale,cutoffLow,cutoffHigh,time,deltaTime,displacementX,displacementY,displacementZ,dYx,dYz,dXx,dZz,turbulence";
        }

        /// <summary>
        /// Converts simulation data for a single pixel to a CSV-compatible string.
        /// </summary>
        /// <param name="xzPosition">Pixel position in 2D space (x, z coordinates).</param>
        /// <param name="spectrum">Spectrum data for the current pixel.</param>
        /// <param name="scaleCutoffs">Scale and cutoff values of the spectrum cascade.</param>
        /// <param name="time">Current simulation time.</param>
        /// <param name="deltaTime">Time difference between frames.</param>
        /// <param name="displacement">Displacement vector for the pixel (x, y, z components).</param>
        /// <param name="derivatives">Derivative values associated with the displacement.</param>
        /// <param name="turbulence">Single turbulence value for the pixel.</param>
        /// <returns>A CSV string representing the data for the given pixel.</returns>
        static string ToCSVEntry(Vector2 xzPosition, WaterFrequencySpectrum spectrum, Vector3 scaleCutoffs, float time, float deltaTime, Vector3 displacement, Vector4 derivatives, float turbulence)
        {
            // TODO: Consider revising scale and cutoffs handling when summing multiple cascades, as these are single-cascade properties.
            var csv = new StringBuilder(15 * 4); // min capacity = 15 floats * (3 digits + 1 digit sep.)

            // world pox
            csv.Append($"{xzPosition.x},{xzPosition.y},");

            // spectrum
            csv.Append($"{spectrum.ToCSV()},");

            // scaleCutoffs
            csv.Append($"{scaleCutoffs.x},{scaleCutoffs.y},{scaleCutoffs.z},");

            // timing
            csv.Append($"{time},{deltaTime},");

            // displacement
            csv.Append($"{displacement.x},{displacement.y},{displacement.z},");

            // derivatives
            csv.Append($"{derivatives.x},{derivatives.y},{derivatives.z},{derivatives.w},");

            // turbulence
            csv.Append($"{turbulence}");

            return csv.ToString();
        }

        /// <summary>
        /// Converts a RenderTexture to a Texture2D for further processing, such as reading pixel data.
        /// </summary>
        /// <param name="rt">RenderTexture to convert.</param>
        /// <returns>A Texture2D containing the data from the input RenderTexture.</returns>
        static Texture2D ToTexture2D(RenderTexture rt)
        {
            Texture2D tex = new Texture2D(rt.width, rt.height, UnityEngine.Experimental.Rendering.DefaultFormat.LDR, UnityEngine.Experimental.Rendering.TextureCreationFlags.DontInitializePixels | UnityEngine.Experimental.Rendering.TextureCreationFlags.DontUploadUponCreate);
            // ReadPixels looks at the active RenderTexture.
            var rtToRestore = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = rtToRestore;
            return tex;
        }
    }
}
