using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using Water;
using Water.Spectrum;

namespace NN
{
    /// <summary>
    /// Class to generate CSV data to feed to a neural network.
    /// </summary>
    public class NNDataGenerator : IDisposable
    {
        private SpectrumCascade cascade;
        private FastFourierTransform fft;

        public NNDataGenerator(int size, Vector3 scaleCutoffs)
        {
            var settings = GraphicsSettings.GetRenderPipelineSettings<WaterSimulationSettings>();

            cascade = new SpectrumCascade($"{nameof(NNDataGenerator)}", size, scaleCutoffs.x, scaleCutoffs.y, scaleCutoffs.z);
            fft = new FastFourierTransform(size, settings.FFTShader);
        }

        public void Dispose()
        {
            cascade.Dispose();
            fft.Dispose();
        }

        /// <summary>
        /// Simulates a single frame and writes it in CSV format to the provided stream.
        /// </summary>
        /// <param name="stream">Stream to write CSV data to</param>
        /// <param name="spectrum">Spectrum to generate data for</param>
        /// <param name="time">Elapsed time</param>
        /// <param name="deltaTime">Delta time</param>
        /// <param name="noise">Gaussian noise, should be as big or bigger than the simulated size to avoid repetition</param>
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

        public static string CSVFormat(WaterFrequencySpectrum spectrum)
        {
            return $"x,z,{spectrum.CSVFormat()},scale,cutoffLow,cutoffHigh,time,deltaTime,displacementX,displacementY,displacementZ,dYx,dYz,dXx,dZz,turbulence";
        }

        // TODO: se sommo più cascades non ha senso avere il vettore scale + cutoffs, dato che sono proprieta della singola cascade
        static string ToCSVEntry(Vector2 xzPosition, WaterFrequencySpectrum spectrum, Vector3 scaleCutoffs, float time, float deltaTime, Vector3 displacement, Vector4 derivatives, float turbulence)
        {
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
