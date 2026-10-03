using System.Numerics;

namespace LithophaneGUI.Core;

/// <summary>
/// The light inside a light support, for the preview only: a mesh of the fixture (LED strip, tea
/// light, bulb…) and the points it emits light from, in the same frame as the lithophane.
/// </summary>
/// <param name="FacesUp">LEDs shine upwards (Lambertian around +Z); bulbs shine in every direction.</param>
/// <param name="WallOffset">Offset (along the footprint normal) of the wall between the slot and the light.</param>
/// <param name="WallTop">Height of that wall; light below it cannot reach the lithophane.</param>
public sealed record LightFixture(Mesh Mesh, Vector3[] Emitters, bool FacesUp, float WallOffset, float WallTop);

public static class Illumination
{
    /// <summary>Share of the light that still reaches the darkest spot; stands in for light bouncing around inside the base.</summary>
    private const float Fill = 0.15f;

    /// <summary>
    /// Relative light (0..1) arriving at each back-surface point of the lithophane from the fixture:
    /// an inverse-square sum over the emitters with the emitter's and the wall's cosine factors,
    /// shadowed by the wall between slot and light, then normalised to the brightest point.
    /// </summary>
    public static float[] Compute(Vector3[] pos, Vector3[] nrm, LightFixture light)
    {
        var result = new float[pos.Length];
        var emitters = light.Emitters;
        if (emitters.Length == 0)
        {
            Array.Fill(result, 1f);
            return result;
        }

        Parallel.For(0, pos.Length, k =>
        {
            var p = pos[k];
            var n = nrm[k];
            var n2 = new Vector2(n.X, n.Y);
            double sum = 0;
            foreach (var e in emitters)
            {
                var d = e - p;
                float dist2 = Math.Max(1f, d.LengthSquared());
                var dir = d / MathF.Sqrt(dist2);
                float receive = -Vector3.Dot(n, dir);
                if (receive <= 0) continue;
                float emit = light.FacesUp ? Math.Max(0f, -dir.Z) : 1f;
                if (emit <= 0) continue;

                // Where the ray crosses the wall plane, it must clear the top of the wall.
                float shadow = 1f;
                float dE = Vector2.Dot(new Vector2(d.X, d.Y), n2);
                if (dE < light.WallOffset)
                {
                    float zCross = p.Z + d.Z * (light.WallOffset / dE);
                    shadow = Math.Clamp((zCross - light.WallTop) / 1.5f + 0.5f, 0f, 1f);
                }
                sum += receive * emit * shadow / dist2;
            }
            result[k] = (float)sum;
        });

        float max = result.Max();
        if (max <= 0) { Array.Fill(result, 1f); return result; }
        for (int k = 0; k < result.Length; k++)
            result[k] = Fill + (1 - Fill) * MathF.Sqrt(result[k] / max);
        return result;
    }
}
